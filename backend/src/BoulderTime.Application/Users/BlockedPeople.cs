using BoulderTime.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

/// <summary>
/// Who the viewer should not see, and who should not see the viewer. Blocking works both ways on purpose: hiding
/// only one direction would let a blocked person keep replying to someone who has stopped reading.
/// </summary>
public sealed class BlockedPeople(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>Empty for a signed-out visitor, who has blocked nobody and is blocked by nobody.</summary>
    public async Task<IReadOnlySet<Guid>> ForViewerAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId) return new HashSet<Guid>();
        var rows = await db.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerUserId == userId || b.BlockedUserId == userId)
            .Select(b => new { b.BlockerUserId, b.BlockedUserId })
            .ToListAsync(ct);
        return rows.Select(b => b.BlockerUserId == userId ? b.BlockedUserId : b.BlockerUserId).ToHashSet();
    }
}
