using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Leaderboards;

public enum LeaderboardMetric { Points = 0, Completed = 1, Highest = 2 }
/// <remarks>Single-word names: query-string enum binding does not apply the SCREAMING_SNAKE JSON policy.</remarks>
public enum LeaderboardPeriod { Week = 0, Month = 1, Year = 2, All = 3 }

public sealed record LeaderboardGradeDto(string Label, int Rank, string? ColorHex, string? ColorHex2 = null);

/// <summary>
/// How points are worked out, as numbers rather than a sentence, so the app can say it in the reader's language.
/// <see cref="Customised"/>: the gym has set some grades' or boulders' points by hand.
/// </summary>
public sealed record ScoringRuleDto(int MinPoints, int MaxPoints, bool Customised);

public sealed record LeaderboardEntryDto(
    int Position, PersonDto Climber, int Points, int Completed, LeaderboardGradeDto? Highest, bool IsViewer);

public sealed record LeaderboardDto(
    Guid GymId, LeaderboardMetric Metric, LeaderboardPeriod Period, DateTimeOffset? From,
    Guid? GradeSystemId, string? GradeSystemName, GradeSystemType? GradeSystemType, string ScoringExplanation,
    int Climbers, IReadOnlyList<LeaderboardEntryDto> Entries, LeaderboardEntryDto? Viewer,
    /// <summary>The rule behind <see cref="ScoringExplanation"/>, which stays for the app builds that show that English sentence.</summary>
    ScoringRuleDto? Scoring = null);

/// <summary>
/// Per-gym leaderboards computed on read from completions (never stored). A completion counts in the period of its
/// completion date, including boulders removed since. Grades are only compared within the gym's scales.
/// </summary>
public sealed class LeaderboardService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock, IClimbScoring scoring, BoulderPoints boulderPoints)
{
    public const int DefaultLimit = 50;

