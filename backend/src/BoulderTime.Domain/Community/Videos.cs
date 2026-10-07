using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Community;

/// <summary>
/// The gym's official beta for a boulder. One per boulder; staff can replace it. No moderation.
///
/// It is either an uploaded video (<see cref="StoragePath"/>) or a link to one already published elsewhere —
/// Instagram, YouTube and the like (<see cref="ExternalUrl"/>) — never both. A link costs the gym no storage and no
/// upload, which is why many gyms want it: the video is already on their own channel.
/// </summary>
public class BoulderBeta : IAuditable
{
    public const int CaptionMaxLength = 300;
    public const int ExternalUrlMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    /// <summary>Empty when the beta is a link.</summary>
    public string StoragePath { get; private set; } = string.Empty;
    /// <summary>Poster frame captured by the uploading device; optional (some formats can't be decoded in the browser).</summary>
    public string? ThumbnailPath { get; private set; }
    /// <summary>Set only for a linked beta. The video stays where it was published; we keep the address.</summary>
    public string? ExternalUrl { get; private set; }
    public string? Caption { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsLink => ExternalUrl is { Length: > 0 };

    private BoulderBeta() { }

    public static BoulderBeta Create(Guid boulderId, Guid userId, string storagePath, string? thumbnailPath, string? caption) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UploadedByUserId = userId, StoragePath = storagePath, ThumbnailPath = Clean(thumbnailPath), Caption = Clean(caption) };

    public static BoulderBeta CreateLink(Guid boulderId, Guid userId, string url, string? caption) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UploadedByUserId = userId, StoragePath = string.Empty, ExternalUrl = url, Caption = Clean(caption) };

    /// <returns>Storage paths that are no longer referenced (old video and/or thumbnail), so they can be deleted.</returns>
    public IReadOnlyList<string> Replace(Guid userId, string storagePath, string? thumbnailPath, string? caption)
    {
        var obsolete = new List<string>();
        if (StoragePath != storagePath)
        {
            if (StoragePath.Length > 0) obsolete.Add(StoragePath);
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
        ExternalUrl = null;
        Caption = Clean(caption);
        return obsolete;
    }

    /// <summary>
    /// Turn this beta into a link. An uploaded video it replaces is released, so swapping an upload for a link gives
    /// the gym its storage — and its allowance — back.
    /// </summary>
    /// <returns>Storage paths that are no longer referenced, so they can be deleted.</returns>
    public IReadOnlyList<string> ReplaceWithLink(Guid userId, string url, string? caption)
    {
        var obsolete = Released();
        UploadedByUserId = userId;
        StoragePath = string.Empty;
        ThumbnailPath = null;
        ExternalUrl = url;
        Caption = Clean(caption);
        return obsolete;
    }

    /// <summary>Every storage path this beta currently holds, for deletion when it goes away.</summary>
    public IReadOnlyList<string> Released()
    {
        var paths = new List<string>();
        if (StoragePath.Length > 0) paths.Add(StoragePath);
        if (ThumbnailPath is not null) paths.Add(ThumbnailPath);
        return paths;
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
