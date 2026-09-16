using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Gyms;

public sealed class SectorService(IAppDbContext db, GymAccess access)
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
        return sectors.Select(SectorDto.From).ToList();
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
        return sectors.OrderBy(s => s.SortOrder).Select(SectorDto.From).ToList();
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
