namespace BoulderTime.Domain.Users;

/// <summary>
/// One person choosing not to see another. Blocking hides what each writes from the other — comments and videos —
/// without telling the blocked person, and without removing anything for everybody else.
///
/// It is not moderation: reporting asks the gym or BoulderTime to act on content, blocking is a personal setting
/// that needs nobody's approval and takes effect at once.
/// </summary>
public class UserBlock
{
    public const int ReasonMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid BlockerUserId { get; private set; }
    public Guid BlockedUserId { get; private set; }
    /// <summary>Optional, for the blocker's own memory. Nobody else reads it.</summary>
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private UserBlock() { }

    public static UserBlock Create(Guid blockerUserId, Guid blockedUserId, string? reason, DateTimeOffset now)
    {
        if (blockerUserId == blockedUserId) throw new ArgumentException("A person cannot block themselves.", nameof(blockedUserId));
        return new UserBlock
        {
            Id = Guid.NewGuid(), BlockerUserId = blockerUserId, BlockedUserId = blockedUserId,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(), CreatedAt = now,
        };
    }
}
