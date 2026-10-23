namespace BoulderTime.Domain.Users;

public enum FriendshipStatus
{
    /// <summary>Asked, not yet answered.</summary>
    Pending = 0,
    /// <summary>Both agreed.</summary>
    Accepted = 1,
}

/// <summary>
/// Two climbers who agreed to be friends, or one asking the other (ADR-063). One row per pair, whoever asked:
/// <see cref="UserLowId"/> and <see cref="UserHighId"/> are the two people in a fixed order, so a pair can't be
/// stored twice (a unique index on them), and <see cref="RequesterId"/> says who asked. Declining, cancelling and
/// ending a friendship all delete the row: there is no history of who said no.
/// </summary>
public class Friendship
{
    public Guid Id { get; private set; }
    public Guid UserLowId { get; private set; }
    public Guid UserHighId { get; private set; }
    public Guid RequesterId { get; private set; }
    public FriendshipStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    private Friendship() { }

    public Guid AddresseeId => RequesterId == UserLowId ? UserHighId : UserLowId;
    public Guid Other(Guid userId) => userId == UserLowId ? UserHighId : UserLowId;

    /// <summary>The two people of a pair in the order they are stored.</summary>
    public static (Guid Low, Guid High) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    public static Friendship Request(Guid requesterId, Guid addresseeId, DateTimeOffset now)
    {
        if (requesterId == addresseeId) throw new ArgumentException("A person cannot befriend themselves.", nameof(addresseeId));
        var (low, high) = Pair(requesterId, addresseeId);
        return new Friendship
        {
            Id = Guid.NewGuid(), UserLowId = low, UserHighId = high, RequesterId = requesterId,
            Status = FriendshipStatus.Pending, CreatedAt = now,
        };
    }

    public void Accept(DateTimeOffset now)
    {
        if (Status == FriendshipStatus.Accepted) return;
        Status = FriendshipStatus.Accepted;
        AcceptedAt = now;
    }
}
