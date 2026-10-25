using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Localization;
using BoulderTime.Domain.Badges;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Badges;

/// <summary>
/// Awards badges (ADR-069). Runs away from the requests (see <see cref="IBadgeQueue"/>), and every award is idempotent:
/// a badge already earned is never earned again, one row per person, badge and scope, guarded by a unique index too.
/// Badges are never taken back: what earned them can change afterwards, the badge stays.
/// </summary>
public sealed class BadgeAwarder(IAppDbContext db, IClock clock, IPushQueue push, IBadgeSettings settings)
{
    /// <summary>Only apps that know badges are told about one; a 1.0 app wouldn't have where to show it.</summary>
    public const string MinimumAppVersion = "1.1.0";

    /// <summary>A day of climbing for the person, in Italian time.</summary>
    public async Task RecordActivityAsync(Guid userId, DateTimeOffset at, CancellationToken ct = default)
    {
        var day = ItalianTime.Day(at);
        if (await db.ActivityDays.AnyAsync(d => d.UserId == userId && d.Day == day, ct)) return;
        db.ActivityDays.Add(ActivityDay.Of(userId, day));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Awards the person the badges their counts have reached and they don't have yet.
    /// </summary>
    /// <param name="dated">
    /// For the awards of the past (<c>backfill-badges</c>): earned on the day the count was reached, where the data says
    /// it; otherwise, and always for live awards, now.
    /// </param>
    /// <param name="notify">Tell the person on their phone; not for the awards of the past.</param>
    public async Task<IReadOnlyList<BadgeAward>> EvaluateAsync(Guid userId, bool dated = false, bool notify = true, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        // Suspended accounts and accounts on their way out earn nothing.
        if (user is null || user.SuspendedAt is not null || user.DeletionRequestedAt is not null) return [];

        var owned = (await db.BadgeAwards.AsNoTracking().Where(a => a.UserId == userId && a.ScopeKey == "").Select(a => a.BadgeId).ToListAsync(ct)).ToHashSet();
        var now = clock.UtcNow;
        var earned = new List<BadgeAward>();

        async Task Reach(BadgeRule rule, Func<Task<List<DateTimeOffset?>>> milestones)
        {
            var due = BadgeCatalog.Live(rule).Where(b => !owned.Contains(b.Id)).ToList();
            if (due.Count == 0) return;
            // One entry per counted thing, in the order they happened (null where the data has no date).
            var list = await milestones();
            foreach (var badge in due.Where(b => list.Count >= b.Threshold))
                earned.Add(BadgeAward.Earn(userId, badge.Id, dated ? list[badge.Threshold - 1] ?? now : now));
        }

        var attempts = db.BoulderAttempts.AsNoTracking().Where(a => a.UserId == userId);
        await Reach(BadgeRule.Completions, async () =>
            (await attempts.Where(a => a.Completed).Select(a => a.CompletedAt).ToListAsync(ct)).OrderBy(d => d ?? DateTimeOffset.MaxValue).ToList());
        await Reach(BadgeRule.Attempts, async () =>
        {
            // No dates for attempts, and only the count matters: no longer than the highest threshold.
            var total = await attempts.SumAsync(a => a.Attempts, ct);
            return Enumerable.Repeat<DateTimeOffset?>(null, Math.Min(total, BadgeCatalog.Live(BadgeRule.Attempts).Max(b => b.Threshold))).ToList();
        });
        await Reach(BadgeRule.ActiveDays, async () =>
            (await db.ActivityDays.AsNoTracking().Where(d => d.UserId == userId).Select(d => d.Day).OrderBy(d => d).ToListAsync(ct))
                .Select(d => (DateTimeOffset?)ItalianTime.Noon(d)).ToList());
        await Reach(BadgeRule.Gyms, async () =>
            (await attempts.Where(a => a.Completed)
                .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => new { a.CompletedAt, b.GymId })
                .Join(db.Gyms.Where(g => g.Status == GymStatus.Active), x => x.GymId, g => g.Id, (x, g) => x)
                .GroupBy(x => x.GymId).Select(g => g.Min(x => x.CompletedAt)).ToListAsync(ct))
                .OrderBy(d => d ?? DateTimeOffset.MaxValue).ToList());
        await Reach(BadgeRule.ProjectClosed, async () =>
            (await db.BoulderProjects.AsNoTracking().Where(p => p.UserId == userId)
                .Join(attempts.Where(a => a.Completed), p => p.BoulderId, a => a.BoulderId, (p, a) => new { p.CreatedAt, a.CompletedAt })
                .Where(x => x.CompletedAt >= x.CreatedAt).Select(x => x.CompletedAt).ToListAsync(ct))
                .OrderBy(d => d).ToList());
        await Reach(BadgeRule.Comments, async () =>
            (await db.Comments.AsNoTracking().Where(c => c.UserId == userId && c.Status == CommentStatus.Visible)
                .Select(c => (DateTimeOffset?)c.CreatedAt).ToListAsync(ct)).OrderBy(d => d).ToList());
        await Reach(BadgeRule.GradeVotes, async () =>
            (await db.GradeSuggestions.AsNoTracking().Where(s => s.UserId == userId)
                .GroupBy(s => s.BoulderId).Select(g => (DateTimeOffset?)g.Min(s => s.CreatedAt)).ToListAsync(ct)).OrderBy(d => d).ToList());

        if (earned.Count == 0) return [];
        db.BadgeAwards.AddRange(earned);
        await db.SaveChangesAsync(ct);
        if (notify) await NotifyAsync(earned, ct);
        return earned;
    }

