using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

public sealed record BlockUserRequest(string? Reason);
public sealed record BlockedPersonDto(Guid UserId, string DisplayName, string? AvatarUrl, DateTimeOffset BlockedAt);

/// <summary>
/// People a climber has chosen not to see. Blocking is personal and immediate: no moderator decides it, and the
/// blocked person is never told.
/// </summary>
public sealed class BlockService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<BlockedPersonDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        // Ordered on an anonymous row, not on the DTO: EF Core cannot sort by a member of a type it builds through
        // a constructor, and asking it to left the whole query untranslatable.
        var rows = await db.UserBlocks.AsNoTracking()
            .Where(b => b.BlockerUserId == userId)
            .Join(db.Users, b => b.BlockedUserId, u => u.Id,
                (b, u) => new { u.Id, u.DisplayName, u.AvatarUrl, b.CreatedAt })
            .OrderBy(x => x.DisplayName)
            .ToListAsync(ct);
        return rows.Select(x => new BlockedPersonDto(x.Id, x.DisplayName, x.AvatarUrl, x.CreatedAt)).ToList();
    }

    public async Task BlockAsync(Guid blockedUserId, BlockUserRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        if (blockedUserId == userId) throw new ValidationException("userId", "You can't block yourself.");
        if (!await db.Users.AnyAsync(u => u.Id == blockedUserId, ct)) throw new NotFoundException("User", blockedUserId);

        var reason = Input.Trimmed(r.Reason);
        if (reason.Length > UserBlock.ReasonMaxLength)
            throw new ValidationException("reason", $"Keep the note under {UserBlock.ReasonMaxLength} characters.");

        // Blocking twice is the same as blocking once, and must not fail.
        if (await db.UserBlocks.AnyAsync(b => b.BlockerUserId == userId && b.BlockedUserId == blockedUserId, ct)) return;

        db.UserBlocks.Add(UserBlock.Create(userId, blockedUserId, reason, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task UnblockAsync(Guid blockedUserId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var rows = await db.UserBlocks.Where(b => b.BlockerUserId == userId && b.BlockedUserId == blockedUserId).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.UserBlocks.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
