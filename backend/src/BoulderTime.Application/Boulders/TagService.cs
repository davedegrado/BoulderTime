using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Boulders;

/// <summary>A tag in the gym's list, with how many boulders on the wall carry it (staff see it before deleting).</summary>
public sealed record GymTagDto(Guid Id, string Name, int Boulders);

public sealed record SaveTagRequest(string? Name);

/// <summary>
/// Each gym's own tags. Anyone can read them (they label public boulders); staff create, rename and delete them,
/// only in their own gym. Names are unique per gym ignoring case, accents and spacing (ADR-046).
/// </summary>
public sealed class TagService(IAppDbContext db, GymAccess access, IClock clock)
{
    public async Task<IReadOnlyList<GymTagDto>> ListAsync(Guid gymId, CancellationToken ct = default)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (!gym.IsPubliclyVisible && await access.GetRoleAsync(gymId, ct) is null) throw new NotFoundException("Gym", gymId);
        var tags = await db.GymTags.AsNoTracking().Where(t => t.GymId == gymId).ToListAsync(ct);
        var counts = await db.BoulderTags.AsNoTracking()
            .Join(db.Boulders.Where(b => b.GymId == gymId && b.Status == BoulderStatus.Active), t => t.BoulderId, b => b.Id, (t, b) => t.TagId)
            .GroupBy(id => id).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return tags.OrderBy(t => t.NormalizedName, StringComparer.Ordinal)
            .Select(t => new GymTagDto(t.Id, t.Name, counts.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<GymTagDto> CreateAsync(Guid gymId, SaveTagRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var name = ValidName(r.Name);
        if (await db.GymTags.CountAsync(t => t.GymId == gymId, ct) >= GymTag.MaxPerGym)
            throw new ValidationException("name", $"A gym can have at most {GymTag.MaxPerGym} tags.");
        await RefuseDuplicateAsync(gymId, name, exceptId: null, ct);
        var tag = GymTag.Create(gymId, name, clock.UtcNow);
        db.GymTags.Add(tag);
        await db.SaveChangesAsync(ct);
        return new GymTagDto(tag.Id, tag.Name, 0);
    }

    public async Task<GymTagDto> RenameAsync(Guid tagId, SaveTagRequest r, CancellationToken ct = default)
    {
        var tag = await db.GymTags.FirstOrDefaultAsync(t => t.Id == tagId, ct) ?? throw new NotFoundException("Tag", tagId);
        await access.RequireRoleAsync(tag.GymId, GymRole.Staff, ct);
        var name = ValidName(r.Name);
        await RefuseDuplicateAsync(tag.GymId, name, tag.Id, ct);
        tag.Rename(name);
        await db.SaveChangesAsync(ct);
        return (await ListAsync(tag.GymId, ct)).First(t => t.Id == tag.Id);
    }

    /// <summary>Deletes the tag and takes it off every boulder (the link rows go with it).</summary>
    public async Task DeleteAsync(Guid tagId, CancellationToken ct = default)
    {
        var tag = await db.GymTags.FirstOrDefaultAsync(t => t.Id == tagId, ct) ?? throw new NotFoundException("Tag", tagId);
        await access.RequireRoleAsync(tag.GymId, GymRole.Staff, ct);
        db.BoulderTags.RemoveRange(await db.BoulderTags.Where(t => t.TagId == tagId).ToListAsync(ct));
        db.GymTags.Remove(tag);
        await db.SaveChangesAsync(ct);
    }

    private static string ValidName(string? raw)
    {
        var name = GymTag.Clean(raw ?? "");
        new Validator()
            .Check(name.Length > 0, "name", "Give the tag a name.")
            .Check(name.Length <= GymTag.NameMaxLength, "name", $"Keep tag names under {GymTag.NameMaxLength + 1} characters.")
            .ThrowIfInvalid();
        return name;
    }

    private async Task RefuseDuplicateAsync(Guid gymId, string name, Guid? exceptId, CancellationToken ct)
    {
        var normalized = GymTag.Normalize(name);
        var existing = await db.GymTags.AsNoTracking().FirstOrDefaultAsync(t => t.GymId == gymId && t.NormalizedName == normalized && t.Id != exceptId, ct);
        if (existing is not null) throw new ValidationException("name", $"There is already a tag called “{existing.Name}”.");
    }
}
