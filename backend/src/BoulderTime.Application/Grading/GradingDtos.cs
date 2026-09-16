using BoulderTime.Domain.Grading;

namespace BoulderTime.Application.Grading;

public sealed record GradeValueDto(Guid Id, string Label, int Rank, string? ColorHex, bool IsActive);

public sealed record GradeSystemDto(Guid Id, Guid GymId, string Name, GradeSystemType Type, bool IsActive, int SortOrder, IReadOnlyList<GradeValueDto> Values);

public sealed record GradeValueInput(Guid? Id, string? Label, string? ColorHex);

public sealed record CreateGradeSystemRequest(string? Name, GradeSystemType? Type, IReadOnlyList<GradeValueInput>? Values);
public sealed record UpdateGradeSystemRequest(string? Name, bool? IsActive);
public sealed record SetGradeValuesRequest(IReadOnlyList<GradeValueInput>? Values);
public sealed record ReorderGradeSystemsRequest(IReadOnlyList<Guid>? GradeSystemIds);
