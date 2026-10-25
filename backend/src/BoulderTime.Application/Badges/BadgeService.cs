using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Localization;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Badges;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Badges;

public sealed record BadgeGymDto(Guid Id, string Name, string Slug);
public sealed record BadgePeriodDto(BadgePeriodKind Kind, DateTimeOffset Start);

/// <param name="Id">This award: a title can be won more than once.</param>
/// <param name="BadgeId">The badge's stable id.</param>
/// <param name="Art">Key of the picture; the app shows a generic one for a key it has no picture for.</param>
public sealed record EarnedBadgeDto(Guid Id, string BadgeId, BadgeCategory Category, string Name, string Description, string Art,
    DateTimeOffset EarnedAt, BadgeGymDto? Gym, BadgePeriodDto? Period);

/// <param name="Featured">Ids of the awards in the foreground of the profile, in order; at most three.</param>
public sealed record UserBadgesDto(IReadOnlyList<Guid> Featured, IReadOnlyList<EarnedBadgeDto> Badges);

public sealed record SetFeaturedBadgesRequest(IReadOnlyList<Guid>? Ids);

/// <summary>
/// A person's badges, as their profile shows them (ADR-069): with the rest of the profile's content, so a private
/// profile shows them to its owner and friends only. Only the owner chooses which go in the foreground.
/// </summary>
public sealed class BadgeService(IAppDbContext db, ICurrentUser currentUser, ProfileAccess access, IClock clock)
{
    public async Task<UserBadgesDto> ForUserAsync(Guid userId, string language, CancellationToken ct = default)
    {
        var user = await access.RequireVisibleAsync(userId, ct);
        if (!await access.CanSeeContentAsync(user, ct)) return new UserBadgesDto([], []);
        return await ReadAsync(userId, Language.Normalize(language), ct);
    }

    public async Task<UserBadgesDto> SetFeaturedAsync(SetFeaturedBadgesRequest r, string language, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var ids = r.Ids ?? [];
        if (ids.Count > BadgeAward.MaxFeatured) throw new ValidationException("ids", "Choose up to 3 badges.");
        if (ids.Distinct().Count() != ids.Count) throw new ValidationException("ids", "Choose each badge once.");

        var mine = await db.BadgeAwards.Where(a => a.UserId == userId).ToListAsync(ct);
        var shown = mine.Where(a => BadgeCatalog.Find(a.BadgeId) is { Enabled: true }).ToDictionary(a => a.Id);
        if (ids.Any(id => !shown.ContainsKey(id))) throw new ValidationException("ids", "You can only choose badges you have earned.");
        foreach (var award in mine) award.Feature(null);
        for (var i = 0; i < ids.Count; i++) shown[ids[i]].Feature(i + 1);
        await db.SaveChangesAsync(ct);
        return await ReadAsync(userId, Language.Normalize(language), ct);
    }

    /// <summary>The caller's badges not shown to them yet, newest first: the app tells them once, then marks them seen.</summary>
    public async Task<IReadOnlyList<EarnedBadgeDto>> NewForMeAsync(string language, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var all = await ReadAsync(userId, Language.Normalize(language), ct);
        var unseen = (await db.BadgeAwards.AsNoTracking().Where(a => a.UserId == userId && a.SeenAt == null).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        return all.Badges.Where(b => unseen.Contains(b.Id)).ToList();
    }

    public async Task MarkSeenAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var unseen = await db.BadgeAwards.Where(a => a.UserId == userId && a.SeenAt == null).ToListAsync(ct);
        if (unseen.Count == 0) return;
        foreach (var award in unseen) award.MarkSeen(now);
        await db.SaveChangesAsync(ct);
    }

    private async Task<UserBadgesDto> ReadAsync(Guid userId, string language, CancellationToken ct)
    {
        var awards = await db.BadgeAwards.AsNoTracking().Where(a => a.UserId == userId).ToListAsync(ct);
        var gymIds = awards.Where(a => a.GymId != null).Select(a => a.GymId!.Value).Distinct().ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => new BadgeGymDto(g.Id, g.Name, g.Slug), ct);
        var order = BadgeCatalog.All.Select((b, i) => (b.Id, i)).ToDictionary(x => x.Id, x => x.i);

        var shown = awards
            .Select(a => (Award: a, Badge: BadgeCatalog.Find(a.BadgeId)))
            // A badge switched off, or unknown to this server, is not shown.
            .Where(x => x.Badge is { Enabled: true })
            .OrderBy(x => order[x.Badge!.Id]).ThenByDescending(x => x.Award.PeriodStart).ThenByDescending(x => x.Award.EarnedAt)
            .ToList();
        var badges = shown.Select(x => new EarnedBadgeDto(x.Award.Id, x.Badge!.Id, x.Badge.Category, x.Badge.Name(language), x.Badge.Description(language),
            x.Badge.Art, x.Award.EarnedAt, x.Award.GymId is { } g ? gyms.GetValueOrDefault(g) : null,
            x.Award is { PeriodKind: { } k, PeriodStart: { } s } ? new BadgePeriodDto(k, s) : null)).ToList();
        var featured = shown.Where(x => x.Award.FeaturedOrder != null).OrderBy(x => x.Award.FeaturedOrder).Select(x => x.Award.Id).ToList();
        return new UserBadgesDto(featured, badges);
    }
}