    /// <summary>
    /// The beta testers of a gym whose beta test just closed: whoever recorded an attempt or a send on any of its
    /// boulders — on the wall, taken down or deleted. Once per person, whatever the gym.
    /// </summary>
    public async Task<int> AwardBetaTestersAsync(Guid gymId, CancellationToken ct = default)
    {
        var badge = BadgeCatalog.Live(BadgeRule.BetaTester).FirstOrDefault();
        if (badge is null) return 0;
        var testers = await db.BoulderAttempts.AsNoTracking()
            .Join(db.Boulders.Where(b => b.GymId == gymId), a => a.BoulderId, b => b.Id, (a, b) => a.UserId)
            .Distinct()
            .Join(db.Users.Where(u => u.SuspendedAt == null && u.DeletionRequestedAt == null), id => id, u => u.Id, (id, u) => id)
            .Where(id => !db.BadgeAwards.Any(x => x.UserId == id && x.BadgeId == badge.Id && x.ScopeKey == ""))
            .ToListAsync(ct);
        if (testers.Count == 0) return 0;
        var now = clock.UtcNow;
        var earned = testers.Select(id => BadgeAward.Earn(id, badge.Id, now)).ToList();
        db.BadgeAwards.AddRange(earned);
        await db.SaveChangesAsync(ct);
        await NotifyAsync(earned, ct);
        return earned.Count;
    }

    /// <summary>A badge given by hand (the founder). False if the person already has it.</summary>
    public async Task<bool> GrantAsync(Guid userId, string badgeId, CancellationToken ct = default)
    {
        var badge = BadgeCatalog.Find(badgeId);
        if (badge is not { Rule: BadgeRule.Manual }) throw new ArgumentException($"{badgeId} is not a badge given by hand.", nameof(badgeId));
        if (await db.BadgeAwards.AnyAsync(a => a.UserId == userId && a.BadgeId == badgeId && a.ScopeKey == "", ct)) return false;
        var award = BadgeAward.Earn(userId, badgeId, clock.UtcNow);
        db.BadgeAwards.Add(award);
        await db.SaveChangesAsync(ct);
        await NotifyAsync([award], ct);
        return true;
    }

    /// <summary>
    /// A phone notification per new badge, to the apps that can show it, in the person's language; nobody who turned
    /// off the notifications about their own things. The badge waits in the app anyway.
    /// </summary>
    internal async Task NotifyAsync(IReadOnlyList<BadgeAward> awards, CancellationToken ct)
    {
        if (!settings.PushEnabled || awards.Count == 0) return;
        var userIds = awards.Select(a => a.UserId).Distinct().ToList();
        var muted = (await db.NotificationSettings.AsNoTracking().Where(s => userIds.Contains(s.UserId)).ToListAsync(ct))
            .Where(s => !s.Allows(NotificationCategory.MyContent)).Select(s => s.UserId).ToHashSet();
        var languages = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Language, ct);
        foreach (var award in awards)
        {
            if (muted.Contains(award.UserId) || BadgeCatalog.Find(award.BadgeId) is not { } badge) continue;
            var language = Language.Normalize(languages.GetValueOrDefault(award.UserId));
            var title = language == Language.English ? $"New badge: {badge.NameEn}" : $"Nuovo badge: {badge.NameIt}";
            // The profile, which a 1.0 web app opens too; a 1.1 app shows the badges there.
            push.Enqueue(award.UserId, new PushMessage(title, badge.Description(language), $"/users/{award.UserId}", $"badge:{award.Id}", MinimumAppVersion));
        }
    }
}
