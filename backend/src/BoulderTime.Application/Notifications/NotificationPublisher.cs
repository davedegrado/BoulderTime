using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Community;
using BoulderTime.Application.Localization;
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

        await PublishAsync(audience, a.CreatedByUserId, NotificationType.GymAnnouncement,
            lang => NotificationTexts.Announcement(lang, gym.Name, a.Type, a.Title, Excerpt(a.Content), sectorName),
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
                lang => NotificationTexts.SectorRetraced(lang, gym.Name, sector.Name, n),
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
            lang => NotificationTexts.NewBoulderInSector(lang, gym.Name, sector.Name, summary, 1),
            RelatedEntityType.Boulder, boulder.Id, gym.Id, boulderLink,
            collapseKey: $"new-boulders:sector:{sector.Id}", ct,
            collapsed: (lang, n) => (NotificationTexts.NewBoulderInSector(lang, gym.Name, sector.Name, summary, n), gymLink, RelatedEntityType.Sector, sector.Id));

        var sectorFollowerIds = sectorFollowers.Select(f => f.UserId).ToHashSet();
        var gymAudience = (await db.GymFollows.Where(f => f.GymId == gym.Id && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct))
            .Where(u => !sectorFollowerIds.Contains(u));
        await PublishAsync(gymAudience, actorId, NotificationType.NewBouldersAtGym,
            lang => NotificationTexts.NewBoulderAtGym(lang, gym.Name, sector.Name, summary, 1),
            RelatedEntityType.Boulder, boulder.Id, gym.Id, boulderLink,
            collapseKey: $"new-boulders:gym:{gym.Id}", ct,
            collapsed: (lang, n) => (NotificationTexts.NewBoulderAtGym(lang, gym.Name, sector.Name, summary, n), gymLink, RelatedEntityType.Gym, gym.Id));
    }

    public async Task BoulderUpdatedAsync(Boulder boulder, Gym gym, string sectorName, IReadOnlyList<NotificationTexts.BoulderChange> changes, Guid actorId, CancellationToken ct)
    {
        var audience = await BoulderFollowersAsync(boulder.Id, ct);
        await PublishAsync(audience, actorId, NotificationType.BoulderUpdated,
            lang => NotificationTexts.BoulderUpdated(lang, gym.Name, sectorName, changes),
            RelatedEntityType.Boulder, boulder.Id, gym.Id, $"/boulders/{boulder.Id}",
            collapseKey: $"boulder-updated:{boulder.Id}", ct);
    }

    /// <summary>Followers of the boulder, plus climbers who are projecting it (tried, not sent) — beta matters most to them.</summary>
    public async Task OfficialBetaAsync(Boulder boulder, Gym gym, string sectorName, Guid actorId, CancellationToken ct)
    {
        var audience = await BoulderFollowersAsync(boulder.Id, ct);
        audience.AddRange(await db.BoulderAttempts.Where(a => a.BoulderId == boulder.Id && !a.Completed && a.Attempts > 0).Select(a => a.UserId).ToListAsync(ct));
        await PublishAsync(audience, actorId, NotificationType.OfficialBeta,
            lang => NotificationTexts.OfficialBeta(lang, gym.Name, sectorName),
            RelatedEntityType.Boulder, boulder.Id, gym.Id, $"/boulders/{boulder.Id}",
            collapseKey: $"beta:{boulder.Id}", ct);
    }

    /// <summary>Comments fold into one unread notification per boulder: "3 new comments on a boulder you follow".</summary>
    public async Task CommentAsync(Comment comment, Boulder boulder, string authorName, CancellationToken ct)
    {
        var audience = await BoulderFollowersAsync(boulder.Id, ct);
        await PublishAsync(audience, comment.UserId, NotificationType.BoulderComments,
            lang => NotificationTexts.Comment(lang, authorName, Excerpt(comment.Content), 1),
            RelatedEntityType.Boulder, boulder.Id, boulder.GymId, $"/boulders/{boulder.Id}",
            collapseKey: $"comments:{boulder.Id}", ct,
            collapsed: (lang, count) => (NotificationTexts.Comment(lang, authorName, Excerpt(comment.Content), count), null, null, null));
    }

    public Task VideoReviewedAsync(BoulderVideo video, Guid gymId, Guid reviewerId, CancellationToken ct) =>
        video.Status == VideoStatus.Approved
            ? PublishAsync([video.UserId], reviewerId, NotificationType.VideoApproved, NotificationTexts.VideoApproved,
                RelatedEntityType.Video, video.Id, gymId, $"/boulders/{video.BoulderId}", null, ct)
            : PublishAsync([video.UserId], reviewerId, NotificationType.VideoRejected,
                lang => NotificationTexts.VideoRejected(lang, video.RejectionReason),
                RelatedEntityType.Video, video.Id, gymId, $"/boulders/{video.BoulderId}", null, ct);

    public Task ReportReviewedAsync(Report report, Guid boulderId, Guid reviewerId, CancellationToken ct) =>
        PublishAsync([report.ReportedByUserId], reviewerId, NotificationType.ReportReviewed,
            lang => NotificationTexts.ReportReviewed(lang, report.Status == ReportStatus.Resolved),
            RelatedEntityType.Report, report.Id, report.GymId, boulderId == Guid.Empty ? "/notifications" : $"/boulders/{boulderId}", null, ct);

    private async Task<List<Guid>> BoulderFollowersAsync(Guid boulderId, CancellationToken ct) =>
        await db.BoulderFollows.Where(f => f.BoulderId == boulderId && f.NotificationsEnabled).Select(f => f.UserId).ToListAsync(ct);

    /// <summary>
    /// Writes one notification per recipient, in that recipient's language: the text factory is called once per
    /// language present among them.
    /// </summary>
    private async Task PublishAsync(IEnumerable<Guid> audience, Guid? actorId, NotificationType type,
        Func<string, NotificationText> text,
        RelatedEntityType relatedType, Guid relatedId, Guid? gymId, string link, string? collapseKey, CancellationToken ct,
        Func<string, int, (NotificationText Text, string? Link, RelatedEntityType? RelatedType, Guid? RelatedId)>? collapsed = null)
    {
        var recipients = audience.Where(u => u != actorId).Distinct().ToList();
        if (recipients.Count == 0) return;

        var category = Notification.CategoryOf(type);
        var settings = await db.NotificationSettings.AsNoTracking().Where(s => recipients.Contains(s.UserId)).ToListAsync(ct);
        var muted = settings.Where(s => !s.Allows(category)).Select(s => s.UserId).ToHashSet();
        recipients.RemoveAll(muted.Contains);
        if (recipients.Count == 0) return;

        var languages = await db.Users.AsNoTracking().Where(u => recipients.Contains(u.Id))
            .Select(u => new { u.Id, u.Language }).ToDictionaryAsync(u => u.Id, u => u.Language, ct);

        var now = clock.UtcNow;
        var open = collapseKey is null
            ? []
            : await db.Notifications.Where(n => n.CollapseKey == collapseKey && n.ReadAt == null && recipients.Contains(n.UserId)).ToDictionaryAsync(n => n.UserId, ct);

        var rendered = new Dictionary<string, NotificationText>();
        foreach (var userId in recipients)
        {
            var language = Language.Normalize(languages.GetValueOrDefault(userId));
            if (open.TryGetValue(userId, out var existing))
            {
                var c = collapsed?.Invoke(language, existing.Count + 1);
                var folded = c?.Text ?? Render(language);
                existing.Collapse(folded.Title, folded.Body, now, c?.Link, c?.RelatedType, c?.RelatedId);
            }
            else
            {
                var fresh = Render(language);
                db.Notifications.Add(Notification.Create(userId, type, fresh.Title, fresh.Body, relatedType, relatedId, gymId, link, collapseKey, now));
            }
        }

        NotificationText Render(string language)
        {
            if (!rendered.TryGetValue(language, out var value)) rendered[language] = value = text(language);
            return value;
        }
    }

    private static string Excerpt(string text) => text.Length <= 140 ? text : text[..139] + "…";
}
