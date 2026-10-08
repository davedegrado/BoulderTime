using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

public sealed record ReportUserRequest(UserReportReason? Reason, string? Description);
public sealed record HandleUserReportRequest(bool? Suspend, string? Note);
public sealed record SuspendUserRequest(string? Reason);

public sealed record UserReportDto(
    Guid Id, PersonDto Person, PersonDto ReportedBy, UserReportReason Reason, string? Description,
    UserReportStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? HandledAt, string? HandlingNote,
    bool PersonIsSuspended, int OpenReportsAgainstPerson);

/// <summary>
/// Reporting a person, and suspending an account across BoulderTime. Anyone signed in can report someone; only
/// BoulderTime administrators see the reports and decide, because a person is not any one gym's member. A suspension
/// hides what the person wrote and stops them doing anything, until an administrator lifts it.
/// </summary>
public sealed class UserModerationService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    /// <summary>
    /// Files a report. Reporting the same person twice while the first report is open changes nothing, so the queue
    /// counts people who complained, not clicks.
    /// </summary>
    public async Task ReportAsync(Guid reportedUserId, ReportUserRequest r, CancellationToken ct = default)
    {
        var reporter = currentUser.RequireUserId();
        if (reportedUserId == reporter) throw new ValidationException("userId", "You can't report yourself.");
        if (r.Reason is not { } reason || !Enum.IsDefined(reason)) throw new ValidationException("reason", "Choose a reason.");
        var description = Input.Trimmed(r.Description);
        if (description.Length > UserReport.DescriptionMaxLength)
            throw new ValidationException("description", $"Keep it to {UserReport.DescriptionMaxLength} characters or fewer.");
        if (!await db.Users.AnyAsync(u => u.Id == reportedUserId, ct)) throw new NotFoundException("User", reportedUserId);

        var open = await db.UserReports.AnyAsync(x =>
            x.ReportedByUserId == reporter && x.ReportedUserId == reportedUserId && x.Status == UserReportStatus.Pending, ct);
        if (open) return;

        db.UserReports.Add(UserReport.Create(reportedUserId, reporter, reason, description, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<UserReportDto>> ListReportsAsync(bool includeHandled, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var query = db.UserReports.AsNoTracking();
        if (!includeHandled) query = query.Where(x => x.Status == UserReportStatus.Pending);
        var reports = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        return await ToDtosAsync(reports, ct);
    }

    /// <summary>
    /// BoulderTime decides on a report: suspend the account or dismiss. Suspending settles every open report about the
    /// same person at once, since they were all asking for the same thing.
    /// </summary>
    public async Task<UserReportDto> HandleReportAsync(Guid reportId, HandleUserReportRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var adminId = currentUser.RequireUserId();
        if (r.Suspend is not { } suspend) throw new ValidationException("suspend", "Say whether to suspend the account.");
        var note = Input.Trimmed(r.Note);
        if (note.Length > UserReport.DescriptionMaxLength)
            throw new ValidationException("note", $"Keep the note to {UserReport.DescriptionMaxLength} characters or fewer.");
        var report = await db.UserReports.FirstOrDefaultAsync(x => x.Id == reportId, ct)
            ?? throw new NotFoundException("UserReport", reportId);
        if (report.Status != UserReportStatus.Pending) throw new ConflictException("This report was already handled.");

        var now = clock.UtcNow;
        if (suspend)
        {
            var person = await RequireSuspendableAsync(report.ReportedUserId, adminId, ct);
            if (!person.IsSuspended) person.Suspend(adminId, note, now);
            var open = await db.UserReports
                .Where(x => x.ReportedUserId == report.ReportedUserId && x.Status == UserReportStatus.Pending)
                .ToListAsync(ct);
            foreach (var other in open) other.Handle(UserReportStatus.Suspended, adminId, note, now);
        }
        else
        {
            report.Handle(UserReportStatus.Dismissed, adminId, note, now);
        }
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([report], ct))[0];
    }

    /// <summary>Suspends an account directly, without a report (from the list of people).</summary>
    public async Task SuspendAsync(Guid userId, SuspendUserRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var adminId = currentUser.RequireUserId();
        var reason = Input.Trimmed(r.Reason);
        if (reason.Length > User.SuspensionReasonMaxLength)
            throw new ValidationException("reason", $"Keep the note to {User.SuspensionReasonMaxLength} characters or fewer.");
        var person = await RequireSuspendableAsync(userId, adminId, ct);
        if (person.IsSuspended) return;
        person.Suspend(adminId, reason, clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Lifts a suspension. Reports already handled stay as they were: they record what was decided then.</summary>
    public async Task ReinstateAsync(Guid userId, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var person = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        if (!person.IsSuspended) return;
        person.Reinstate();
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Administrators can't suspend themselves or each other: one admin locking out the others is not moderation.
    /// Revoking the admin role is a deliberate, separate step (the CLI).
    /// </summary>
    private async Task<User> RequireSuspendableAsync(Guid userId, Guid adminId, CancellationToken ct)
    {
        if (userId == adminId) throw new ValidationException("userId", "You can't suspend yourself.");
        var person = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        if (person.IsPlatformAdmin)
            throw new ForbiddenException("A BoulderTime administrator can't be suspended. Revoke the role first.", "cannot_suspend_admin");
        return person;
    }

    private static PersonDto Person(User u) => new(u.Id, u.DisplayName, u.AvatarUrl);

    private async Task<IReadOnlyList<UserReportDto>> ToDtosAsync(List<UserReport> reports, CancellationToken ct)
    {
        if (reports.Count == 0) return [];
        var userIds = reports.SelectMany(r => new[] { r.ReportedUserId, r.ReportedByUserId }).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
        var reportedIds = reports.Select(r => r.ReportedUserId).Distinct().ToList();
        var openCounts = await db.UserReports.AsNoTracking()
            .Where(x => reportedIds.Contains(x.ReportedUserId) && x.Status == UserReportStatus.Pending)
            .GroupBy(x => x.ReportedUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        return reports.Select(r => new UserReportDto(
            r.Id, Person(users[r.ReportedUserId]), Person(users[r.ReportedByUserId]), r.Reason, r.Description,
            r.Status, r.CreatedAt, r.HandledAt, r.HandlingNote,
            users[r.ReportedUserId].IsSuspended, openCounts.GetValueOrDefault(r.ReportedUserId))).ToList();
    }
}
