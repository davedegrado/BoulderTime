using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Community;

public sealed record CreateReportRequest(ReportEntityType? EntityType, Guid? EntityId, ReportReason? Reason, string? Description);

public enum ReportAction
{
    /// <summary>Close without touching the content.</summary>
    None = 0,
    /// <summary>Hide the comment, or reject the video, then resolve.</summary>
    RemoveContent = 1,
}

public sealed record CloseReportRequest(ReportAction? Action, string? Note);

public sealed record ReportDto(
    Guid Id, ReportEntityType EntityType, Guid EntityId, Guid GymId, string GymName, Guid BoulderId,
    ReportReason Reason, string? Description, ReportStatus Status, string? ResolutionNote,
    PersonDto ReportedBy, DateTimeOffset CreatedAt, DateTimeOffset? ReviewedAt, string? Excerpt);

public sealed record ModerationSummaryDto(int PendingVideos, int PendingReports);

/// <summary>
/// Reports on comments, videos and boulders. Staff (STAFF+) moderate their own gym's reports; platform admins moderate all.
/// A user has at most one open report per item, and can't report content they can't see.
/// </summary>
public sealed class ReportService(IAppDbContext db, BoulderAccess boulders, GymAccess gyms, ICurrentUser currentUser, IClock clock)
{
    public async Task<ReportDto> CreateAsync(CreateReportRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        if (r.EntityType is not { } type || !Enum.IsDefined(type) || r.EntityId is not { } entityId)
            throw new ValidationException("entityId", "Choose what you're reporting.");
        if (r.Reason is not { } reason || !Enum.IsDefined(reason)) throw new ValidationException("reason", "Choose a reason.");
        if (Input.Trimmed(r.Description).Length > Report.DescriptionMaxLength)
            throw new ValidationException("description", $"Keep it to {Report.DescriptionMaxLength} characters or fewer.");
        if (reason == ReportReason.Other && Input.Trimmed(r.Description).Length < 3)
            throw new ValidationException("description", "Tell us a bit more about the problem.");

        var boulderId = await BoulderOfAsync(type, entityId, userId, ct) ?? throw new NotFoundException("Content", entityId);
        var scope = await boulders.RequireVisibleAsync(boulderId, ct);

        if (await db.Reports.AnyAsync(x => x.ReportedByUserId == userId && x.EntityType == type && x.EntityId == entityId && x.Status == ReportStatus.Pending, ct))
            throw new ConflictException("You've already reported this. The gym will review it.", "already_reported");

        var report = Report.Create(userId, type, entityId, scope.Gym.Id, reason, r.Description, clock.UtcNow);
        db.Reports.Add(report);
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { throw new ConflictException("You've already reported this. The gym will review it.", "already_reported"); }
        return (await ToDtosAsync([report], ct))[0];
    }

    public async Task<PagedResult<ReportDto>> ListForGymAsync(Guid gymId, ReportStatus? status, int? page, CancellationToken ct = default)
    {
        await gyms.RequireRoleAsync(gymId, GymRole.Staff, ct);
        return await ListAsync(db.Reports.AsNoTracking().Where(x => x.GymId == gymId), status, page, ct);
    }

    public async Task<PagedResult<ReportDto>> ListAllAsync(ReportStatus? status, int? page, CancellationToken ct = default)
    {
        await gyms.RequirePlatformAdminAsync(ct);
        return await ListAsync(db.Reports.AsNoTracking(), status, page, ct);
    }

    public async Task<ReportDto> ResolveAsync(Guid reportId, CloseReportRequest r, CancellationToken ct = default) =>
        await CloseAsync(reportId, ReportStatus.Resolved, r, ct);

    public async Task<ReportDto> DismissAsync(Guid reportId, CloseReportRequest r, CancellationToken ct = default) =>
        await CloseAsync(reportId, ReportStatus.Dismissed, r with { Action = ReportAction.None }, ct);

    public async Task<ModerationSummaryDto> SummaryAsync(Guid gymId, CancellationToken ct = default)
    {
        await gyms.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var pendingVideos = await db.BoulderVideos.Where(v => v.Status == VideoStatus.Pending)
            .Join(db.Boulders, v => v.BoulderId, b => b.Id, (v, b) => b.GymId).CountAsync(g => g == gymId, ct);
        var pendingReports = await db.Reports.CountAsync(x => x.GymId == gymId && x.Status == ReportStatus.Pending, ct);
        return new ModerationSummaryDto(pendingVideos, pendingReports);
    }