    public static DateTimeOffset? PeriodStart(LeaderboardPeriod period, DateTimeOffset now)
    {
        var today = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        return period switch
        {
            LeaderboardPeriod.Week => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), // Monday, UTC
            LeaderboardPeriod.Month => new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero),
            LeaderboardPeriod.Year => new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero),
            _ => null,
        };
    }

    public async Task<LeaderboardDto> GetAsync(Guid gymId, LeaderboardMetric? metric, LeaderboardPeriod? period, int? limit, CancellationToken ct = default)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (!gym.IsPubliclyVisible && await access.GetRoleAsync(gymId, ct) is null) throw new NotFoundException("Gym", gymId);

        var m = metric ?? LeaderboardMetric.Points;
        var p = period ?? LeaderboardPeriod.Month;
        if (!Enum.IsDefined(m) || !Enum.IsDefined(p)) throw new ValidationException("metric", "Unknown leaderboard.");
        var take = Math.Clamp(limit ?? DefaultLimit, 1, 100);
        var from = PeriodStart(p, clock.UtcNow);

        // Completions at this gym in the period. Who appears is the leaderboard setting alone: a private profile still
        // appears if its owner chose so, showing only what a leaderboard row shows (ADR-061).
        var viewerId = currentUser.UserId;
        var sends = await db.BoulderAttempts.AsNoTracking()
            .Where(a => a.Completed && (from == null || a.CompletedAt >= from))
            // The gym's trial boulders count for nobody: neither points, nor completions, nor the highest grade.
            .Join(db.Boulders.Where(b => b.BetaTestAt == null), a => a.BoulderId, b => b.Id, (a, b) => new { a.UserId, a.BoulderId, a.CompletedAt, b.GymId })
            .Where(x => x.GymId == gymId)
            .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new
            {
                x.UserId, x.BoulderId, x.CompletedAt, u.LeaderboardOptOut, u.LeaderboardExcludedAt, u.SuspendedAt,
                u.DeletionRequestedAt,
            })
            // Hidden climbers are absent rather than shown with no points: a zero-point row still names them.
            // Accounts waiting to be erased leave the leaderboard the moment the person asks.
            .Where(x => !x.LeaderboardOptOut && x.LeaderboardExcludedAt == null && x.SuspendedAt == null && x.DeletionRequestedAt == null)
            .Select(x => new { x.UserId, x.BoulderId, x.CompletedAt })
            .ToListAsync(ct);

        // Scales of this gym: the primary system is the first active one; others are fallbacks for points only.
        var systems = await db.GradeSystems.AsNoTracking().Where(s => s.GymId == gymId && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var primary = systems.FirstOrDefault();
        var systemIds = systems.Select(s => s.Id).ToList();
        var scales = await boulderPoints.ForGymAsync(gymId, ct);

        var boulderIds = sends.Select(s => s.BoulderId).Distinct().ToList();
        var grades = await db.BoulderGrades.AsNoTracking()
            .Where(g => boulderIds.Contains(g.BoulderId) && g.Source == GradeSource.Staff && systemIds.Contains(g.GradeSystemId))
            .Join(db.GradeValues, g => g.GradeValueId, v => v.Id, (g, v) => new { g.BoulderId, g.GradeSystemId, g.GradeValueId, v.Label, v.Rank, v.ColorHex, v.ColorHex2 })
            .ToListAsync(ct);
        // Points staff set on single boulders win over their grade's.
        var overrides = await db.Boulders.AsNoTracking().Where(b => boulderIds.Contains(b.Id) && b.PointsOverride != null)
            .ToDictionaryAsync(b => b.Id, b => b.PointsOverride, ct);
        var order = systems.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var gradesByBoulder = grades.GroupBy(g => g.BoulderId).ToDictionary(g => g.Key, g => g.OrderBy(x => order[x.GradeSystemId]).ToList());

        var rows = sends.GroupBy(s => s.UserId).Select(g =>
        {
            var points = 0;
            LeaderboardGradeDto? highest = null;
            foreach (var send in g)
            {
                var list = gradesByBoulder.GetValueOrDefault(send.BoulderId) ?? [];
                // Primary system if graded in it, otherwise the next system of the gym; a boulder's own points first.
                points += scales.PointsOf(overrides.GetValueOrDefault(send.BoulderId), list.Select(x => (x.GradeSystemId, x.GradeValueId))) ?? 0;
                var inPrimary = primary is null ? null : list.FirstOrDefault(x => x.GradeSystemId == primary.Id);
                if (inPrimary is not null && (highest is null || inPrimary.Rank > highest.Rank))
                    highest = new LeaderboardGradeDto(inPrimary.Label, inPrimary.Rank, inPrimary.ColorHex, inPrimary.ColorHex2);
            }
            var lastSend = g.Max(x => x.CompletedAt) ?? DateTimeOffset.MinValue;
            return new Row(g.Key, points, g.Count(), highest, lastSend);
        }).ToList();

        if (m == LeaderboardMetric.Highest) rows = rows.Where(r => r.Highest is not null).ToList();

        // Primary key per metric, then secondary keys; ties on the primary value share a position.
        var sorted = (m switch
        {
            LeaderboardMetric.Completed => rows.OrderByDescending(r => r.Completed).ThenByDescending(r => r.Points),
            LeaderboardMetric.Highest => rows.OrderByDescending(r => r.Highest!.Rank).ThenByDescending(r => r.Points),
            _ => rows.OrderByDescending(r => r.Points).ThenByDescending(r => r.Completed),
        }).ThenBy(r => r.LastSend).ToList();
        int Key(Row r) => m switch { LeaderboardMetric.Completed => r.Completed, LeaderboardMetric.Highest => r.Highest!.Rank, _ => r.Points };

        var positions = new int[sorted.Count];
        for (var i = 0; i < sorted.Count; i++)
            positions[i] = i > 0 && Key(sorted[i]) == Key(sorted[i - 1]) ? positions[i - 1] : i + 1;

        var wanted = sorted.Take(take).Select(r => r.UserId).ToHashSet();
        var viewerIndex = viewerId is { } vid ? sorted.FindIndex(r => r.UserId == vid) : -1;
        if (viewerIndex >= 0) wanted.Add(sorted[viewerIndex].UserId);
        var people = await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl), ct);

        LeaderboardEntryDto ToEntry(int i) => new(positions[i], people[sorted[i].UserId], sorted[i].Points, sorted[i].Completed, sorted[i].Highest, sorted[i].UserId == viewerId);

        var entries = Enumerable.Range(0, Math.Min(take, sorted.Count)).Select(ToEntry).ToList();
        var viewer = viewerIndex >= 0 ? ToEntry(viewerIndex) : null;
        var scoringSystem = primary?.Name ?? "grade";
        var customised = await db.GradeValues.AsNoTracking().AnyAsync(v => systemIds.Contains(v.GradeSystemId) && v.PointsOverride != null, ct)
                         || await db.Boulders.AsNoTracking().AnyAsync(b => b.GymId == gymId && b.PointsOverride != null, ct);
        return new LeaderboardDto(gymId, m, p, from, primary?.Id, primary?.Name, primary?.Type, scoring.Describe(scoringSystem),
            sorted.Count, entries, viewer, new ScoringRuleDto(RankBasedScoring.MinPoints, RankBasedScoring.MaxPoints, customised));
    }

    /// <remarks>Ties on the metric are ordered by who got there first (earlier last send).</remarks>
    private sealed record Row(Guid UserId, int Points, int Completed, LeaderboardGradeDto? Highest, DateTimeOffset LastSend);
}
