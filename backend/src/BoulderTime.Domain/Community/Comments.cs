using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Community;

public enum CommentStatus
{
    Visible = 0,
    /// <summary>Hidden by moderation. Kept so reports and audit still point at real content.</summary>
    Hidden = 1,
}

/// <summary>A flat comment on a boulder. Authors can edit or delete their own; gym staff can hide.</summary>
public class Comment : IAuditable
{
    public const int MaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid UserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public CommentStatus Status { get; private set; } = CommentStatus.Visible;
    public DateTimeOffset? EditedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private Comment() { }

    public static Comment Create(Guid boulderId, Guid userId, string content) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UserId = userId, Content = content.Trim() };

    public void Edit(string content, DateTimeOffset now)
    {
        Content = content.Trim();
        EditedAt = now;
    }

    public void Hide() => Status = CommentStatus.Hidden;
    public void Unhide() => Status = CommentStatus.Visible;
}

public class CommentLike
{
    public Guid CommentId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CommentLike() { }

    public static CommentLike Create(Guid commentId, Guid userId, DateTimeOffset now) =>
        new() { CommentId = commentId, UserId = userId, CreatedAt = now };
}
