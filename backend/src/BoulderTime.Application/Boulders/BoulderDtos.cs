using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Staff;

namespace BoulderTime.Application.Boulders;

/// <summary>An official grade, labelled with its system so clients never confuse a colour GRADE with hold colour.</summary>
public sealed record BoulderGradeDto(Guid GradeSystemId, string SystemName, GradeSystemType SystemType, Guid GradeValueId, string Label, int Rank, string? ColorHex,
    string? ColorHex2 = null);

public sealed record RatingSummaryDto(double? Average, int Count);

/// <summary>A gym's tag as a boulder shows it.</summary>
public sealed record BoulderTagDto(Guid Id, string Name);

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
    RatingSummaryDto Rating, ViewerProgressDto? Viewer,
    BoulderIntensity? Intensity = null, IReadOnlyList<BoulderTagDto>? Tags = null,
    /// <summary>How many people have completed it (one each, however many times they tried). Suspended accounts don't count.</summary>
    int Completions = 0,
    /// <summary>The setters' name for it; null when it has none (never empty).</summary>
    string? Name = null,
    /// <summary>What completing it scores on the gym's leaderboard; null when it scores nothing (ungraded).</summary>
    int? Points = null,
    /// <summary>The gym's logo, for lists that mix gyms (Home); null when the gym has none.</summary>
    string? GymLogoUrl = null,
    /// <summary>The viewer marked it as a project (ADR-067). Added later: older apps don't read it.</summary>
    bool IsProject = false,
    /// <summary>On the wall and set within <see cref="NewBoulders.Window"/>. Added later: older apps don't read it.</summary>
    bool IsNew = false,
    /// <summary>
    /// Highlighted on its sector's current photo, so its holds can be lit up there
    /// (<c>GET /api/boulders/{id}/holds-on-sector-photo</c>). Added later: older apps don't read it.
    /// </summary>
    bool HoldsOnSectorPhoto = false);

/// <summary>What "new" means for a boulder: set in the last few days and still on the wall.</summary>
public static class NewBoulders
{
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    public static DateTimeOffset Since(DateTimeOffset now) => now - Window;

    public static bool Is(Boulder b, DateTimeOffset now) => b.Status == BoulderStatus.Active && b.CreatedAt >= Since(now);
}

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
    string? ThumbnailUrl = null,
    BoulderIntensity? Intensity = null, IReadOnlyList<BoulderTagDto>? Tags = null, int Completions = 0,
    string? Name = null,
    /// <summary>What completing it scores: <see cref="PointsOverride"/> when staff set one, else its grade's points.</summary>
    int? Points = null,
    /// <summary>The points staff set on this boulder by hand, shown to its gym's staff only; null when it follows its grade.</summary>
    int? PointsOverride = null,
    /// <summary>What its highlighted photo was made from, for its gym's staff to go on editing; null otherwise.</summary>
    BoulderHighlightDto? Highlight = null,
    /// <summary>The viewer marked it as a project (ADR-067). Added later: older apps don't read it.</summary>
    bool IsProject = false,
    /// <summary>How its card picture was made, for its gym's staff (ADR-068); null otherwise or when not known.</summary>
    ThumbnailMode? ThumbnailMode = null);

public sealed record GradeChoice(Guid? GradeSystemId, Guid? GradeValueId);

