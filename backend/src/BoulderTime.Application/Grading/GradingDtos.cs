using BoulderTime.Domain.Grading;

namespace BoulderTime.Application.Grading;

/// <param name="ColorHex2">The second colour of a two-colour grade ("green/blue"); null for one colour.</param>
/// <param name="Points">What a send of this grade scores: <paramref name="PointsOverride"/> when staff set one, else <paramref name="AutomaticPoints"/>.</param>
/// <param name="AutomaticPoints">The points the scale would give it on its own (10 for the easiest grade, 100 for the hardest).</param>
public sealed record GradeValueDto(Guid Id, string Label, int Rank, string? ColorHex, bool IsActive,
    string? ColorHex2 = null, int Points = 0, int AutomaticPoints = 0, int? PointsOverride = null);

public sealed record GradeSystemDto(Guid Id, Guid GymId, string Name, GradeSystemType Type, bool IsActive, int SortOrder, IReadOnlyList<GradeValueDto> Values);

/// <param name="ColorHex2">
/// Second colour of a two-colour grade. For a grade that already exists, null leaves it as it is (app builds from before
/// two-colour grades don't send it) and an empty string makes it one colour again.
/// </param>
/// <param name="Points">
/// Points set by hand. Null leaves them as they are (older app builds don't send them), 0 goes back to automatic,
/// 1–1000 sets them.
/// </param>
public sealed record GradeValueInput(Guid? Id, string? Label, string? ColorHex, string? ColorHex2 = null, int? Points = null);

public sealed record CreateGradeSystemRequest(string? Name, GradeSystemType? Type, IReadOnlyList<GradeValueInput>? Values);
public sealed record UpdateGradeSystemRequest(string? Name, bool? IsActive);
public sealed record SetGradeValuesRequest(IReadOnlyList<GradeValueInput>? Values);
public sealed record ReorderGradeSystemsRequest(IReadOnlyList<Guid>? GradeSystemIds);
