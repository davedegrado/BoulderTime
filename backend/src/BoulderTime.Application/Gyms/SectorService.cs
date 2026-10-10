using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Gyms;

public sealed class SectorService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Public: active sectors of an active gym. Staff also see inactive sectors (and non-public gyms).</summary>
    public async Task<IReadOnlyList<SectorDto>> ListAsync(Guid gymId, CancellationToken ct = default)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        var isStaff = await access.GetRoleAsync(gymId, ct) is not null;
        if (!gym.IsPubliclyVisible && !isStaff) throw new NotFoundException("Gym", gymId);

        var q = db.Sectors.AsNoTracking().Where(s => s.GymId == gymId);
        if (!isStaff) q = q.Where(s => s.IsActive);
        var sectors = await q.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync(ct);
        var followed = new HashSet<Guid>();
        if (currentUser.UserId is { } uid)
        {
            var ids = sectors.Select(s => s.Id).ToList();
            followed = (await db.SectorFollows.AsNoTracking().Where(f => f.UserId == uid && ids.Contains(f.SectorId)).Select(f => f.SectorId).ToListAsync(ct)).ToHashSet();
        }
        // What the floor plan and the sector sheet say: boulders on the wall, and how many of them are new this week.
        var weekAgo = clock.UtcNow.AddDays(-7);
        var counts = await db.Boulders.AsNoTracking()
            .Where(b => b.GymId == gymId && b.Status == BoulderStatus.Active)
            .GroupBy(b => b.SectorId)
            .Select(g => new { SectorId = g.Key, Active = g.Count(), New = g.Count(b => b.CreatedAt >= weekAgo) })
            .ToDictionaryAsync(x => x.SectorId, ct);
        return sectors.Select(s => SectorDto.From(s, followed.Contains(s.Id),
            counts.TryGetValue(s.Id, out var c) ? c.Active : 0, c?.New ?? 0)).ToList();
    }

    /// <summary>Draws a sector on the gym's floor plan, or takes it off with no points.</summary>
    public async Task<SectorDto> SetZoneAsync(Guid sectorId, SetSectorZoneRequest r, CancellationToken ct = default)
    {
        var sector = await db.Sectors.FirstOrDefaultAsync(s => s.Id == sectorId, ct) ?? throw new NotFoundException("Sector", sectorId);
        await access.RequireRoleAsync(sector.GymId, GymRole.Staff, ct);

        var points = r.Points ?? [];
        if (points.Count == 0)
        {
            sector.SetMapZone(null);
        }
        else
        {
            static bool Inside(MapPoint? p) => p is not null && double.IsFinite(p.X) && double.IsFinite(p.Y) && p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1;
            new Validator()
                .Check(points.Count is >= 3 and <= 64, "points", "Draw the sector with 3 to 64 points.")
                .Check(points.All(Inside), "points", "Keep every point on the floor plan.")
                .Check(r.Label is null || Inside(r.Label), "label", "Keep the label on the floor plan.")
                .ThrowIfInvalid();
            // Rounded: four decimals are a tenth of a millimetre on a 10 m wall, and keep the stored outline small.
            static MapPoint Round(MapPoint p) => new(Math.Round(p.X, 4), Math.Round(p.Y, 4));
            var outline = points.Select(Round).ToList();
            var label = r.Label is { } l ? Round(l) : new MapPoint(Math.Round(outline.Average(p => p.X), 4), Math.Round(outline.Average(p => p.Y), 4));
            sector.SetMapZone(new MapZoneDto(outline, label).ToJson());
        }
        await db.SaveChangesAsync(ct);
        return SectorDto.From(sector);
    }

    public async Task<SectorDto> CreateAsync(Guid gymId, CreateSectorRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        Validate(r.Name, r.Description);

        var nextOrder = await db.Sectors.Where(s => s.GymId == gymId).Select(s => (int?)s.SortOrder).MaxAsync(ct) ?? -1;
        var sector = Sector.Create(gymId, r.Name!, r.Description, nextOrder + 1);
        db.Sectors.Add(sector);
        await SaveUniqueNameAsync(ct);
        return SectorDto.From(sector);
    }

    public async Task<SectorDto> UpdateAsync(Guid sectorId, UpdateSectorRequest r, CancellationToken ct = default)
    {
        var sector = await db.Sectors.FirstOrDefaultAsync(s => s.Id == sectorId, ct) ?? throw new NotFoundException("Sector", sectorId);
        await access.RequireRoleAsync(sector.GymId, GymRole.Staff, ct);

        if (r.Name is not null || r.Description is not null)
        {
            var name = r.Name ?? sector.Name;
            var description = r.Description ?? sector.Description;
            Validate(name, description);
            sector.Update(name, description);
        }
        if (r.IsActive is { } active) sector.SetActive(active);

        await SaveUniqueNameAsync(ct);
        return SectorDto.From(sector);
    }

    /// <summary>Sets the display order. The request must list every sector of the gym exactly once.</summary>
    public async Task<IReadOnlyList<SectorDto>> ReorderAsync(Guid gymId, ReorderSectorsRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var sectors = await db.Sectors.Where(s => s.GymId == gymId).ToListAsync(ct);
        var ids = r.SectorIds ?? [];
        if (ids.Count != sectors.Count || ids.Distinct().Count() != ids.Count || !sectors.All(s => ids.Contains(s.Id)))
            throw new ValidationException("sectorIds", "List every sector of this gym exactly once.");

        for (var i = 0; i < ids.Count; i++) sectors.First(s => s.Id == ids[i]).MoveTo(i);
        await db.SaveChangesAsync(ct);
        return sectors.OrderBy(s => s.SortOrder).Select(s => SectorDto.From(s)).ToList();
    }

    private static void Validate(string? name, string? description) =>
        new Validator()
            .Check(Input.Trimmed(name).Length >= 1, "name", "Enter a sector name.")
            .Check(Input.MaxLength(name, Sector.NameMaxLength), "name", $"Keep it to {Sector.NameMaxLength} characters or fewer.")
            .Check(Input.MaxLength(description, Sector.DescriptionMaxLength), "description", $"Keep it to {Sector.DescriptionMaxLength} characters or fewer.")
            .ThrowIfInvalid();

    private async Task SaveUniqueNameAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException)
        {
            throw new ValidationException("name", "This gym already has a sector with that name.");
        }
    }
}
