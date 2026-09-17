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

public sealed record LeaderboardGradeDto(string Label, int Rank, string? ColorHex);

public sealed record LeaderboardEntryDto(
    int Position, PersonDto Climber, int Points, int Completed, LeaderboardGradeDto? Highest, bool IsViewer);

public sealed record LeaderboardDto(
    Guid GymId, LeaderboardMetric Metric, LeaderboardPeriod Period, DateTimeOffset? From,
    Guid? GradeSystemId, string? GradeSystemName, GradeSystemType? GradeSystemType, string ScoringExplanation,
    int Climbers, IReadOnlyList<LeaderboardEntryDto> Entries, LeaderboardEntryDto? Viewer);

/// <summary>
/// Per-gym leaderboards computed on read from completions (never stored). A completion counts in the period of its
/// completion date, including boulders removed since. Grades are only compared within the gym's scales.
/// </summary>
public sealed class LeaderboardService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock, IClimbScoring scoring)
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

        // Completions at this gym in the period, by public profiles (the viewer always sees themself).
        var viewerId = currentUser.UserId;
        var sends = await db.BoulderAttempts.AsNoTracking()
            .Where(a => a.Completed && (from == null || a.CompletedAt >= from))
            .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => new { a.UserId, a.BoulderId, a.CompletedAt, b.GymId })
            .Where(x => x.GymId == gymId)
            .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.UserId, x.BoulderId, x.CompletedAt, u.ProfileVisibility })
            .Where(x => x.ProfileVisibility == ProfileVisibility.Public || x.UserId == viewerId)
            .Select(x => new { x.UserId, x.BoulderId, x.CompletedAt })
            .ToListAsync(ct);

        // Scales of this gym: the primary system is the first active one; others are fallbacks for points only.
        var systems = await db.GradeSystems.AsNoTracking().Where(s => s.GymId == gymId && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var primary = systems.FirstOrDefault();
        var systemIds = systems.Select(s => s.Id).ToList();
        var scaleSizes = await db.GradeValues.AsNoTracking().Where(v => systemIds.Contains(v.GradeSystemId))
            .GroupBy(v => v.GradeSystemId).Select(g => new { g.Key, Size = g.Max(v => v.Rank) + 1 })
            .ToDictionaryAsync(x => x.Key, x => x.Size, ct);

        var boulderIds = sends.Select(s => s.BoulderId).Distinct().ToList();
        var grades = await db.BoulderGrades.AsNoTracking()
            .Where(g => boulderIds.Contains(g.BoulderId) && g.Source == GradeSource.Staff && systemIds.Contains(g.GradeSystemId))
            .Join(db.GradeValues, g => g.GradeValueId, v => v.Id, (g, v) => new { g.BoulderId, g.GradeSystemId, v.Label, v.Rank, v.ColorHex })
            .ToListAsync(ct);
        var order = systems.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var gradesByBoulder = grades.GroupBy(g => g.BoulderId).ToDictionary(g => g.Key, g => g.OrderBy(x => order[x.GradeSystemId]).ToList());

        var rows = sends.GroupBy(s => s.UserId).Select(g =>
        {
            var points = 0;
            LeaderboardGradeDto? highest = null;
            foreach (var send in g)
            {
                if (!gradesByBoulder.TryGetValue(send.BoulderId, out var list) || list.Count == 0) continue;
                var scored = list[0]; // primary system if graded in it, otherwise the next system of the gym
                points += scoring.PointsFor(new GradedSend(scored.Rank, scaleSizes.GetValueOrDefault(scored.GradeSystemId, scored.Rank + 1)));
                var inPrimary = primary is null ? null : list.FirstOrDefault(x => x.GradeSystemId == primary.Id);
                if (inPrimary is not null && (highest is null || inPrimary.Rank > highest.Rank))
                    highest = new LeaderboardGradeDto(inPrimary.Label, inPrimary.Rank, inPrimary.ColorHex);
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
        return new LeaderboardDto(gymId, m, p, from, primary?.Id, primary?.Name, primary?.Type, scoring.Describe(scoringSystem),
            sorted.Count, entries, viewer);
    }

    /// <remarks>Ties on the metric are ordered by who got there first (earlier last send).</remarks>
    private sealed record Row(Guid UserId, int Points, int Completed, LeaderboardGradeDto? Highest, DateTimeOffset LastSend);
}
