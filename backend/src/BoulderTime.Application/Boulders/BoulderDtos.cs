using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Staff;

namespace BoulderTime.Application.Boulders;

/// <summary>An official grade, labelled with its system so clients never confuse a colour GRADE with hold colour.</summary>
public sealed record BoulderGradeDto(Guid GradeSystemId, string SystemName, GradeSystemType SystemType, Guid GradeValueId, string Label, int Rank, string? ColorHex);

public sealed record RatingSummaryDto(double? Average, int Count);

/// <summary>The signed-in viewer's own tracking of a boulder; null when anonymous or never tracked.</summary>
public sealed record ViewerProgressDto(int Attempts, bool Completed, DateTimeOffset? CompletedAt, int? Rating);

/// <param name="PhotoUrl">Card image: the small thumbnail when available, otherwise the full photo.</param>
public sealed record BoulderSummaryDto(
    Guid Id, Guid GymId, string GymName, Guid SectorId, string SectorName, string PhotoUrl,
    HoldColor HoldColor, IReadOnlyList<BoulderGradeDto> Grades,
    BoulderStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? RemovedAt,
    RatingSummaryDto Rating, ViewerProgressDto? Viewer);

public sealed record PersonDto(Guid UserId, string DisplayName, string? AvatarUrl);

public sealed record BoulderDetailDto(
    Guid Id, Guid GymId, string GymSlug, string GymName, Guid SectorId, string SectorName, string PhotoUrl, string PhotoPath,
    HoldColor HoldColor, IReadOnlyList<BoulderGradeDto> Grades, PersonDto? Setter,
    BoulderStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? RemovedAt, GymRole? ViewerRole,
    RatingSummaryDto Rating, ViewerProgressDto? Viewer, bool IsFollowing);

public sealed record GradeChoice(Guid? GradeSystemId, Guid? GradeValueId);

/// <param name="ThumbnailPath">Optional small version of the photo for lists (generated on the device).</param>
public sealed record SaveBoulderRequest(
    Guid? SectorId, string? PhotoPath, HoldColor? HoldColor, IReadOnlyList<GradeChoice>? Grades, Guid? SetterUserId, string? ThumbnailPath = null);

/// <summary>Filters by the viewer's own progress. Ignored for anonymous requests.</summary>
public enum ProgressFilter { All, Untried, Projects, Completed }

public sealed record BoulderQuery(
    BoulderStatus? Status, Guid? SectorId, HoldColor? HoldColor, Guid? GradeValueId,
    ProgressFilter? Progress, int? MinRating, int? Page, int? PageSize);

/// <param name="NotifyFollowers">Send one "sector retraced" notification per affected sector to its followers.</param>
public sealed record RemoveBouldersRequest(IReadOnlyList<Guid>? BoulderIds, bool? NotifyFollowers = false);

/// <summary>Result of a (bulk) removal, grouped by sector so a single "sector retraced" update can follow.</summary>
public sealed record RemoveBouldersResult(int Removed, IReadOnlyList<SectorRemovalDto> Sectors);
public sealed record SectorRemovalDto(Guid SectorId, string SectorName, int Removed);

public sealed record PhotoUploadRequest(string? ContentType, long? SizeBytes, bool? Thumbnail = false);
