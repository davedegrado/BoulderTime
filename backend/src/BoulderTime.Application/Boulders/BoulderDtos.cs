using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Staff;

namespace BoulderTime.Application.Boulders;

/// <summary>An official grade, labelled with its system so clients never confuse a colour GRADE with hold colour.</summary>
public sealed record BoulderGradeDto(Guid GradeSystemId, string SystemName, GradeSystemType SystemType, Guid GradeValueId, string Label, int Rank, string? ColorHex);

public sealed record RatingSummaryDto(double? Average, int Count);

/// <summary>The signed-in viewer's own tracking of a boulder; null when anonymous or never tracked.</summary>
public sealed record ViewerProgressDto(int Attempts, bool Completed, DateTimeOffset? CompletedAt, int? Rating);

/// <param name="PhotoUrl">
/// Card image: the small thumbnail when available, otherwise the full photo. Null for a deleted boulder, which
/// keeps no photo — it survives only inside the history of the climbers who sent it.
/// </param>
public sealed record BoulderSummaryDto(
    Guid Id, Guid GymId, string GymName, Guid SectorId, string SectorName, string? PhotoUrl,
    HoldColor HoldColor, IReadOnlyList<BoulderGradeDto> Grades,
    BoulderStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? RemovedAt,
    RatingSummaryDto Rating, ViewerProgressDto? Viewer);

public sealed record PersonDto(Guid UserId, string DisplayName, string? AvatarUrl);

/// <summary>
/// What deleting a boulder would take with it, shown to staff before they confirm. <see cref="Sends"/> is the one
/// thing deletion does NOT take: it is here so staff can see how many people climbed it before erasing the rest.
/// </summary>
public sealed record DeletionImpactDto(int Sends, int Comments, int Videos, bool HasBeta);

public sealed record BoulderDetailDto(
    Guid Id, Guid GymId, string GymSlug, string GymName, Guid SectorId, string SectorName, string PhotoUrl, string PhotoPath,
    HoldColor HoldColor, IReadOnlyList<BoulderGradeDto> Grades, PersonDto? Setter,
    BoulderStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? RemovedAt, GymRole? ViewerRole,
    RatingSummaryDto Rating, ViewerProgressDto? Viewer, bool IsFollowing,
    /// <summary>Whether climbers of this gym may upload their own beta; the app shows a lock when they may not.</summary>
    bool CommunityVideosEnabled = false,
    /// <summary>
    /// Whether the gym may still add an official beta to this boulder. False when its allowance is full and this
    /// boulder has none: the app hides the upload rather than letting someone film and fail at the last step.
    /// </summary>
    bool CanAddOfficialBeta = true,
    /// <summary>The small picture the cards show (the whole photo, or the part staff chose); null when there is none.</summary>
    string? ThumbnailUrl = null);

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