    private async Task<ReportDto> CloseAsync(Guid reportId, ReportStatus status, CloseReportRequest r, CancellationToken ct)
    {
        var reviewer = currentUser.RequireUserId();
        var report = await db.Reports.FirstOrDefaultAsync(x => x.Id == reportId, ct) ?? throw new NotFoundException("Report", reportId);
        var role = await gyms.GetRoleAsync(report.GymId, ct);
        if (role is null) throw new NotFoundException("Report", reportId);
        if (report.Status != ReportStatus.Pending) throw new ConflictException("This report was already handled.", "report_closed");
        if (Input.Trimmed(r.Note).Length > 500) throw new ValidationException("note", "Keep the note to 500 characters or fewer.");

        if (r.Action == ReportAction.RemoveContent)
        {
            switch (report.EntityType)
            {
                case ReportEntityType.Comment:
                    var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == report.EntityId, ct);
                    comment?.Hide();
                    break;
                case ReportEntityType.Video:
                    var video = await db.BoulderVideos.FirstOrDefaultAsync(v => v.Id == report.EntityId, ct);
                    if (video is not null && video.UserId != reviewer)
                        video.Reject(reviewer, clock.UtcNow, string.IsNullOrWhiteSpace(r.Note) ? "Removed after a report." : r.Note.Trim());
                    else if (video is not null)
                        throw new ForbiddenException("You can't moderate your own video.", "own_video");
                    break;
                case ReportEntityType.Boulder:
                    throw new ValidationException("action", "Boulders can't be removed from a report. Edit or remove the boulder in the staff area.");
            }
        }

        report.Close(status, reviewer, r.Note, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([report], ct))[0];
    }

    private async Task<PagedResult<ReportDto>> ListAsync(IQueryable<Report> q, ReportStatus? status, int? page, CancellationToken ct)
    {
        var (p, size) = Paging.Normalize(page, 30);
        q = q.Where(x => x.Status == (status ?? ReportStatus.Pending));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ReportDto>(await ToDtosAsync(rows, ct), p, size, total);
    }

    /// <summary>Resolves the boulder a reported item belongs to. Hidden comments only count for their author.</summary>
    private async Task<Guid?> BoulderOfAsync(ReportEntityType type, Guid entityId, Guid userId, CancellationToken ct) => type switch
    {
        ReportEntityType.Boulder => await db.Boulders.AnyAsync(b => b.Id == entityId, ct) ? entityId : null,
        ReportEntityType.Comment => await db.Comments.Where(c => c.Id == entityId && (c.Status == CommentStatus.Visible || c.UserId == userId)).Select(c => (Guid?)c.BoulderId).FirstOrDefaultAsync(ct),
        ReportEntityType.Video => await db.BoulderVideos.Where(v => v.Id == entityId && (v.Status == VideoStatus.Approved || v.UserId == userId)).Select(v => (Guid?)v.BoulderId).FirstOrDefaultAsync(ct),
        _ => null,
    };

    private async Task<IReadOnlyList<ReportDto>> ToDtosAsync(IReadOnlyList<Report> reports, CancellationToken ct)
    {
        var reporterIds = reports.Select(x => x.ReportedByUserId).Distinct().ToList();
        var people = await db.Users.AsNoTracking().Where(u => reporterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl), ct);
        var gymIds = reports.Select(x => x.GymId).Distinct().ToList();
        var gymNames = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var commentIds = reports.Where(x => x.EntityType == ReportEntityType.Comment).Select(x => x.EntityId).ToList();
        var comments = await db.Comments.AsNoTracking().Where(c => commentIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => (c.BoulderId, Text: c.Content), ct);
        var videoIds = reports.Where(x => x.EntityType == ReportEntityType.Video).Select(x => x.EntityId).ToList();
        var videoRows = await db.BoulderVideos.AsNoTracking().Where(v => videoIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => (v.BoulderId, Text: v.Caption), ct);

        return reports.Select(x =>
        {
            var (boulderId, excerpt) = x.EntityType switch
            {
                ReportEntityType.Comment when comments.TryGetValue(x.EntityId, out var c) => (c.BoulderId, (string?)c.Text),
                ReportEntityType.Video when videoRows.TryGetValue(x.EntityId, out var v) => (v.BoulderId, v.Text),
                ReportEntityType.Boulder => (x.EntityId, null),
                _ => (Guid.Empty, "(content deleted)"),
            };
            if (excerpt is { Length: > 160 }) excerpt = excerpt[..160] + "…";
            return new ReportDto(x.Id, x.EntityType, x.EntityId, x.GymId, gymNames.GetValueOrDefault(x.GymId, ""), boulderId, x.Reason, x.Description,
                x.Status, x.ResolutionNote, people.GetValueOrDefault(x.ReportedByUserId, new PersonDto(x.ReportedByUserId, "Climber", null)),
                x.CreatedAt, x.ReviewedAt, excerpt);
        }).ToList();
    }
}
