using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

/// <summary>
/// Whose profile the viewer may open, and whose content they may see: the one place that decides it for every answer
/// that carries profile content — the profile, the history, the list of friends (ADR-061, ADR-063).
/// </summary>
public sealed class ProfileAccess(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>
    /// The person, if their profile opens for the viewer. A private profile still opens (from a leaderboard, say):
    /// name and photo, its content only for whoever may see it (<see cref="CanSeeContentAsync"/>). A suspended account
    /// has no public face (ADR-037), nor has one waiting to be erased: deleting an account takes it out of the app at
    /// once. Their owners still see their own.
    /// </summary>
    public async Task<User> RequireVisibleAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        if (currentUser.UserId == userId) return user;
        if (user.SuspendedAt is not null || user.DeletionRequestedAt is not null) throw new NotFoundException("User", userId);
        return user;
    }

    /// <summary>
    /// Whether the viewer may see the content of this profile: when it is public, their own, or a friend's — an
    /// accepted friendship; a pending request is never enough.
    /// </summary>
    public async Task<bool> CanSeeContentAsync(User user, CancellationToken ct = default)
    {
        if (user.ProfileVisibility != ProfileVisibility.Private) return true;
        if (currentUser.UserId is not { } viewer) return false;
        if (viewer == user.Id) return true;
        var (low, high) = Friendship.Pair(viewer, user.Id);
        return await db.Friendships.AsNoTracking()
            .AnyAsync(f => f.UserLowId == low && f.UserHighId == high && f.Status == FriendshipStatus.Accepted, ct);
    }
}
