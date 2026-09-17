using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Boulders;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Boulders;

/// <summary>
/// Turns boulder entities into card DTOs (grades, gym and sector names, community rating, viewer progress) with a
/// fixed number of queries regardless of list size. Shared by boulder lists, climbing history and Home.
/// </summary>
public sealed class BoulderReader(IAppDbContext db, IObjectStorage storage, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<BoulderSummaryDto>> SummariesAsync(IReadOnlyList<Boulder> boulders, CancellationToken ct)
    {
        if (boulders.Count == 0) return [];
        var ids = boulders.Select(b => b.Id).ToList();
        var grades = await GradesAsync(ids, ct);
        var ratings = await RatingsAsync(ids, ct);
        var viewer = await ViewerAsync(ids, currentUser.UserId, ct);

        var sectorIds = boulders.Select(b => b.SectorId).Distinct().ToList();
        var sectors = await db.Sectors.AsNoTracking().Where(s => sectorIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var gymIds = boulders.Select(b => b.GymId).Distinct().ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        return boulders.Select(b => new BoulderSummaryDto(
            b.Id, b.GymId, gyms.GetValueOrDefault(b.GymId, ""), b.SectorId, sectors.GetValueOrDefault(b.SectorId, ""),
            storage.PublicUrl(StorageBuckets.BoulderImages, b.PhotoPath), b.HoldColor, grades.GetValueOrDefault(b.Id, []),
            b.Status, b.CreatedAt, b.RemovedAt, ratings.GetValueOrDefault(b.Id, NoRatings), viewer.GetValueOrDefault(b.Id)))
            .ToList();
    }

    public static readonly RatingSummaryDto NoRatings = new(null, 0);

    public async Task<Dictionary<Guid, IReadOnlyList<BoulderGradeDto>>> GradesAsync(List<Guid> boulderIds, CancellationToken ct)
    {
        if (boulderIds.Count == 0) return [];
        var rows = await db.BoulderGrades.AsNoTracking()
            .Where(g => boulderIds.Contains(g.BoulderId) && g.Source == GradeSource.Staff)
            .Join(db.GradeSystems, g => g.GradeSystemId, s => s.Id, (g, s) => new { g, s })
            .Join(db.GradeValues, x => x.g.GradeValueId, v => v.Id, (x, v) => new { x.g.BoulderId, System = x.s, Value = v })
            .ToListAsync(ct);
        return rows
            .GroupBy(x => x.BoulderId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<BoulderGradeDto>)g
                .OrderBy(x => x.System.SortOrder)
                .Select(x => new BoulderGradeDto(x.System.Id, x.System.Name, x.System.Type, x.Value.Id, x.Value.Label, x.Value.Rank, x.Value.ColorHex))
                .ToList());
    }

    public async Task<Dictionary<Guid, RatingSummaryDto>> RatingsAsync(List<Guid> boulderIds, CancellationToken ct)
    {
        if (boulderIds.Count == 0) return [];
        var rows = await db.BoulderRatings.AsNoTracking()
            .Where(r => boulderIds.Contains(r.BoulderId))
            .GroupBy(r => r.BoulderId)
            .Select(g => new { BoulderId = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.BoulderId, r => new RatingSummaryDto(Math.Round(r.Average, 1), r.Count));
    }

    public async Task<Dictionary<Guid, ViewerProgressDto>> ViewerAsync(List<Guid> boulderIds, Guid? userId, CancellationToken ct)
    {
        if (userId is not { } uid || boulderIds.Count == 0) return [];
        var attempts = await db.BoulderAttempts.AsNoTracking().Where(a => a.UserId == uid && boulderIds.Contains(a.BoulderId)).ToListAsync(ct);
        var ratings = await db.BoulderRatings.AsNoTracking().Where(r => r.UserId == uid && boulderIds.Contains(r.BoulderId))
            .ToDictionaryAsync(r => r.BoulderId, r => r.Rating, ct);
        return attempts.ToDictionary(a => a.BoulderId, a => new ViewerProgressDto(
            a.Attempts, a.Completed, a.CompletedAt, ratings.TryGetValue(a.BoulderId, out var r) ? r : (int?)null));
    }
}
