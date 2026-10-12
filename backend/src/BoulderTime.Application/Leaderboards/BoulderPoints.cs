using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Grading;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Leaderboards;

/// <summary>What a grade is worth: the automatic points, and what it actually scores once staff have had their say.</summary>
public readonly record struct GradePoints(int Automatic, int Effective);

/// <summary>
/// A gym's scales as the points see them: its active grading systems in order (the first is the primary one, the
/// others are fallbacks for boulders not graded in it) and what every grade of them is worth.
/// </summary>
public sealed class GymScales(IReadOnlyList<Guid> systemOrder, IReadOnlyDictionary<Guid, GradePoints> values)
{
    public static readonly GymScales Empty = new([], new Dictionary<Guid, GradePoints>());

    public IReadOnlyList<Guid> SystemOrder { get; } = systemOrder;
    public IReadOnlyDictionary<Guid, GradePoints> Values { get; } = values;

    /// <summary>
    /// What one boulder scores: the points staff set on it, otherwise the points of its grade in the first of the gym's
    /// scales it is graded in. Null when it has neither — an ungraded boulder counts as a send but scores nothing.
    /// </summary>
    public int? PointsOf(int? boulderOverride, IEnumerable<(Guid SystemId, Guid ValueId)> grades)
    {
        if (boulderOverride is { } fixedPoints) return fixedPoints;
        var bySystem = grades.GroupBy(g => g.SystemId).ToDictionary(g => g.Key, g => g.First().ValueId);
        foreach (var systemId in SystemOrder)
            if (bySystem.TryGetValue(systemId, out var valueId) && Values.TryGetValue(valueId, out var points)) return points.Effective;
        return null;
    }
}

/// <summary>
/// The one place that decides what a send is worth, so the leaderboard and the "+64 pt" on a card can never disagree.
///
/// The automatic points spread the scale's ACTIVE grades evenly from 10 to 100 (<see cref="IClimbScoring"/>). Staff can
/// set any grade's points by hand, and any single boulder's. A retired grade keeps what it was worth on the day it was
/// retired, so taking a grade out of the scale changes nobody's points but the ones staff choose to change.
///
/// Nothing is stored per send: points are worked out when they are read, so changing a value changes every
/// leaderboard at once, past periods included (the policy chosen in October 2026).
/// </summary>
public sealed class BoulderPoints(IAppDbContext db, IClimbScoring scoring)
{
    /// <summary>The worth of every grade of one system, given all its values (active and retired).</summary>
    public static Dictionary<Guid, GradePoints> ForScale(IReadOnlyCollection<GradeValue> values, IClimbScoring scoring)
    {
        var result = new Dictionary<Guid, GradePoints>();
        var active = values.Where(v => v.IsActive).OrderBy(v => v.Rank).ToList();
        for (var i = 0; i < active.Count; i++)
        {
            var automatic = scoring.PointsFor(new GradedSend(i, active.Count));
            result[active[i].Id] = new GradePoints(automatic, active[i].PointsOverride ?? automatic);
        }
        // Grades retired before their points were kept are valued the way they always were: by their place on the whole
        // list, retired ones included.
        var legacySize = values.Count == 0 ? 0 : values.Max(v => v.Rank) + 1;
        foreach (var v in values.Where(v => !v.IsActive))
        {
            var automatic = v.RetiredPoints ?? scoring.PointsFor(new GradedSend(v.Rank, legacySize));
            result[v.Id] = new GradePoints(automatic, v.PointsOverride ?? automatic);
        }
        return result;
    }

    public async Task<GymScales> ForGymAsync(Guid gymId, CancellationToken ct) =>
        (await ForGymsAsync([gymId], ct)).GetValueOrDefault(gymId, GymScales.Empty);

    public async Task<Dictionary<Guid, GymScales>> ForGymsAsync(IReadOnlyCollection<Guid> gymIds, CancellationToken ct)
    {
        if (gymIds.Count == 0) return [];
        var ids = gymIds.Select(id => (Guid?)id).Distinct().ToList();
        var systems = await db.GradeSystems.AsNoTracking()
            .Where(s => ids.Contains(s.GymId) && s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new { s.Id, GymId = s.GymId!.Value })
            .ToListAsync(ct);
        var systemIds = systems.Select(s => s.Id).ToList();
        var values = await db.GradeValues.AsNoTracking().Where(v => systemIds.Contains(v.GradeSystemId)).ToListAsync(ct);
        var bySystem = values.GroupBy(v => v.GradeSystemId).ToDictionary(g => g.Key, g => g.ToList());

        return systems.GroupBy(s => s.GymId).ToDictionary(g => g.Key, g =>
        {
            var points = new Dictionary<Guid, GradePoints>();
            foreach (var s in g)
                foreach (var (id, p) in ForScale(bySystem.GetValueOrDefault(s.Id, []), scoring)) points[id] = p;
            return new GymScales(g.Select(s => s.Id).ToList(), points);
        });
    }
}
