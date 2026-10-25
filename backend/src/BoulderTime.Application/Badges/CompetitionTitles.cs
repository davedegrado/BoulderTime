using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Domain.Badges;
using BoulderTime.Domain.Gyms;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Badges;

/// <summary>
/// Hands out the titles of the gyms' leaderboards when a month or a year closes (ADR-069): champion of the month, of
/// the year, and the titles that count them. Only for gyms that launched, and only for what was climbed from their
/// launch on. Each period is settled once, with the points as they are when it is settled; the titles stay won.
/// </summary>
public sealed class CompetitionTitles(IAppDbContext db, IClock clock, LeaderboardService leaderboards, BadgeAwarder awarder)
{
    /// <summary>Settles every period that closed and wasn't settled yet. Returns the titles handed out.</summary>
    public async Task<int> SettleDueAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var gyms = await db.Gyms.AsNoTracking().Where(g => g.LaunchedAt != null && g.Status == GymStatus.Active)
            .Select(g => new { g.Id, LaunchedAt = g.LaunchedAt!.Value }).ToListAsync(ct);
        var titles = 0;
        foreach (var gym in gyms)
        {
            var settled = (await db.CompetitionPeriods.AsNoTracking().Where(p => p.GymId == gym.Id).Select(p => new { p.Kind, p.Start }).ToListAsync(ct))
                .Select(p => (p.Kind, p.Start)).ToHashSet();
            foreach (var kind in new[] { BadgePeriodKind.Month, BadgePeriodKind.Year })
            {
                for (var start = PeriodOf(kind, gym.LaunchedAt); End(kind, start) <= now; start = End(kind, start))
                {
                    if (settled.Contains((kind, start))) continue;
                    titles += await SettleAsync(gym.Id, gym.LaunchedAt, kind, start, ct);
                }
            }
        }
        return titles;
    }

    private async Task<int> SettleAsync(Guid gymId, DateTimeOffset launchedAt, BadgePeriodKind kind, DateTimeOffset start, CancellationToken ct)
    {
        var from = start > launchedAt ? start : launchedAt;
        var champions = await leaderboards.ChampionsAsync(gymId, from, End(kind, start), ct);
        var badgeId = kind == BadgePeriodKind.Month ? "COMPETITION_MONTH_CHAMPION" : "COMPETITION_YEAR_CHAMPION";
        var scope = $"{gymId:N}:{(kind == BadgePeriodKind.Month ? start.ToString("yyyy-MM") : start.ToString("yyyy"))}";
        var now = clock.UtcNow;

        var already = await db.BadgeAwards.AsNoTracking().Where(a => a.BadgeId == badgeId && a.ScopeKey == scope).Select(a => a.UserId).ToListAsync(ct);
        var earned = champions.Except(already).Select(id => BadgeAward.Earn(id, badgeId, now, scope, gymId, kind, start)).ToList();
        db.BadgeAwards.AddRange(earned);
        db.CompetitionPeriods.Add(CompetitionPeriod.Settle(gymId, kind, start, now, champions.Count));
        await db.SaveChangesAsync(ct);

        // The titles that count titles at this gym.
        var counted = new List<BadgeAward>();
        var rule = kind == BadgePeriodKind.Month ? BadgeRule.MonthTitlesAtGym : BadgeRule.YearTitlesAtGym;
        foreach (var userId in champions)
        {
            var won = await db.BadgeAwards.CountAsync(a => a.UserId == userId && a.BadgeId == badgeId && a.GymId == gymId, ct);
            var owned = await db.BadgeAwards.Where(a => a.UserId == userId && a.ScopeKey == $"{gymId:N}").Select(a => a.BadgeId).ToListAsync(ct);
            counted.AddRange(BadgeCatalog.Live(rule).Where(b => won >= b.Threshold && !owned.Contains(b.Id))
                .Select(b => BadgeAward.Earn(userId, b.Id, now, $"{gymId:N}", gymId)));
        }
        if (counted.Count > 0)
        {
            db.BadgeAwards.AddRange(counted);
            await db.SaveChangesAsync(ct);
        }
        await awarder.NotifyAsync([.. earned, .. counted], ct);
        return earned.Count + counted.Count;
    }

    /// <summary>The month or year that holds the instant, as the leaderboards count them (UTC).</summary>
    public static DateTimeOffset PeriodOf(BadgePeriodKind kind, DateTimeOffset at) =>
        kind == BadgePeriodKind.Month
            ? new DateTimeOffset(at.UtcDateTime.Year, at.UtcDateTime.Month, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(at.UtcDateTime.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset End(BadgePeriodKind kind, DateTimeOffset start) =>
        kind == BadgePeriodKind.Month ? start.AddMonths(1) : start.AddYears(1);
}
