using BoulderTime.Domain.Common;

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
    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public Guid SectorId { get; private set; }
    /// <summary>Object path in the boulder-images bucket, e.g. gyms/{gymId}/boulders/{guid}.jpg.</summary>
    public string PhotoPath { get; private set; } = string.Empty;
    /// <summary>Small version (≈480 px) generated on the uploading device for lists; null for older boulders.</summary>
    public string? ThumbnailPath { get; private set; }
    public HoldColor HoldColor { get; private set; }
    public Guid? SetterUserId { get; private set; }
    public BoulderStatus Status { get; private set; } = BoulderStatus.Active;
    /// <summary>Null for boulders loaded by the system (demo seed, imports).</summary>
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private Boulder() { }

    public static Boulder Create(Guid gymId, Guid sectorId, string photoPath, HoldColor holdColor, Guid? setterUserId, Guid? createdBy, string? thumbnailPath = null) => new()
    {
        Id = Guid.NewGuid(), GymId = gymId, SectorId = sectorId, PhotoPath = photoPath, ThumbnailPath = thumbnailPath, HoldColor = holdColor,
        SetterUserId = setterUserId, CreatedByUserId = createdBy, Status = BoulderStatus.Active,
    };

    /// <returns>Storage paths that are no longer referenced after the update.</returns>
    public IReadOnlyList<string> Update(Guid sectorId, string photoPath, string? thumbnailPath, HoldColor holdColor, Guid? setterUserId)
    {
        var obsolete = new List<string>();
        if (photoPath != PhotoPath)
        {
            obsolete.Add(PhotoPath);
            if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
            ThumbnailPath = thumbnailPath;
        }
        SectorId = sectorId;
        PhotoPath = photoPath;
        HoldColor = holdColor;
        SetterUserId = setterUserId;
        return obsolete;
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

    /// <summary>Only a removed boulder comes back. A deleted one has no photo and no content to come back to.</summary>
    public void Restore()
    {
        if (Status == BoulderStatus.Deleted) throw new InvalidOperationException("A deleted boulder cannot be restored.");
        Status = BoulderStatus.Active;
        RemovedAt = null;
        RemovedByUserId = null;
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
        if (PhotoPath.Length > 0) obsolete.Add(PhotoPath);
        if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
        PhotoPath = string.Empty;
        ThumbnailPath = null;
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