/// <param name="ThumbnailPath">Optional small version of the photo for lists (generated on the device).</param>
/// <param name="Intensity">Optional; none means "not said". Independent of the grades.</param>
/// <param name="TagIds">The gym's tags for this boulder. Null leaves the tags as they are (older apps don't send them); empty clears them.</param>
/// <param name="Name">Optional name. Null leaves it as it is (older apps don't send it); empty removes it.</param>
/// <param name="Points">Points set by hand for this boulder. Null leaves them as they are, 0 goes back to the grade's, 1–1000 sets them.</param>
/// <param name="Highlight">
/// What a highlighted photo was made from, sent with it. Null keeps what the boulder has while its photo stays the same;
/// a new photo without one leaves the boulder with no highlight to go back to.
/// </param>
public sealed record SaveBoulderRequest(
    Guid? SectorId, string? PhotoPath, HoldColor? HoldColor, IReadOnlyList<GradeChoice>? Grades, Guid? SetterUserId, string? ThumbnailPath = null,
    BoulderIntensity? Intensity = null, IReadOnlyList<Guid>? TagIds = null, string? Name = null, int? Points = null,
    BoulderHighlightDto? Highlight = null,
    /// <summary>How the card picture sent with it was made (ADR-068). Older apps don't send it: it is then not known.</summary>
    ThumbnailMode? ThumbnailMode = null);

/// <summary>
/// "Highlight the holds" as it was left: the original photo (a sector's wall photo or one uploaded for it), the mask
/// PNG, and the choices (hold colour, outline) as a small JSON object the app reads back.
/// </summary>
public sealed record BoulderHighlightDto(string? SourcePath, string? MaskPath, string? Settings);

/// <summary>Filters by the viewer's own progress. Ignored for anonymous requests.</summary>
/// <remarks>
/// <see cref="Projects"/> keeps its first meaning, tried and not completed, for the apps already installed; from 1.1 the
/// app calls it "tried". <see cref="Marked"/> is the projects the climber chose, not yet completed (ADR-067).
/// </remarks>
public enum ProgressFilter { All, Untried, Projects, Completed, Marked }

/// <param name="TagIds">Boulders carrying ALL of these tags (each one narrows, like every other filter).</param>
/// <param name="Intensities">Boulders with ANY of these intensities; boulders without one are left out.</param>
/// <param name="Name">Boulders whose name contains this text, whatever the case (boulders without a name never match).</param>
/// <param name="IsNew">Only the new boulders (<see cref="NewBoulders"/>). Added later: older apps never send it.</param>
/// <param name="Sort">How to order the list. None: newest set first (taken down: last taken down first), as always.</param>
/// <param name="Order">Reverses a sort; each sort has its own natural direction when none is given.</param>
public sealed record BoulderQuery(
    BoulderStatus? Status, Guid? SectorId, HoldColor? HoldColor, Guid? GradeValueId,
    ProgressFilter? Progress, int? MinRating, int? Page, int? PageSize,
    IReadOnlyList<Guid>? TagIds = null, IReadOnlyList<BoulderIntensity>? Intensities = null, string? Name = null,
    BoulderSort? Sort = null, SortDirection? Order = null, bool? IsNew = null);

/// <summary>
/// Orders a gym's boulder list. Sector follows the gym's own order of its sectors; points are what a send scores;
/// grade follows the gym's scales (never alphabetical: 6A+ comes before 6B); set is the day it went on the wall (a
/// retrace is a new boulder, so it is also the retrace day); removed is the day it came off (taken-down list only).
/// Boulders missing the value (ungraded, no points) always come last. Ties: sector order, then newest set first.
/// </summary>
public enum BoulderSort { Sector, Points, Grade, Set, Removed }

public enum SortDirection { Asc, Desc }

/// <param name="NotifyFollowers">Send one "sector retraced" notification per affected sector to its followers.</param>
public sealed record RemoveBouldersRequest(IReadOnlyList<Guid>? BoulderIds, bool? NotifyFollowers = false);

/// <summary>Result of a (bulk) removal, grouped by sector so a single "sector retraced" update can follow.</summary>
public sealed record RemoveBouldersResult(int Removed, IReadOnlyList<SectorRemovalDto> Sectors);
public sealed record SectorRemovalDto(Guid SectorId, string SectorName, int Removed);

/// <param name="Mask">The mask of a highlighted photo: a PNG, stored next to the boulder's photo.</param>
public sealed record PhotoUploadRequest(string? ContentType, long? SizeBytes, bool? Thumbnail = false, bool? Mask = false);
