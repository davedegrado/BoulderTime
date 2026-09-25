using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Leaderboards;
using BoulderTime.Domain.Staff;
using BoulderTime.Application.Boulders;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Leaderboards;

public sealed record LeaderboardVisibilityRequest(bool? Hidden);
public sealed record ReportClimberRequest(Guid? UserId, string? Reason);
public sealed record HandleLeaderboardReportRequest(bool? Exclude, string? Note);

public sealed record LeaderboardReportDto(
    Guid Id, Guid GymId, string GymName, PersonDto Climber, PersonDto ReportedBy, string Reason,
    LeaderboardReportStatus Status, DateTimeOffset CreatedAt, bool ClimberIsExcluded);

public sealed record ExcludedClimberDto(PersonDto Climber, DateTimeOffset ExcludedAt);

/// <summary>
/// Who appears in leaderboards. Two independent switches: the climber's own choice, and an exclusion set by
/// BoulderTime when results look implausible. Gyms can report a climber but never exclude one — the gym would
/// otherwise be judging its own members.
/// </summary>
public sealed class LeaderboardVisibilityService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    /// <summary>The climber's own choice to stay out of leaderboards.</summary>
    public async Task SetOwnVisibilityAsync(LeaderboardVisibilityRequest r, CancellationToken ct = default)
    {
        if (r.Hidden is not { } hidden) throw new ValidationException("hidden", "Say whether to hide yourself.");
        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        user.SetLeaderboardOptOut(hidden);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Gym staff flag a climber to BoulderTime. Staff of that gym only, and never themselves.</summary>
    public async Task<LeaderboardReportDto> ReportAsync(Guid gymId, ReportClimberRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var reporter = currentUser.RequireUserId();
        if (r.UserId is not { } reportedUserId) throw new ValidationException("userId", "Choose the climber to report.");
        var reason = Input.Trimmed(r.Reason);
        if (reason.Length == 0) throw new ValidationException("reason", "Say what looks wrong.");
        if (reason.Length > LeaderboardReport.ReasonMaxLength)
            throw new ValidationException("reason", $"Keep it to {LeaderboardReport.ReasonMaxLength} characters or fewer.");
        if (reportedUserId == reporter) throw new ValidationException("userId", "You can't report yourself.");
        _ = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == reportedUserId, ct)
            ?? throw new NotFoundException("User", reportedUserId);

        var existing = await db.LeaderboardReports
            .FirstOrDefaultAsync(x => x.GymId == gymId && x.ReportedUserId == reportedUserId && x.Status == LeaderboardReportStatus.Pending, ct);
        if (existing is null)
        {
            existing = LeaderboardReport.Create(gymId, reportedUserId, reporter, reason, clock.UtcNow);
            db.LeaderboardReports.Add(existing);
            await db.SaveChangesAsync(ct);
        }
        return (await ToDtosAsync([existing], ct))[0];
    }

    public async Task<IReadOnlyList<LeaderboardReportDto>> ListReportsAsync(bool includeHandled, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var query = db.LeaderboardReports.AsNoTracking();
        if (!includeHandled) query = query.Where(x => x.Status == LeaderboardReportStatus.Pending);
        var reports = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return await ToDtosAsync(reports, ct);
    }

    /// <summary>BoulderTime decides: exclude the climber from every leaderboard, or dismiss the report.</summary>
    public async Task<LeaderboardReportDto> HandleReportAsync(Guid reportId, HandleLeaderboardReportRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var adminId = currentUser.RequireUserId();
        if (r.Exclude is not { } exclude) throw new ValidationException("exclude", "Say whether to exclude the climber.");
        var report = await db.LeaderboardReports.FirstOrDefaultAsync(x => x.Id == reportId, ct)
            ?? throw new NotFoundException("LeaderboardReport", reportId);

        if (exclude)
        {
            var climber = await db.Users.FirstOrDefaultAsync(u => u.Id == report.ReportedUserId, ct)
                ?? throw new NotFoundException("User", report.ReportedUserId);
            climber.ExcludeFromLeaderboards(adminId, clock.UtcNow);
        }
        report.Handle(exclude ? LeaderboardReportStatus.Excluded : LeaderboardReportStatus.Dismissed, adminId, r.Note, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([report], ct))[0];
    }

    public async Task<IReadOnlyList<ExcludedClimberDto>> ListExcludedAsync(CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => u.LeaderboardExcludedAt != null)
            .OrderByDescending(u => u.LeaderboardExcludedAt).ToListAsync(ct);
        return users.Select(u => new ExcludedClimberDto(Person(u), u.LeaderboardExcludedAt!.Value)).ToList();
    }

    /// <summary>Lets an administrator undo an exclusion — the climber's own opt-out is left alone.</summary>
    public async Task AllowAsync(Guid userId, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        user.AllowInLeaderboards();
        await db.SaveChangesAsync(ct);
    }

    private static PersonDto Person(Domain.Users.User u) => new(u.Id, u.DisplayName, u.AvatarUrl);

    private async Task<IReadOnlyList<LeaderboardReportDto>> ToDtosAsync(List<LeaderboardReport> reports, CancellationToken ct)
    {
        if (reports.Count == 0) return [];
        var userIds = reports.SelectMany(r => new[] { r.ReportedUserId, r.ReportedByUserId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var gymIds = reports.Select(r => r.GymId).Distinct().ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        return reports.Select(r => new LeaderboardReportDto(
            r.Id, r.GymId, gyms.GetValueOrDefault(r.GymId, ""),
            Person(users[r.ReportedUserId]), Person(users[r.ReportedByUserId]),
            r.Reason, r.Status, r.CreatedAt, users[r.ReportedUserId].LeaderboardExcludedAt is not null)).ToList();
    }
}
