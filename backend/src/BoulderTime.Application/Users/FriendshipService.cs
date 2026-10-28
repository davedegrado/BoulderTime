using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Badges;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Localization;
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
/// <param name="MutualFriends">Friends the viewer and this person have in common (1.1; only on the requests received).</param>
public sealed record FriendRequestDto(PersonDto Person, DateTimeOffset RequestedAt, int? MutualFriends = null);
/// <param name="Activity">What the friend climbed lately (1.1): null when they have no send, or when the viewer may not
/// see their profile's content.</param>
public sealed record FriendDto(PersonDto Person, DateTimeOffset Since, FriendActivityDto? Activity = null);
/// <summary>
/// The hardest boulder a friend sent in their latest session (the day of their latest send, which <paramref name="LastSendAt"/>
/// is), and how many boulders they completed this month.
/// </summary>
/// <param name="Grade">The boulder's main grade, as on its card; null when it has none.</param>
public sealed record FriendActivityDto(DateTimeOffset LastSendAt, Guid BoulderId, string GymName, BoulderGradeDto? Grade,
    Domain.Boulders.HoldColor HoldColor, int SentThisMonth);
/// <summary>How a list of friends is ordered: by name (as always), or the most recently active first.</summary>
public enum FriendSort { Name = 0, Activity = 1 }
/// <summary>Someone found by name, with the viewer's friendship with them, so the list can offer the right button.</summary>
public sealed record PersonResultDto(PersonDto Person, FriendshipState Friendship);

/// <summary>
/// Friendships between climbers (ADR-063): one asks, the other accepts; either can end it, and asking someone who
/// already asked you makes you friends. A friend sees the content of a private profile (ProfileAccess); leaderboards
/// and the other lists don't change. Blocking ends a friendship and is never revealed: a request to someone who blocked
/// you looks sent like any other, and they never see it while the block lasts.
/// </summary>
public sealed class FriendshipService(IAppDbContext db, ICurrentUser currentUser, IClock clock, BlockedPeople blocked, ProfileAccess access, IPushQueue push, BoulderReader reader)
{
    /// <summary>Requests one person may send in a day: plenty for real use, too few to spam with.</summary>
    public const int RequestsPerDay = 30;

