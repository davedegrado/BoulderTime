namespace BoulderTime.Domain.Badges;

public enum BadgePeriodKind { Month = 0, Year = 1 }

/// <summary>
/// A badge a person earned (ADR-069). Kept once earned: what made it (a count, a leaderboard) can change afterwards,
/// the badge does not. <see cref="ScopeKey"/> tells apart the awards of one badge that can be won more than once —
/// the champion of another month, of another gym — and is empty for the badges a person earns once; one row per
/// (person, badge, scope), so earning twice the same thing is impossible.
/// </summary>
public class BadgeAward
{
    public const int BadgeIdMaxLength = 64;
    public const int ScopeKeyMaxLength = 100;
    public const int MaxFeatured = 3;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>The badge's stable id from the catalog, e.g. <c>PROGRESSION_COMPLETIONS_10</c>.</summary>
    public string BadgeId { get; private set; } = string.Empty;
    public string ScopeKey { get; private set; } = string.Empty;
    /// <summary>The gym of a title won there.</summary>
    public Guid? GymId { get; private set; }
    public BadgePeriodKind? PeriodKind { get; private set; }
    /// <summary>First instant of the month or year of a title, UTC like the leaderboards.</summary>
    public DateTimeOffset? PeriodStart { get; private set; }
    public DateTimeOffset EarnedAt { get; private set; }
    /// <summary>When the person was shown it as new in the app; null until then.</summary>
    public DateTimeOffset? SeenAt { get; private set; }
    /// <summary>1 to 3 when the person put it in the foreground of their profile, in that order.</summary>
    public int? FeaturedOrder { get; private set; }

    private BadgeAward() { }

    public static BadgeAward Earn(Guid userId, string badgeId, DateTimeOffset earnedAt, string scopeKey = "",
        Guid? gymId = null, BadgePeriodKind? periodKind = null, DateTimeOffset? periodStart = null) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, BadgeId = badgeId, ScopeKey = scopeKey, GymId = gymId,
        PeriodKind = periodKind, PeriodStart = periodStart, EarnedAt = earnedAt,
    };

    public void MarkSeen(DateTimeOffset now) => SeenAt ??= now;

    public void Feature(int? order) => FeaturedOrder = order;
}

/// <summary>A day, in Italian time, on which the person did something with their climbing (ADR-069).</summary>
public class ActivityDay
{
    public Guid UserId { get; private set; }
    public DateOnly Day { get; private set; }

    private ActivityDay() { }

    public static ActivityDay Of(Guid userId, DateOnly day) => new() { UserId = userId, Day = day };
}

/// <summary>
/// A gym's month or year whose titles were handed out. Settled once: points changed later don't move a title already
/// won, and a period is never settled twice.
/// </summary>
public class CompetitionPeriod
{
    public Guid GymId { get; private set; }
    public BadgePeriodKind Kind { get; private set; }
    public DateTimeOffset Start { get; private set; }
    public DateTimeOffset SettledAt { get; private set; }
    public int Champions { get; private set; }

    private CompetitionPeriod() { }

    public static CompetitionPeriod Settle(Guid gymId, BadgePeriodKind kind, DateTimeOffset start, DateTimeOffset now, int champions) =>
        new() { GymId = gymId, Kind = kind, Start = start, SettledAt = now, Champions = champions };
}
