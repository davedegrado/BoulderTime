using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Notifications;

/// <summary>
/// Decides WHO gets notified and adds the notifications to the unit of work; the caller's SaveChanges commits them
/// together with the change that caused them. Rules applied to every notification:
///   • the person who acted is never notified about their own action;
///   • each recipient gets at most one notification per event (audiences are de-duplicated);
///   • follow-level "notifications off" and the user's category settings are respected;
///   • bursts with a collapse key fold into one unread notification.
/// </summary>
public sealed class NotificationPublisher(IAppDbContext db, IClock clock)
{
    public async Task AnnouncementAsync(GymAnnouncement a, Gym gym, string? sectorName, CancellationToken ct)
    {
        if (!a.NotifyFollowers) return;
        var audience = await db.GymFollows.Where(f => f.GymId == gym.Id && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct);
        if (a.SectorId is { } sectorId)
            audience.AddRange(await db.SectorFollows.Where(f => f.SectorId == sectorId && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct));

        var prefix = a.Type switch
        {
            AnnouncementType.Event => "Event",
            AnnouncementType.Competition => "Competition",
            AnnouncementType.ScheduleChange => "Schedule change",
            AnnouncementType.Maintenance => "Maintenance",
            _ => null,
        };
        var title = $"{gym.Name}: {(prefix is null ? a.Title : $"{prefix} · {a.Title}")}";
        var body = sectorName is null ? Excerpt(a.Content) : $"{sectorName} · {Excerpt(a.Content)}";
        await PublishAsync(audience, a.CreatedByUserId, NotificationType.GymAnnouncement, title, body,
            RelatedEntityType.Announcement, a.Id, gym.Id, $"/gyms/{gym.Slug}?tab=updates", collapseKey: null, ct);
    }

    /// <summary>
    /// One notification per sector per person, never one per removed boulder. The audience is the sector's followers
    /// plus anyone following one of the removed boulders in that sector.
    /// </summary>
    public async Task SectorsRetracedAsync(Gym gym, IReadOnlyList<(Sector Sector, IReadOnlyList<Guid> BoulderIds)> sectors, Guid actorId, CancellationToken ct)
    {
        foreach (var (sector, boulderIds) in sectors)
        {
            var audience = await db.SectorFollows.Where(f => f.SectorId == sector.Id && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct);
            audience.AddRange(await db.BoulderFollows.Where(f => boulderIds.Contains(f.BoulderId) && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct));
            var n = boulderIds.Count;
            await PublishAsync(audience, actorId, NotificationType.SectorRetraced,
                $"Sector {sector.Name} has been retraced", $"{gym.Name} removed {n} {(n == 1 ? "boulder" : "boulders")}. Fresh problems are on the way.",
                RelatedEntityType.Sector, sector.Id, gym.Id, $"/gyms/{gym.Slug}", collapseKey: null, ct);
        }
    }

    /// <summary>
    /// A new boulder. Sector followers get "New boulder in Cave"; gym followers who don't follow that sector get
    /// "New boulder at Crimp Factory". Both collapse while unread ("5 new boulders in Cave"), so a setting session
    /// produces one notification per person instead of one per boulder. Someone who follows the sector but muted it
    /// gets nothing through the gym either.
    /// </summary>
    public async Task BoulderCreatedAsync(Boulder boulder, Gym gym, Sector sector, string summary, Guid actorId, CancellationToken ct)
    {
        var sectorFollowers = await db.SectorFollows.Where(f => f.SectorId == sector.Id).Select(f => new { f.UserId, f.NotificationsEnabled }).ToListAsync(ct);
        var boulderLink = $"/boulders/{boulder.Id}";
        var gymLink = $"/gyms/{gym.Slug}";

        await PublishAsync(sectorFollowers.Where(f => f.NotificationsEnabled).Select(f => f.UserId), actorId, NotificationType.NewBouldersInSector,
            $"New boulder in {sector.Name}", $"{gym.Name} · {summary}", RelatedEntityType.Boulder, boulder.Id, gym.Id, boulderLink,
            collapseKey: $"new-boulders:sector:{sector.Id}", ct,
            collapsed: n => ($"{n} new boulders in {sector.Name}", $"{gym.Name} · latest: {summary}", gymLink, RelatedEntityType.Sector, sector.Id));

        var sectorFollowerIds = sectorFollowers.Select(f => f.UserId).ToHashSet();
        var gymAudience = (await db.GymFollows.Where(f => f.GymId == gym.Id && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct))
            .Where(u => !sectorFollowerIds.Contains(u));
        await PublishAsync(gymAudience, actorId, NotificationType.NewBouldersAtGym,
            $"New boulder at {gym.Name}", $"{sector.Name} · {summary}", RelatedEntityType.Boulder, boulder.Id, gym.Id, boulderLink,
            collapseKey: $"new-boulders:gym:{gym.Id}", ct,
            collapsed: n => ($"{n} new boulders at {gym.Name}", $"Latest in {sector.Name}: {summary}", gymLink, RelatedEntityType.Gym, gym.Id));
    }

