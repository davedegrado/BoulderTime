namespace BoulderTime.Domain.Grading;

/// <summary>Starting value lists for common systems. Gyms can edit them after creation.</summary>
public static class GradePresets
{
    public static readonly IReadOnlyList<(string Label, string? Hex)> Color =
    [
        ("White", "#F2F2F2"), ("Yellow", "#F5C400"), ("Green", "#2E9E4F"), ("Blue", "#2F6FDB"),
        ("Red", "#D93A2B"), ("Black", "#1A1A1A"),
    ];

    public static readonly IReadOnlyList<(string Label, string? Hex)> Fontainebleau =
        new[] { "3", "4", "4+", "5", "5+", "6A", "6A+", "6B", "6B+", "6C", "6C+", "7A", "7A+", "7B", "7B+", "7C", "7C+", "8A", "8A+", "8B", "8B+", "8C", "8C+" }
            .Select(l => (l, (string?)null)).ToList();

    public static readonly IReadOnlyList<(string Label, string? Hex)> VScale =
        new[] { "VB", "V0", "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8", "V9", "V10", "V11", "V12", "V13", "V14", "V15", "V16", "V17" }
            .Select(l => (l, (string?)null)).ToList();

    public static IReadOnlyList<(string Label, string? Hex)> For(GradeSystemType type) => type switch
    {
        GradeSystemType.Color => Color,
        GradeSystemType.Fontainebleau => Fontainebleau,
        GradeSystemType.VScale => VScale,
        _ => [],
    };

    public static string DefaultName(GradeSystemType type) => type switch
    {
        GradeSystemType.Color => "Colour",
        GradeSystemType.Fontainebleau => "Fontainebleau",
        GradeSystemType.VScale => "V-scale",
        _ => "Custom",
    };
}
