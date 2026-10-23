using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

/// <summary>The viewer's friendship with someone, from the viewer's side.</summary>
public enum FriendshipState
{
    None = 0,
    /// <summary>The viewer asked; the other hasn't answered.</summary>
    Outgoing = 1,
    /// <summary>The other asked the viewer.</summary>
    Incoming = 2,
    Friends = 3,
}

public sealed record FriendshipDto(FriendshipState State);
public sealed record FriendRequestDto(PersonDto Person, DateTimeOffset RequestedAt);
public sealed record FriendDto(PersonDto Person, DateTimeOffset Since);

/// <summary>
/// Friendships between climbers (ADR-063): one asks, the other accepts; either can end it, and asking someone who
/// already asked you makes you friends. A friend sees the content of a private profile (ProfileAccess); leaderboards
/// and the other lists don't change. Blocking ends a friendship and is never revealed: a request to someone who blocked
/// you looks sent like any other, and they never see it while the block lasts.
/// </summary>
public sealed class FriendshipService(IAppDbContext db, ICurrentUser currentUser, IClock clock, BlockedPeople blocked, ProfileAccess access)
{
    /// <summary>Requests one person may send in a day: plenty for real use, too few to spam with.</summary>
    public const int RequestsPerDay = 30;

    /// <summary>The viewer's friendship with this person; null for oneself and when signed out.</summary>
    public async Task<FriendshipState?> StateWithAsync(Guid otherId, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } me || me == otherId) return null;
        // Someone the viewer blocked asks nothing of them while the block lasts.
        if (await blocked.ViewerBlockedAsync(otherId, ct)) return FriendshipState.None;
        return Of(await PairAsync(me, otherId, tracked: false, ct), me);
    }

    public async Task<FriendshipDto> RequestAsync(Guid otherId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        if (otherId == me) throw new ValidationException("userId", "You can't add yourself as a friend.");
        await access.RequireVisibleAsync(otherId, ct);
        if (await blocked.ViewerBlockedAsync(otherId, ct)) throw new ValidationException("userId", "Unblock this person to add them as a friend.");

        var row = await PairAsync(me, otherId, tracked: true, ct);
        if (row is not null)
        {
            // Asking again changes nothing; asking someone who asked you is saying yes.
            if (row.Status == FriendshipStatus.Pending && row.RequesterId != me)
            {
                row.Accept(clock.UtcNow);
                await db.SaveChangesAsync(ct);
            }
            return new FriendshipDto(Of(row, me));
        }

        var since = clock.UtcNow.AddDays(-1);
        if (await db.Friendships.CountAsync(f => f.RequesterId == me && f.CreatedAt >= since, ct) >= RequestsPerDay)
            throw new ConflictException("You've sent a lot of friend requests today. Try again tomorrow.", "too_many_friend_requests");

        var request = Friendship.Request(me, otherId, clock.UtcNow);
        db.Friendships.Add(request);
        await db.SaveChangesAsync(ct);
        return new FriendshipDto(FriendshipState.Outgoing);
    }

    public async Task<FriendshipDto> AcceptAsync(Guid otherId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var row = await PairAsync(me, otherId, tracked: true, ct);
        if (row is { Status: FriendshipStatus.Accepted }) return new FriendshipDto(FriendshipState.Friends);
        // Only a request the other made, and only from someone the viewer still sees.
        if (row is null || row.RequesterId == me || await blocked.ViewerBlockedAsync(otherId, ct)) throw new NotFoundException("Friend request", otherId);
        await access.RequireVisibleAsync(otherId, ct);
        row.Accept(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new FriendshipDto(FriendshipState.Friends);
    }

    /// <summary>Cancels the viewer's request, declines theirs, or ends the friendship. Nobody is told.</summary>
    public async Task<FriendshipDto> RemoveAsync(Guid otherId, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var row = await PairAsync(me, otherId, tracked: true, ct);
        if (row is not null)
        {
            db.Friendships.Remove(row);
            await db.SaveChangesAsync(ct);
        }
        return new FriendshipDto(FriendshipState.None);
    }

    /// <summary>Who asked the viewer to be friends, newest first, leaving out people hidden from them.</summary>
    public async Task<IReadOnlyList<FriendRequestDto>> IncomingAsync(CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var hidden = await blocked.ForViewerAsync(ct);
        var rows = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending && (f.UserLowId == me || f.UserHighId == me) && f.RequesterId != me)
            .Where(f => !hidden.Contains(f.RequesterId))
            .Join(db.Users, f => f.RequesterId, u => u.Id, (f, u) => new { u.Id, u.DisplayName, u.AvatarUrl, f.CreatedAt })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(x => new FriendRequestDto(new PersonDto(x.Id, x.DisplayName, x.AvatarUrl), x.CreatedAt)).ToList();
    }

    /// <summary>
    /// A person's friends, for whoever may see the content of their profile: anyone for a public one, its owner and
    /// friends for a private one; others get an empty page. People hidden from the viewer are left out.
    /// </summary>
    public async Task<PagedResult<FriendDto>> FriendsOfAsync(Guid userId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var user = await access.RequireVisibleAsync(userId, ct);
        var (p, size) = Paging.Normalize(page, pageSize, 30);
        if (!await access.CanSeeContentAsync(user, ct)) return new PagedResult<FriendDto>([], p, size, 0);
        var hidden = await blocked.ForViewerAsync(ct);
        var q = db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.UserLowId == userId || f.UserHighId == userId))
            .Select(f => new { Other = f.UserLowId == userId ? f.UserHighId : f.UserLowId, Since = f.AcceptedAt ?? f.CreatedAt })
            .Where(x => !hidden.Contains(x.Other))
            .Join(db.Users, x => x.Other, u => u.Id, (x, u) => new { u.Id, u.DisplayName, u.AvatarUrl, x.Since });
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(x => x.DisplayName).ThenBy(x => x.Id).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<FriendDto>(rows.Select(x => new FriendDto(new PersonDto(x.Id, x.DisplayName, x.AvatarUrl), x.Since)).ToList(), p, size, total);
    }

    private async Task<Friendship?> PairAsync(Guid a, Guid b, bool tracked, CancellationToken ct)
    {
        var (low, high) = Friendship.Pair(a, b);
        var q = tracked ? db.Friendships : db.Friendships.AsNoTracking();
        return await q.FirstOrDefaultAsync(f => f.UserLowId == low && f.UserHighId == high, ct);
    }

    private static FriendshipState Of(Friendship? f, Guid me) =>
        f is null ? FriendshipState.None
        : f.Status == FriendshipStatus.Accepted ? FriendshipState.Friends
        : f.RequesterId == me ? FriendshipState.Outgoing
        : FriendshipState.Incoming;
}
