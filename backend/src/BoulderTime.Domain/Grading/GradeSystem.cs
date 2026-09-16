using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Grading;

public enum GradeSystemType
{
    /// <summary>Colour-coded difficulty (e.g. white → black). The colour describes the GRADE, never the holds.</summary>
    Color = 0,
    Fontainebleau = 1,
    VScale = 2,
    Custom = 3,
}

/// <summary>
/// A grading methodology used by one gym. A gym can run several systems at once (e.g. colour + Font);
/// adding one never requires a schema change.
/// </summary>
public class GradeSystem : IAuditable
{
    public const int NameMaxLength = 40;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public GradeSystemType Type { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int SortOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private GradeSystem() { }

    public static GradeSystem Create(Guid gymId, string name, GradeSystemType type, int sortOrder) => new()
    {
        Id = Guid.NewGuid(), GymId = gymId, Name = name.Trim(), Type = type, SortOrder = sortOrder,
    };

    public void Rename(string name) => Name = name.Trim();
    public void SetActive(bool active) => IsActive = active;
    public void MoveTo(int sortOrder) => SortOrder = sortOrder;
}

/// <summary>
/// One grade within a system. <see cref="Rank"/> orders difficulty (0 = easiest) and powers "highest grade" and scoring.
/// Values are deactivated rather than deleted so existing boulders keep their grade.
/// </summary>
public class GradeValue
{
    public const int LabelMaxLength = 20;

    public Guid Id { get; private set; }
    public Guid GradeSystemId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public int Rank { get; private set; }
    /// <summary>Only for colour systems: the colour of the GRADE (e.g. "#F5C400" for yellow). Never a hold colour.</summary>
    public string? ColorHex { get; private set; }
    public bool IsActive { get; private set; } = true;

    private GradeValue() { }

    public static GradeValue Create(Guid systemId, string label, int rank, string? colorHex) => new()
    {
        Id = Guid.NewGuid(), GradeSystemId = systemId, Label = label.Trim(), Rank = rank,
        ColorHex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim().ToUpperInvariant(),
    };

    public void Update(string label, int rank, string? colorHex)
    {
        Label = label.Trim();
        Rank = rank;
        ColorHex = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim().ToUpperInvariant();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
}
