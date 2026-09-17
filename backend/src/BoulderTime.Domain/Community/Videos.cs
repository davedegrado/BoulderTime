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
    public string? Caption { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private BoulderBeta() { }

    public static BoulderBeta Create(Guid boulderId, Guid userId, string storagePath, string? caption) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UploadedByUserId = userId, StoragePath = storagePath, Caption = Clean(caption) };

    /// <returns>The previous storage path when the video file changed, so the old object can be deleted.</returns>
    public string? Replace(Guid userId, string storagePath, string? caption)
    {
        var previous = StoragePath != storagePath ? StoragePath : null;
        UploadedByUserId = userId;
        StoragePath = storagePath;
        Caption = Clean(caption);
        return previous;
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
    public string? Caption { get; private set; }
    public VideoStatus Status { get; private set; } = VideoStatus.Pending;
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private BoulderVideo() { }

    public static BoulderVideo Submit(Guid boulderId, Guid userId, string storagePath, string? caption) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UserId = userId, StoragePath = storagePath, Caption = Clean(caption) };

    /// <summary>Changing caption or file resets review. Returns the replaced storage path, if the file changed.</summary>
    public string? Modify(string? caption, string? storagePath)
    {
        string? previous = null;
        if (storagePath is not null && storagePath != StoragePath)
        {
            previous = StoragePath;
            StoragePath = storagePath;
        }
        if (caption is not null) Caption = Clean(caption);
        Status = VideoStatus.Pending;
        RejectionReason = null;
        ReviewedAt = null;
        ReviewedByUserId = null;
        return previous;
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
