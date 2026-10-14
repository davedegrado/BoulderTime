using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Boulders;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Boulders;

/// <summary>
/// Turns boulder entities into card DTOs (grades, gym and sector names, community rating, viewer progress) with a
/// fixed number of queries regardless of list size. Shared by boulder lists, climbing history and Home.
/// </summary>
public sealed class BoulderReader(IAppDbContext db, IObjectStorage storage, ICurrentUser currentUser, Leaderboards.BoulderPoints points)
{
    public async Task<IReadOnlyList<BoulderSummaryDto>> SummariesAsync(IReadOnlyList<Boulder> boulders, CancellationToken ct)
    {
        if (boulders.Count == 0) return [];
        var ids = boulders.Select(b => b.Id).ToList();
        var grades = await GradesAsync(ids, ct);
        var ratings = await RatingsAsync(ids, ct);
        var viewer = await ViewerAsync(ids, currentUser.UserId, ct);
        var tags = await TagsAsync(ids, ct);
        var completions = await CompletionsAsync(ids, ct);

        var sectorIds = boulders.Select(b => b.SectorId).Distinct().ToList();
        var sectors = await db.Sectors.AsNoTracking().Where(s => sectorIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var gymIds = boulders.Select(b => b.GymId).Distinct().ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id)).Select(g => new { g.Id, g.Name, g.LogoUrl })
            .ToDictionaryAsync(g => g.Id, ct);
        var scales = await points.ForGymsAsync(gymIds, ct);

        return boulders.Select(b => new BoulderSummaryDto(
            b.Id, b.GymId, gyms.GetValueOrDefault(b.GymId)?.Name ?? "", b.SectorId, sectors.GetValueOrDefault(b.SectorId, ""),
            // A deleted boulder has no photo left to point at, and a url for an empty path would 404. One that lost its
            // sector's wall photo still has its own card picture.
            b.Status == BoulderStatus.Deleted ? null : CardPicture(b),
            b.HoldColor, grades.GetValueOrDefault(b.Id, []),
            b.Status, b.CreatedAt, b.RemovedAt, ratings.GetValueOrDefault(b.Id, NoRatings), viewer.GetValueOrDefault(b.Id),
            b.Intensity, tags.GetValueOrDefault(b.Id, []), completions.GetValueOrDefault(b.Id),
            b.Name, PointsOf(scales, b, grades.GetValueOrDefault(b.Id, [])), gyms.GetValueOrDefault(b.GymId)?.LogoUrl))
            .ToList();
    }

    private string? CardPicture(Boulder b) =>
        b.ThumbnailPath is { } thumbnail ? storage.PublicUrl(StorageBuckets.BoulderImages, thumbnail)
        : b.PhotoPath.Length > 0 ? storage.PublicUrl(StorageBuckets.BoulderImages, b.PhotoPath)
        : null;

    public static readonly RatingSummaryDto NoRatings = new(null, 0);

    /// <summary>The same number the leaderboard adds up for a send of this boulder.</summary>
    public static int? PointsOf(IReadOnlyDictionary<Guid, Leaderboards.GymScales> scales, Boulder b, IReadOnlyList<BoulderGradeDto> grades) =>
        scales.GetValueOrDefault(b.GymId, Leaderboards.GymScales.Empty).PointsOf(b.PointsOverride, grades.Select(g => (g.GradeSystemId, g.GradeValueId)));

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
                .Select(x => new BoulderGradeDto(x.System.Id, x.System.Name, x.System.Type, x.Value.Id, x.Value.Label, x.Value.Rank, x.Value.ColorHex, x.Value.ColorHex2))
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

    /// <summary>Each boulder's tags, alphabetical, for a whole page in one query.</summary>
    public async Task<Dictionary<Guid, IReadOnlyList<BoulderTagDto>>> TagsAsync(List<Guid> boulderIds, CancellationToken ct)
    {
        if (boulderIds.Count == 0) return [];
        var rows = await db.BoulderTags.AsNoTracking().Where(t => boulderIds.Contains(t.BoulderId))
            .Join(db.GymTags, bt => bt.TagId, t => t.Id, (bt, t) => new { bt.BoulderId, t.Id, t.Name, t.NormalizedName })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.BoulderId).ToDictionary(g => g.Key,
            g => (IReadOnlyList<BoulderTagDto>)g.OrderBy(r => r.NormalizedName, StringComparer.Ordinal).Select(r => new BoulderTagDto(r.Id, r.Name)).ToList());
    }

    /// <summary>
    /// How many people completed each boulder, for a whole page in one query. A person counts once — there is one
    /// progress row per person and boulder — and only while it is marked completed, so undoing a send takes it off.
    /// Suspended accounts don't count, as they count nowhere else.
    /// </summary>
    public async Task<Dictionary<Guid, int>> CompletionsAsync(List<Guid> boulderIds, CancellationToken ct)
    {
        if (boulderIds.Count == 0) return [];
        return await db.BoulderAttempts.AsNoTracking()
            .Where(a => boulderIds.Contains(a.BoulderId) && a.Completed)
            .Join(db.Users.Where(u => u.SuspendedAt == null), a => a.UserId, u => u.Id, (a, u) => a.BoulderId)
            .GroupBy(id => id).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
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
