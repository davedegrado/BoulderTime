using BoulderTime.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

/// <summary>
/// Who the viewer should not see, and who should not see the viewer. Blocking works both ways on purpose: hiding
/// only one direction would let a blocked person keep replying to someone who has stopped reading.
/// </summary>
public sealed class BlockedPeople(IAppDbContext db, ICurrentUser currentUser)
{
    /// <summary>
    /// Everyone hidden from the viewer: people blocked in either direction, and suspended accounts, which are hidden
    /// from everybody — signed out or not (ADR-037). A <see cref="List{T}"/> rather than a set, and declared as one:
    /// these ids go into <c>Contains</c> inside database queries, and EF Core translates that for a list.
    /// </summary>
    public async Task<List<Guid>> ForViewerAsync(CancellationToken ct = default)
    {
        var hidden = await db.Users.AsNoTracking().Where(u => u.SuspendedAt != null).Select(u => u.Id).ToListAsync(ct);
        if (currentUser.UserId is not { } userId) return hidden;
        var rows = await db.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerUserId == userId || b.BlockedUserId == userId)
            .Select(b => new { b.BlockerUserId, b.BlockedUserId })
            .ToListAsync(ct);
        hidden.AddRange(rows.Select(b => b.BlockerUserId == userId ? b.BlockedUserId : b.BlockerUserId));
        return hidden.Distinct().ToList();
    }

    /// <summary>
    /// Whether the viewer blocked this person — one direction only. The two-way hiding above must never answer this
    /// question: it would tell the blocked person they were blocked, which is exactly what blocking does not do.
    /// </summary>
    public async Task<bool> ViewerBlockedAsync(Guid otherUserId, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } userId) return false;
        return await db.UserBlocks.AsNoTracking()
            .AnyAsync(b => b.BlockerUserId == userId && b.BlockedUserId == otherUserId, ct);
    }
}