    public async Task BoulderUpdatedAsync(Boulder boulder, Gym gym, string sectorName, string whatChanged, Guid actorId, CancellationToken ct)
    {
        var audience = await BoulderFollowersAsync(boulder.Id, ct);
        await PublishAsync(audience, actorId, NotificationType.BoulderUpdated, $"A boulder you follow was updated",
            $"{sectorName} · {gym.Name} — {whatChanged}", RelatedEntityType.Boulder, boulder.Id, gym.Id, $"/boulders/{boulder.Id}",
            collapseKey: $"boulder-updated:{boulder.Id}", ct);
    }

    /// <summary>Followers of the boulder, plus climbers who are projecting it (tried, not sent) — beta matters most to them.</summary>
    public async Task OfficialBetaAsync(Boulder boulder, Gym gym, string sectorName, Guid actorId, CancellationToken ct)
    {
        var audience = await BoulderFollowersAsync(boulder.Id, ct);
        audience.AddRange(await db.BoulderAttempts.Where(a => a.BoulderId == boulder.Id && !a.Completed && a.Attempts > 0).Select(a => a.UserId).ToListAsync(ct));
        await PublishAsync(audience, actorId, NotificationType.OfficialBeta, "New official beta",
            $"{gym.Name} posted beta for a boulder in {sectorName}.", RelatedEntityType.Boulder, boulder.Id, gym.Id, $"/boulders/{boulder.Id}",
            collapseKey: $"beta:{boulder.Id}", ct);
    }

    /// <summary>Comments fold into one unread notification per boulder: "3 new comments on a boulder you follow".</summary>
    public async Task CommentAsync(Comment comment, Boulder boulder, string authorName, CancellationToken ct)
    {
        var audience = await BoulderFollowersAsync(boulder.Id, ct);
        await PublishAsync(audience, comment.UserId, NotificationType.BoulderComments, $"{authorName} commented on a boulder you follow",
            Excerpt(comment.Content), RelatedEntityType.Boulder, boulder.Id, boulder.GymId, $"/boulders/{boulder.Id}",
            collapseKey: $"comments:{boulder.Id}", ct,
            collapsed: count => ($"{count} new comments on a boulder you follow", Excerpt(comment.Content), null, null, null));
    }

    public Task VideoReviewedAsync(BoulderVideo video, Guid gymId, Guid reviewerId, CancellationToken ct) =>
        video.Status == VideoStatus.Approved
            ? PublishAsync([video.UserId], reviewerId, NotificationType.VideoApproved, "Your video is live",
                "The gym approved your video. Everyone can watch it now.", RelatedEntityType.Video, video.Id, gymId, $"/boulders/{video.BoulderId}", null, ct)
            : PublishAsync([video.UserId], reviewerId, NotificationType.VideoRejected, "Your video wasn't approved",
                video.RejectionReason, RelatedEntityType.Video, video.Id, gymId, $"/boulders/{video.BoulderId}", null, ct);

    public Task ReportReviewedAsync(Report report, Guid boulderId, Guid reviewerId, CancellationToken ct) =>
        PublishAsync([report.ReportedByUserId], reviewerId, NotificationType.ReportReviewed,
            report.Status == ReportStatus.Resolved ? "Thanks — your report was handled" : "Your report was reviewed",
            report.Status == ReportStatus.Resolved ? "The gym took action on the content you reported." : "The gym reviewed it and didn't find a problem.",
            RelatedEntityType.Report, report.Id, report.GymId, boulderId == Guid.Empty ? "/notifications" : $"/boulders/{boulderId}", null, ct);

    private async Task<List<Guid>> BoulderFollowersAsync(Guid boulderId, CancellationToken ct) =>
        await db.BoulderFollows.Where(f => f.BoulderId == boulderId && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct);

    private async Task PublishAsync(IEnumerable<Guid> audience, Guid? actorId, NotificationType type, string title, string? body,
        RelatedEntityType relatedType, Guid relatedId, Guid? gymId, string link, string? collapseKey, CancellationToken ct,
        Func<int, (string Title, string? Body, string? Link, RelatedEntityType? RelatedType, Guid? RelatedId)>? collapsed = null)
    {
        var recipients = audience.Where(u => u != actorId).Distinct().ToList();
        if (recipients.Count == 0) return;

        var category = Notification.CategoryOf(type);
        var muted = (await db.NotificationSettings.AsNoTracking().Where(s => recipients.Contains(s.UserId)).ToListAsync(ct))
            .Where(s => !s.Allows(category)).Select(s => s.UserId).ToHashSet();
        recipients.RemoveAll(muted.Contains);
        if (recipients.Count == 0) return;

        var now = clock.UtcNow;
        var open = collapseKey is null
            ? []
            : await db.Notifications.Where(n => n.CollapseKey == collapseKey && n.ReadAt == null && recipients.Contains(n.UserId)).ToDictionaryAsync(n => n.UserId, ct);

        foreach (var userId in recipients)
        {
            if (open.TryGetValue(userId, out var existing))
            {
                var c = collapsed?.Invoke(existing.Count + 1);
                existing.Collapse(c?.Title ?? title, c is null ? body : c.Value.Body, now, c?.Link, c?.RelatedType, c?.RelatedId);
            }
            else
                db.Notifications.Add(Notification.Create(userId, type, title, body, relatedType, relatedId, gymId, link, collapseKey, now));
        }
    }

    private static string Excerpt(string text) => text.Length <= 140 ? text : text[..139] + "…";
}
