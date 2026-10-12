using System.Text.RegularExpressions;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Grading;

/// <summary>
/// Gym grading configuration. Viewing: public for visible gyms (active systems and values only), staff see everything.
/// Changing systems is ADMIN+ because it is gym-wide policy; any STAFF member can then use the systems on boulders.
/// </summary>
public sealed partial class GradingService(IAppDbContext db, GymAccess access, Leaderboards.IClimbScoring scoring)
{
    public const int MaxValuesPerSystem = 40;

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColor();

    public async Task<IReadOnlyList<GradeSystemDto>> ListAsync(Guid gymId, CancellationToken ct = default)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        var isStaff = await access.GetRoleAsync(gymId, ct) is not null;
        if (!gym.IsPubliclyVisible && !isStaff) throw new NotFoundException("Gym", gymId);
        return await LoadAsync(gymId, includeInactive: isStaff, ct);
    }

    public async Task<GradeSystemDto> CreateAsync(Guid gymId, CreateGradeSystemRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        if (r.Type is not { } type || !Enum.IsDefined(type)) throw new ValidationException("type", "Choose a grading type.");

        var name = Input.Trimmed(r.Name);
        if (name.Length == 0) name = GradePresets.DefaultName(type);
        ValidateName(name);

        var inputs = r.Values is { Count: > 0 }
            ? r.Values.ToList()
            : GradePresets.For(type).Select(v => new GradeValueInput(null, v.Label, v.Hex)).ToList();
        ValidateValues(type, inputs);
        var values = inputs.Select(v => (Label: Input.Trimmed(v.Label), Hex: v.ColorHex)).ToList();

        var order = await db.GradeSystems.Where(s => s.GymId == gymId).Select(s => (int?)s.SortOrder).MaxAsync(ct) ?? -1;
        var system = GradeSystem.Create(gymId, name, type, order + 1);
        db.GradeSystems.Add(system);
        for (var i = 0; i < values.Count; i++)
        {
            var value = GradeValue.Create(system.Id, values[i].Label, i, type == GradeSystemType.Color ? values[i].Hex : null);
            Apply(value, type, inputs[i]);
            db.GradeValues.Add(value);
        }

        await SaveUniqueAsync(ct);
        return (await LoadAsync(gymId, true, ct)).First(s => s.Id == system.Id);
    }

    public async Task<GradeSystemDto> UpdateAsync(Guid systemId, UpdateGradeSystemRequest r, CancellationToken ct = default)
    {
        var system = await db.GradeSystems.FirstOrDefaultAsync(s => s.Id == systemId && s.GymId != null, ct) ?? throw new NotFoundException("Grading system", systemId);
        var gymId = system.GymId!.Value;
        await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        if (r.Name is not null)
        {
            ValidateName(Input.Trimmed(r.Name));
            system.Rename(r.Name);
        }
        if (r.IsActive is { } active) system.SetActive(active);
        await SaveUniqueAsync(ct);
        return (await LoadAsync(gymId, true, ct)).First(s => s.Id == system.Id);
    }

    /// <summary>
    /// Replaces the ordered value list. Listed existing values are updated and re-ranked, new ones are added,
    /// and values left out are deactivated (never deleted), so boulders graded with them keep their grade.
    /// </summary>
    public async Task<GradeSystemDto> SetValuesAsync(Guid systemId, SetGradeValuesRequest r, CancellationToken ct = default)
    {
        var system = await db.GradeSystems.FirstOrDefaultAsync(s => s.Id == systemId && s.GymId != null, ct) ?? throw new NotFoundException("Grading system", systemId);
        var gymId = system.GymId!.Value;
        await access.RequireRoleAsync(gymId, GymRole.Admin, ct);

        var input = r.Values ?? [];
        ValidateValues(system.Type, input);

        var existing = await db.GradeValues.Where(v => v.GradeSystemId == systemId).ToListAsync(ct);
        // What the grades in use are worth before the change, for the ones this change retires.
        var before = Leaderboards.BoulderPoints.ForScale(existing, scoring);
        var byId = existing.ToDictionary(v => v.Id);
        if (input.Any(v => v.Id is { } id && !byId.ContainsKey(id)))
            throw new ValidationException("values", "A value doesn't belong to this grading system.");

        var keep = new HashSet<Guid>();
        for (var i = 0; i < input.Count; i++)
        {
            var item = input[i];
            var hex = system.Type == GradeSystemType.Color ? item.ColorHex : null;
            if (item.Id is { } id)
            {
                byId[id].Update(item.Label!, i, hex);
                Apply(byId[id], system.Type, item);
                keep.Add(id);
            }
            else
            {
                var value = GradeValue.Create(systemId, item.Label!, i, hex);
                Apply(value, system.Type, item);
                db.GradeValues.Add(value);
            }
        }
        // Retired values sort after the active scale and keep their label: old boulders still show it. They also keep
        // the points they were worth, so retiring a grade moves nobody else's points.
        var next = input.Count;
        foreach (var v in existing.Where(v => !keep.Contains(v.Id)).OrderBy(v => v.IsActive ? 0 : 1).ThenBy(v => v.Rank))
            v.Retire(next++, before[v.Id].Automatic);

        await SaveUniqueAsync(ct);
        return (await LoadAsync(gymId, true, ct)).First(s => s.Id == systemId);
    }

    public async Task<IReadOnlyList<GradeSystemDto>> ReorderAsync(Guid gymId, ReorderGradeSystemsRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        var systems = await db.GradeSystems.Where(s => s.GymId == gymId).ToListAsync(ct);
        var ids = r.GradeSystemIds ?? [];
        if (ids.Count != systems.Count || ids.Distinct().Count() != ids.Count || !systems.All(s => ids.Contains(s.Id)))
            throw new ValidationException("gradeSystemIds", "List every grading system of this gym exactly once.");
        for (var i = 0; i < ids.Count; i++) systems.First(s => s.Id == ids[i]).MoveTo(i);
        await db.SaveChangesAsync(ct);
        return await LoadAsync(gymId, true, ct);
    }

    internal async Task<IReadOnlyList<GradeSystemDto>> LoadAsync(Guid gymId, bool includeInactive, CancellationToken ct)
    {
        var systems = await db.GradeSystems.AsNoTracking()
            .Where(s => s.GymId == gymId && (includeInactive || s.IsActive))
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);
        var ids = systems.Select(s => s.Id).ToList();
        var values = await db.GradeValues.AsNoTracking()
            .Where(v => ids.Contains(v.GradeSystemId) && (includeInactive || v.IsActive))
            .OrderBy(v => v.Rank)
            .ToListAsync(ct);
        // Points come from the whole scale, retired grades included, even when only the active ones are shown.
        var allValues = includeInactive ? values : await db.GradeValues.AsNoTracking().Where(v => ids.Contains(v.GradeSystemId)).ToListAsync(ct);
        return systems.Select(s =>
        {
            var points = Leaderboards.BoulderPoints.ForScale(allValues.Where(v => v.GradeSystemId == s.Id).ToList(), scoring);
            return new GradeSystemDto(s.Id, s.GymId!.Value, s.Name, s.Type, s.IsActive, s.SortOrder,
                values.Where(v => v.GradeSystemId == s.Id).Select(v => new GradeValueDto(v.Id, v.Label, v.Rank, v.ColorHex, v.IsActive,
                    v.ColorHex2, points[v.Id].Effective, points[v.Id].Automatic, v.PointsOverride)).ToList());
        }).ToList();
    }

    private static void ValidateName(string name) =>
        new Validator()
            .Check(name.Length >= 1, "name", "Enter a name.")
            .Check(name.Length <= GradeSystem.NameMaxLength, "name", $"Keep it to {GradeSystem.NameMaxLength} characters or fewer.")
            .ThrowIfInvalid();

    /// <summary>The second colour and the points, which older app builds don't send: absent means "as it was".</summary>
    private static void Apply(GradeValue value, GradeSystemType type, GradeValueInput input)
    {
        if (type != GradeSystemType.Color) value.SetSecondColor(null);
        else if (input.ColorHex2 is { } second) value.SetSecondColor(second);
        if (input.Points is { } points) value.SetPointsOverride(points == 0 ? null : points);
    }

    private static void ValidateValues(GradeSystemType type, IReadOnlyList<GradeValueInput> inputs)
    {
        var values = inputs.Select(x => (Label: Input.Trimmed(x.Label), Hex: x.ColorHex)).ToList();
        var seconds = inputs.Where(x => !string.IsNullOrWhiteSpace(x.ColorHex2)).ToList();
        var v = new Validator()
            .Check(values.Count >= 1, "values", "Add at least one grade.")
            .Check(values.Count <= MaxValuesPerSystem, "values", $"Use at most {MaxValuesPerSystem} grades.")
            .Check(values.All(x => x.Label.Length is >= 1 and <= GradeValue.LabelMaxLength), "values", $"Each grade needs a label of 1–{GradeValue.LabelMaxLength} characters.")
            .Check(values.Select(x => x.Label.ToLowerInvariant()).Distinct().Count() == values.Count, "values", "Grade labels must be unique.");
        if (type == GradeSystemType.Color)
            v.Check(values.All(x => x.Hex is not null && HexColor().IsMatch(x.Hex)), "values", "Every colour grade needs a colour like #F5C400.");
        v.Check(seconds.Count == 0 || type == GradeSystemType.Color, "values", "Only colour grades can have two colours.")
         .Check(seconds.All(x => HexColor().IsMatch(x.ColorHex2!.Trim())), "values", "Every colour grade needs a colour like #F5C400.")
         .Check(seconds.All(x => !string.Equals(x.ColorHex2!.Trim(), x.ColorHex?.Trim(), StringComparison.OrdinalIgnoreCase)), "values",
             "The two colours of a grade must be different.")
         .Check(inputs.All(x => x.Points is null or (>= 0 and <= GradeValue.MaxPoints)), "values", $"Points go from 1 to {GradeValue.MaxPoints}.");
        v.ThrowIfInvalid();
    }

    private async Task SaveUniqueAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { throw new ValidationException("name", "This gym already has a grading system with that name."); }
    }
}
