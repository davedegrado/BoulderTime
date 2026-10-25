using BoulderTime.Domain.Common;
using BoulderTime.Domain.Gyms;

namespace BoulderTime.Domain.Boulders;

public enum BoulderStatus
{
    Active = 0,
    Removed = 1,
    /// <summary>
    /// Erased by the gym. The row survives only to hold up climbers' history: its photo, videos, comments and
    /// ratings are gone and no list or page shows it any more, but the sends it carries still count.
    /// </summary>
    Deleted = 2,
}

/// <summary>
/// A boulder problem on the wall. Boulders are never versioned: retracing removes the old boulder and creates a
/// brand-new one. Removed boulders stay in the database forever so climbers keep their history.
/// </summary>
public class Boulder : IAuditable
{
    public const int NameMaxLength = 60;
    /// <summary>The most a single boulder can be worth when staff set its points by hand.</summary>
    public const int MaxPoints = 1000;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public Guid SectorId { get; private set; }
    /// <summary>
    /// Object path in the boulder-images bucket, e.g. gyms/{gymId}/boulders/{guid}.jpg — or a sector's wall photo
    /// (gyms/{gymId}/sectors/…), which the boulder only borrows and never deletes. Empty for a deleted boulder.
    /// </summary>
    public string PhotoPath { get; private set; } = string.Empty;
    /// <summary>Small version (≈480 px) generated on the uploading device for lists; null for older boulders.</summary>
    public string? ThumbnailPath { get; private set; }
    /// <summary>
    /// How the card picture was made (ADR-068); null when not known — an older app made it, or there is none. It follows
    /// the picture: a new picture takes the choice sent with it, and keeps none when none was sent.
    /// </summary>
    public ThumbnailMode? ThumbnailMode { get; private set; }
    public HoldColor HoldColor { get; private set; }
    /// <summary>
    /// The name the setters gave it ("La Fessura"), optional. Never empty: no name is null, so no screen ever shows an
    /// empty label, and a nameless boulder is never called by its id.
    /// </summary>
    public string? Name { get; private set; }
    /// <summary>
    /// Points staff set for this one boulder, overriding what its grade is worth. Null means it scores as its grade does.
    /// </summary>
    public int? PointsOverride { get; private set; }
    /// <summary>How intense it is to climb, independent of the grade. Null until staff say (boulders set before it existed).</summary>
    public BoulderIntensity? Intensity { get; private set; }
    public Guid? SetterUserId { get; private set; }
    public BoulderStatus Status { get; private set; } = BoulderStatus.Active;
    /// <summary>Null for boulders loaded by the system (demo seed, imports).</summary>
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    /// <summary>
    /// Set on the boulders of a gym's trial period, before it launched BoulderTime for real: they stay in the history of
    /// the climbers who tested it, but score nothing and count in no leaderboard. Null for every ordinary boulder.
    /// </summary>
    public DateTimeOffset? BetaTestAt { get; private set; }
    /// <summary>
    /// "Highlight the holds", kept so staff can go on from where they left it: the photo the holds were highlighted on
    /// (a sector's wall photo, which the boulder only borrows, or an original of its own), the mask of what keeps its
    /// colour (a PNG next to the boulder's photo), and the choices made (hold colour, outline) as JSON. All null when the
    /// boulder's photo isn't a highlighted one. The photo itself is the finished picture.
    /// </summary>
    public string? HighlightSourcePath { get; private set; }
    public string? HighlightMaskPath { get; private set; }
    public string? HighlightSettings { get; private set; }
    public const int HighlightSettingsMaxLength = 500;
    public Guid? RemovedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private Boulder() { }

    public static Boulder Create(Guid gymId, Guid sectorId, string photoPath, HoldColor holdColor, Guid? setterUserId, Guid? createdBy, string? thumbnailPath = null,
        BoulderIntensity? intensity = null, ThumbnailMode? thumbnailMode = null) => new()
    {
        Id = Guid.NewGuid(), GymId = gymId, SectorId = sectorId, PhotoPath = photoPath, ThumbnailPath = thumbnailPath, HoldColor = holdColor,
        ThumbnailMode = thumbnailPath is null ? null : thumbnailMode,
        Intensity = intensity, SetterUserId = setterUserId, CreatedByUserId = createdBy, Status = BoulderStatus.Active,
    };

    /// <returns>Storage paths that are no longer referenced after the update.</returns>
    public IReadOnlyList<string> Update(Guid sectorId, string photoPath, string? thumbnailPath, HoldColor holdColor, Guid? setterUserId,
        BoulderIntensity? intensity = null, ThumbnailMode? thumbnailMode = null)
    {
        var picture = ThumbnailPath;
        Intensity = intensity;
        var obsolete = new List<string>();
        if (photoPath != PhotoPath)
        {
            if (OwnsPhoto) obsolete.Add(PhotoPath);
            if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
            ThumbnailPath = thumbnailPath;
        }
        // Same photo, new thumbnail: staff picked another part of it for the cards. No thumbnail means keep the one
        // there is.
        else if (thumbnailPath is not null && thumbnailPath != ThumbnailPath)
        {
            if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
            ThumbnailPath = thumbnailPath;
        }
        if (ThumbnailPath != picture) ThumbnailMode = ThumbnailPath is null ? null : thumbnailMode;
        SectorId = sectorId;
        PhotoPath = photoPath;
        HoldColor = holdColor;
        SetterUserId = setterUserId;
        return obsolete;
    }