    /// <summary>The first store app version that has friends (ADR-063).</summary>
    public const string FriendsSinceAppVersion = "1.1.0";

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
        await NotifyRequestAsync(me, otherId, ct);
        return new FriendshipDto(FriendshipState.Outgoing);
    }

    /// <summary>
    /// A phone notification for a new request, to whoever has notifications on and hasn't turned friend requests off,
    /// on the devices whose app has friends.
    /// Push only, no line in the notification list: installed apps can't draw a kind of notification they don't know,
    /// and the request already waits on the profile and in Notifications. Someone who blocked the requester hears
    /// nothing, as the request stays out of their sight. Asking again after cancelling replaces the notification on
    /// the phone instead of adding one (same tag).
    /// </summary>
    private async Task NotifyRequestAsync(Guid requesterId, Guid addresseeId, CancellationToken ct)
    {
        if (await db.UserBlocks.AsNoTracking().AnyAsync(b => b.BlockerUserId == addresseeId && b.BlockedUserId == requesterId, ct)) return;
        var settings = await db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == addresseeId, ct);
        if (settings is { FriendRequests: false }) return;
        var requesterName = await db.Users.AsNoTracking().Where(u => u.Id == requesterId).Select(u => u.DisplayName).FirstAsync(ct);
        var language = await db.Users.AsNoTracking().Where(u => u.Id == addresseeId).Select(u => u.Language).FirstAsync(ct);
        var text = NotificationTexts.FriendRequest(language, requesterName);
        // Only where the request can be answered: the website and store apps from 1.1. A 1.0 app has no friends, and
        // would open a profile without Accept.
        push.Enqueue(addresseeId, new PushMessage(text.Title, text.Body ?? "", $"/users/{requesterId}", $"friend-request:{requesterId}",
            MinimumAppVersion: FriendsSinceAppVersion));
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
        // Friends in common help to tell who is asking: a count only, never who they are.
        var mine = await FriendIdsAsync(me, ct);
        var askers = rows.Select(x => x.Id).ToList();
        var theirs = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (askers.Contains(f.UserLowId) || askers.Contains(f.UserHighId)))
            .Select(f => new { f.UserLowId, f.UserHighId }).ToListAsync(ct);
        int Mutual(Guid asker) => theirs.Count(f => (f.UserLowId == asker && mine.Contains(f.UserHighId) && !hidden.Contains(f.UserHighId))
            || (f.UserHighId == asker && mine.Contains(f.UserLowId) && !hidden.Contains(f.UserLowId)));
        return rows.Select(x => new FriendRequestDto(new PersonDto(x.Id, x.DisplayName, x.AvatarUrl), x.CreatedAt, Mutual(x.Id))).ToList();
    }

    private async Task<HashSet<Guid>> FriendIdsAsync(Guid userId, CancellationToken ct) =>
        (await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.UserLowId == userId || f.UserHighId == userId))
            .Select(f => f.UserLowId == userId ? f.UserHighId : f.UserLowId).ToListAsync(ct)).ToHashSet();

    /// <summary>
    /// The requests the viewer sent that haven't been answered, newest first, leaving out suspended accounts and
    /// accounts being erased. Blocking already ends a request either way, so there is nothing else to hide; a request
    /// sent to someone who blocked the viewer stays here, as it looks sent like any other.
    /// </summary>
    public async Task<IReadOnlyList<FriendRequestDto>> OutgoingAsync(CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var suspendedOrLeaving = await db.Users.AsNoTracking()
            .Where(u => u.SuspendedAt != null || u.DeletionRequestedAt != null).Select(u => u.Id).ToListAsync(ct);
        var rows = await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == me)
            .Select(f => new { Other = f.UserLowId == me ? f.UserHighId : f.UserLowId, f.CreatedAt })
            .Where(x => !suspendedOrLeaving.Contains(x.Other))
            .Join(db.Users, x => x.Other, u => u.Id, (x, u) => new { u.Id, u.DisplayName, u.AvatarUrl, x.CreatedAt })
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(x => new FriendRequestDto(new PersonDto(x.Id, x.DisplayName, x.AvatarUrl), x.CreatedAt)).ToList();
    }

    /// <summary>Shortest text a search answers: one letter would list half the app.</summary>
    public const int SearchMinLength = 2;
    public const int SearchMaxResults = 20;

    /// <summary>
    /// Climbers whose display name contains the text, to add them as friends: by name only, never by email. Names that
    /// start with the text first. Leaves out the viewer, people hidden from them (blocked either way), suspended
    /// accounts and accounts being erased; a private profile is found too, as its name and photo are public anyway.
    /// </summary>
    public async Task<IReadOnlyList<PersonResultDto>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var me = currentUser.RequireUserId();
        var text = Input.Trimmed(query);
        if (text.Length < SearchMinLength) return [];
        if (text.Length > Domain.Users.User.DisplayNameMaxLength) text = text[..Domain.Users.User.DisplayNameMaxLength];
        var lower = text.ToLowerInvariant();
        var contains = Input.LikeContains(lower);
        var startsWith = Input.LikeContains(lower)[1..]; // "%text%" without the leading %
        var hidden = await blocked.ForViewerAsync(ct);
        var people = await db.Users.AsNoTracking()
            .Where(u => u.Id != me && u.SuspendedAt == null && u.DeletionRequestedAt == null && !hidden.Contains(u.Id))
            .Where(u => EF.Functions.Like(u.DisplayName.ToLower(), contains, @"\"))
            .OrderBy(u => EF.Functions.Like(u.DisplayName.ToLower(), startsWith, @"\") ? 0 : 1)
            .ThenBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Take(SearchMaxResults)
            .Select(u => new { u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();
        var pairs = await db.Friendships.AsNoTracking()
            .Where(f => (f.UserLowId == me && ids.Contains(f.UserHighId)) || (f.UserHighId == me && ids.Contains(f.UserLowId)))
            .ToListAsync(ct);
        return people.Select(p => new PersonResultDto(new PersonDto(p.Id, p.DisplayName, p.AvatarUrl),
            Of(pairs.FirstOrDefault(f => f.Other(me) == p.Id), me))).ToList();
    }

    /// <summary>
    /// A person's friends, for whoever may see the content of their profile: anyone for a public one, its owner and
    /// friends for a private one; others get an empty page. People hidden from the viewer are left out. By name, as
    /// always, or with <see cref="FriendSort.Activity"/> the most recently active first. Each friend's latest session comes
    /// only when the viewer may see that friend's own profile content (their profile is public, or they are the viewer
    /// or the viewer's friend), and only that activity orders the list, so the order never tells more than the rows.
    /// </summary>
    public async Task<PagedResult<FriendDto>> FriendsOfAsync(Guid userId, int? page, int? pageSize, CancellationToken ct = default, FriendSort sort = FriendSort.Name)
    {
        var user = await access.RequireVisibleAsync(userId, ct);
        var (p, size) = Paging.Normalize(page, pageSize, 30);
        if (!await access.CanSeeContentAsync(user, ct)) return new PagedResult<FriendDto>([], p, size, 0);
        var hidden = await blocked.ForViewerAsync(ct);
        var me = currentUser.UserId;
        var myFriends = me is { } m ? await FriendIdsAsync(m, ct) : [];
        var q = db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.UserLowId == userId || f.UserHighId == userId))
            .Select(f => new { Other = f.UserLowId == userId ? f.UserHighId : f.UserLowId, Since = f.AcceptedAt ?? f.CreatedAt })
            .Where(x => !hidden.Contains(x.Other))
            .Join(db.Users, x => x.Other, u => u.Id, (x, u) => new
            {
                u.Id, u.DisplayName, u.AvatarUrl, x.Since,
                Open = u.ProfileVisibility != ProfileVisibility.Private || u.Id == me || myFriends.Contains(u.Id),
            })
            .Select(x => new
            {
                x.Id, x.DisplayName, x.AvatarUrl, x.Since, x.Open,
                LastSend = x.Open ? db.BoulderAttempts.Where(a => a.UserId == x.Id && a.Completed).Max(a => a.CompletedAt) : null,
            });
        var total = await q.CountAsync(ct);
        var ordered = sort == FriendSort.Activity
            ? q.OrderBy(x => x.LastSend == null).ThenByDescending(x => x.LastSend).ThenBy(x => x.DisplayName).ThenBy(x => x.Id)
            : q.OrderBy(x => x.DisplayName).ThenBy(x => x.Id);
        var rows = await ordered.Skip((p - 1) * size).Take(size).ToListAsync(ct);
        var activity = await ActivityOfAsync(rows.Where(r => r.Open && r.LastSend != null).Select(r => r.Id).ToList(), ct);
        return new PagedResult<FriendDto>(rows.Select(x => new FriendDto(new PersonDto(x.Id, x.DisplayName, x.AvatarUrl), x.Since,
            activity.GetValueOrDefault(x.Id))).ToList(), p, size, total);
    }

    /// <summary>
    /// For each person, the hardest boulder of their latest session (the Italian day of their latest send: its main
    /// grade, holds and gym, the latest of them on a tie or when none is graded) and their sends this month.
    /// </summary>
    private async Task<Dictionary<Guid, FriendActivityDto>> ActivityOfAsync(List<Guid> people, CancellationToken ct)
    {
        if (people.Count == 0) return [];
        var now = clock.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var sends = await db.BoulderAttempts.AsNoTracking().Where(a => people.Contains(a.UserId) && a.Completed)
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Last = g.Max(a => a.CompletedAt),
                ThisMonth = g.Count(a => a.CompletedAt >= monthStart),
            })
            .ToListAsync(ct);
        // The sends of the last day and a half before each one's latest: their whole last day, whatever the time zone.
        var recent = await db.BoulderAttempts.AsNoTracking()
            .Where(a => people.Contains(a.UserId) && a.Completed && a.CompletedAt != null
                && a.CompletedAt >= db.BoulderAttempts.Where(b => b.UserId == a.UserId && b.Completed).Max(b => b.CompletedAt) - SessionWindow)
            .Select(a => new { a.UserId, a.BoulderId, CompletedAt = a.CompletedAt!.Value })
            .ToListAsync(ct);
        var boulderIds = recent.Select(s => s.BoulderId).Distinct().ToList();
        var boulders = await db.Boulders.AsNoTracking().Where(b => boulderIds.Contains(b.Id)).ToListAsync(ct);
        var summaries = (await reader.SummariesAsync(boulders, ct)).ToDictionary(b => b.Id);
        var result = new Dictionary<Guid, FriendActivityDto>();
        foreach (var s in sends.Where(s => s.Last != null))
        {
            var day = ItalianTime.Day(s.Last!.Value);
            var best = recent.Where(r => r.UserId == s.UserId && ItalianTime.Day(r.CompletedAt) == day && summaries.ContainsKey(r.BoulderId))
                .Select(r => (Boulder: summaries[r.BoulderId], r.CompletedAt))
                .OrderByDescending(x => x.Boulder.Grades.FirstOrDefault() is not null)
                .ThenByDescending(x => x.Boulder.Grades.FirstOrDefault()?.Rank)
                .ThenByDescending(x => x.CompletedAt)
                .Select(x => x.Boulder)
                .FirstOrDefault();
            if (best is not null)
                result[s.UserId] = new FriendActivityDto(s.Last.Value, best.Id, best.GymName, best.Grades.FirstOrDefault(), best.HoldColor, s.ThisMonth);
        }
        return result;
    }

    private static readonly TimeSpan SessionWindow = TimeSpan.FromHours(36);

    /// <summary>
    /// How many friends the person has, leaving out people hidden from the viewer: the same people the list of friends
    /// shows. The caller decides whether the viewer may see it at all.
    /// </summary>
    public async Task<int> CountOfAsync(Guid userId, CancellationToken ct = default)
    {
        var hidden = await blocked.ForViewerAsync(ct);
        return await db.Friendships.AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted && (f.UserLowId == userId || f.UserHighId == userId))
            .Select(f => f.UserLowId == userId ? f.UserHighId : f.UserLowId)
            .CountAsync(other => !hidden.Contains(other), ct);
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
