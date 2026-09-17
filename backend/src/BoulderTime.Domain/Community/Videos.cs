using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Community;

/// <summary>The gym's official beta video for a boulder. One per boulder; staff can replace it. No moderation.</summary>
public class BoulderBeta : IAuditable
{
    public const int CaptionMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    /// <summary>Poster frame captured by the uploading device; optional (some formats can't be decoded in the browser).</summary>
    public string? ThumbnailPath { get; private set; }
    public string? Caption { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private BoulderBeta() { }

    public static BoulderBeta Create(Guid boulderId, Guid userId, string storagePath, string? thumbnailPath, string? caption) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UploadedByUserId = userId, StoragePath = storagePath, ThumbnailPath = Clean(thumbnailPath), Caption = Clean(caption) };

    /// <returns>Storage paths that are no longer referenced (old video and/or thumbnail), so they can be deleted.</returns>
    public IReadOnlyList<string> Replace(Guid userId, string storagePath, string? thumbnailPath, string? caption)
    {
        var obsolete = new List<string>();
        if (StoragePath != storagePath)
        {
            obsolete.Add(StoragePath);
            if (ThumbnailPath is not null && ThumbnailPath != thumbnailPath) obsolete.Add(ThumbnailPath);
            ThumbnailPath = Clean(thumbnailPath);
        }
        else if (thumbnailPath is not null && thumbnailPath != ThumbnailPath)
        {
            if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
            ThumbnailPath = Clean(thumbnailPath);
        }
        UploadedByUserId = userId;
        StoragePath = storagePath;
        Caption = Clean(caption);
        return obsolete;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

public enum VideoStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

/// <summary>
/// A climber's video of a boulder. Visible to everyone only once approved by gym staff. Any change to an approved
/// video sends it back to review. Nobody can review their own video.
/// </summary>
public class BoulderVideo : IAuditable
{
    public const int CaptionMaxLength = 300;
    public const int ReasonMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid UserId { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public string? ThumbnailPath { get; private set; }
    public string? Caption { get; private set; }
    public VideoStatus Status { get; private set; } = VideoStatus.Pending;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private BoulderVideo() { }

    public static BoulderVideo Submit(Guid boulderId, Guid userId, string storagePath, string? thumbnailPath, string? caption) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UserId = userId, StoragePath = storagePath, ThumbnailPath = Clean(thumbnailPath), Caption = Clean(caption) };

    /// <summary>Changing caption or file resets review. Returns storage paths that are no longer referenced.</summary>
    public IReadOnlyList<string> Modify(string? caption, string? storagePath, string? thumbnailPath)
    {
        var obsolete = new List<string>();
        if (storagePath is not null && storagePath != StoragePath)
        {
            obsolete.Add(StoragePath);
            if (ThumbnailPath is not null) obsolete.Add(ThumbnailPath);
            StoragePath = storagePath;
            ThumbnailPath = Clean(thumbnailPath);
        }
        if (caption is not null) Caption = Clean(caption);
        Status = VideoStatus.Pending;
        RejectionReason = null;
        ReviewedAt = null;
        ReviewedByUserId = null;
        return obsolete;
    }

    public void Approve(Guid reviewerId, DateTimeOffset now) => Review(reviewerId, now, VideoStatus.Approved, null);

    public void Reject(Guid reviewerId, DateTimeOffset now, string reason) => Review(reviewerId, now, VideoStatus.Rejected, reason.Trim());

    private void Review(Guid reviewerId, DateTimeOffset now, VideoStatus status, string? reason)
    {
        if (reviewerId == UserId) throw new InvalidOperationException("A video can't be reviewed by its uploader.");
        Status = status;
        RejectionReason = reason;
        ReviewedAt = now;
        ReviewedByUserId = reviewerId;
    }

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
