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
/// adding one never requires a schema change. A system with no gym is one of BoulderTime's reference scales
/// (<see cref="ReferenceGrades"/>): climbers may vote a boulder's grade in it even when the gym doesn't use it.
/// </summary>
public class GradeSystem : IAuditable
{
    public const int NameMaxLength = 40;

    public Guid Id { get; private set; }
    /// <summary>Null for a reference scale, which belongs to no gym and only ever collects community votes.</summary>
    public Guid? GymId { get; private set; }
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
    /// <summary>
    /// A second colour, for the in-between grades some gyms mark with two colours ("green/blue", between green and
    /// blue). Only for colour systems; null for an ordinary one-colour grade.
    /// </summary>
    public string? ColorHex2 { get; private set; }
    /// <summary>Points staff set for this grade instead of the automatic ones. Null means automatic.</summary>
    public int? PointsOverride { get; private set; }
    /// <summary>
    /// The automatic points the grade was worth on the day it was retired, kept so that retiring a grade doesn't change
    /// what it is worth to the people who climbed it. Null while the grade is in use.
    /// </summary>
    public int? RetiredPoints { get; private set; }
    public bool IsActive { get; private set; } = true;

    public const int MaxPoints = 1000;

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
        RetiredPoints = null;
    }

    /// <summary>Null or blank: a one-colour grade.</summary>
    public void SetSecondColor(string? colorHex) =>
        ColorHex2 = string.IsNullOrWhiteSpace(colorHex) ? null : colorHex.Trim().ToUpperInvariant();

    /// <summary>Null: automatic points again.</summary>
    public void SetPointsOverride(int? points)
    {
        if (points is < 1 or > MaxPoints) throw new ArgumentOutOfRangeException(nameof(points));
        PointsOverride = points;
    }

    /// <summary>
    /// Takes the grade out of the scale, after the active ones. <paramref name="automaticPoints"/> is what it was worth
    /// until now, kept for the boulders that carry it; a grade retired earlier keeps the value it already has.
    /// </summary>
    public void Retire(int rank, int automaticPoints)
    {
        Rank = rank;
        if (IsActive || RetiredPoints is null) RetiredPoints = automaticPoints;
        IsActive = false;
    }

    public void Deactivate() => IsActive = false;
}