    /// <summary>
    /// Sets, replaces or (all null) clears what the highlighted photo was made from.
    /// </summary>
    /// <returns>Storage paths no longer used: the old mask, and the old original when it was the boulder's own.</returns>
    public IReadOnlyList<string> SetHighlight(string? sourcePath, string? maskPath, string? settings)
    {
        var obsolete = new List<string>();
        if (HighlightMaskPath is not null && HighlightMaskPath != maskPath) obsolete.Add(HighlightMaskPath);
        if (HighlightSourcePath is not null && HighlightSourcePath != sourcePath && !Sector.IsSectorPhoto(HighlightSourcePath))
            obsolete.Add(HighlightSourcePath);
        HighlightSourcePath = sourcePath;
        HighlightMaskPath = maskPath;
        HighlightSettings = settings;
        return obsolete;
    }

    /// <summary>
    /// Puts a lighter copy of one of the boulder's files in place of the file it has now: the same picture, encoded
    /// again. Only its own photo (not a wall photo, nor the original a highlight goes on from) and its card picture.
    /// </summary>
    /// <returns>False when the boulder no longer has <paramref name="from"/> there.</returns>
    public bool SwapFile(PhotoFile file, string from, string to)
    {
        if (file == PhotoFile.Photo)
        {
            if (PhotoPath != from || !OwnsPhoto || PhotoPath == HighlightSourcePath) return false;
            PhotoPath = to;
            return true;
        }
        if (ThumbnailPath != from) return false;
        ThumbnailPath = to;
        return true;
    }

    /// <summary>A photo of its own, as opposed to none or its sector's wall photo, which other boulders use too.</summary>
    private bool OwnsPhoto => PhotoPath.Length > 0 && !Sector.IsSectorPhoto(PhotoPath);

    /// <summary>Blank or spaces only means no name.</summary>
    public void Rename(string? name) => Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    /// <summary>Null goes back to the points of the grade.</summary>
    public void SetPointsOverride(int? points)
    {
        if (points is < 1 or > MaxPoints) throw new ArgumentOutOfRangeException(nameof(points));
        PointsOverride = points;
    }

    /// <returns>False if it was already removed, or deleted (idempotent bulk operations).</returns>
    public bool Remove(Guid? byUserId, DateTimeOffset now)
    {
        // Deleted is not a milder state than removed: taking a deleted boulder "off the wall" would put it back on
        // the removed list with no photo and nothing on it. Deletion is the end of the line.
        if (Status is BoulderStatus.Removed or BoulderStatus.Deleted) return false;
        Status = BoulderStatus.Removed;
        RemovedAt = now;
        RemovedByUserId = byUserId;
        return true;
    }

    /// <summary>
    /// Only a removed boulder comes back. A deleted one has no photo and no content to come back to. A trial boulder put
    /// back on the wall is an ordinary boulder again: only boulders off the wall are trial ones.
    /// </summary>
    public void Restore()
    {
        if (Status == BoulderStatus.Deleted) throw new InvalidOperationException("A deleted boulder cannot be restored.");
        Status = BoulderStatus.Active;
        RemovedAt = null;
        RemovedByUserId = null;
        BetaTestAt = null;
    }

    /// <summary>Marks a boulder off the wall as one of the gym's trial period.</summary>
    /// <returns>False if it is on the wall, or already marked (so running it again only adds the ones missing).</returns>
    public bool MarkBetaTest(DateTimeOffset now)
    {
        if (Status == BoulderStatus.Active || BetaTestAt is not null) return false;
        BetaTestAt = now;
        return true;
    }

    /// <returns>False if it wasn't marked.</returns>
    public bool UnmarkBetaTest()
    {
        if (BetaTestAt is null) return false;
        BetaTestAt = null;
        return true;
    }

    /// <summary>
    /// Erases the boulder from the gym. The row stays, because climbers' sends point at it and those are theirs,
    /// not the gym's — but everything the gym put on it goes, starting with the photo. There is no way back:
    /// a deleted boulder cannot be restored, because there is nothing left to restore it to.
    /// </summary>
    /// <returns>Storage paths that are no longer referenced, so they can be deleted.</returns>
    public IReadOnlyList<string> Delete(Guid? byUserId, DateTimeOffset now)
    {
        var obsolete = new List<string>();
        if (OwnsPhoto) obsolete.Add(PhotoPath);
        if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
        obsolete.AddRange(SetHighlight(null, null, null));
        PhotoPath = string.Empty;
        ThumbnailPath = null;
        ThumbnailMode = null;
        Status = BoulderStatus.Deleted;
        RemovedAt ??= now;
        RemovedByUserId ??= byUserId;
        return obsolete;
    }
}

public enum GradeSource
{
    /// <summary>Official grade set by gym staff.</summary>
    Staff = 0,
    /// <summary>Reserved: community consensus is stored separately as grade suggestions (Phase 5).</summary>
    Community = 1,
}

/// <summary>An official grade of a boulder in one grading system. At most one staff grade per (boulder, system).</summary>
public class BoulderGrade
{
    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid GradeSystemId { get; private set; }
    public Guid GradeValueId { get; private set; }
    public GradeSource Source { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private BoulderGrade() { }

    public static BoulderGrade Official(Guid boulderId, Guid systemId, Guid valueId, Guid? createdBy, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), BoulderId = boulderId, GradeSystemId = systemId, GradeValueId = valueId,
        Source = GradeSource.Staff, CreatedByUserId = createdBy, CreatedAt = now,
    };
}
