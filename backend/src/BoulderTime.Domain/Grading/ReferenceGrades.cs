namespace BoulderTime.Domain.Grading;

/// <summary>
/// The standard scales every boulder can be voted in, whichever systems its gym uses: a climber who thinks in
/// V-grades can say so at a gym that grades in Font. They belong to no gym, are created by the database migration
/// with fixed ids, and are never an official grade, never filtered on and never scored. A gym that runs its own
/// system of the same type is voted in that one instead. Colours have no reference scale: a colour means something
/// only in the gym that chose it.
/// </summary>
public static class ReferenceGrades
{
    public sealed record Scale(Guid Id, string Name, GradeSystemType Type, int SortOrder, IReadOnlyList<Grade> Values);
    public sealed record Grade(Guid Id, string Label, int Rank);

    public static readonly Guid FontainebleauId = new("b07e0000-f0a1-4000-8000-000000000000");
    public static readonly Guid VScaleId = new("b07e0000-5ca1-4000-8000-000000000000");

    public static readonly IReadOnlyList<Scale> All =
    [
        Build(FontainebleauId, "b07e0000-f0a1-4000-8001-", GradeSystemType.Fontainebleau, 0),
        Build(VScaleId, "b07e0000-5ca1-4000-8001-", GradeSystemType.VScale, 1),
    ];

    /// <summary>When the scales were first created; fixed, so the migration that creates them never changes.</summary>
    public static readonly DateTimeOffset CreatedAt = new(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

    // Value ids are the scale's prefix plus the rank, so they stay the same on every database.
    private static Scale Build(Guid id, string valuePrefix, GradeSystemType type, int sortOrder) =>
        new(id, GradePresets.DefaultName(type), type, sortOrder,
            GradePresets.For(type).Select((v, rank) => new Grade(new Guid($"{valuePrefix}{rank:D12}"), v.Label, rank)).ToList());
}
