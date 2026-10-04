using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

public sealed record DeleteAccountRequest(string? ConfirmEmail);
public sealed record DeletionStatusDto(bool PendingDeletion, DateTimeOffset? RequestedAt, DateTimeOffset? ErasedAfter);

/// <summary>
/// Deleting an account. The person asks, the account disappears from the app at once, and the data is erased a week
/// later: long enough to undo a decision taken in anger, short enough to be an honest promise.
/// </summary>
public sealed class AccountDeletionService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    /// <summary>How long a deleted account can still be brought back.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    public async Task<DeletionStatusDto> StatusAsync(CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        return Status(user.DeletionRequestedAt);
    }

    /// <summary>
    /// Asks for deletion. The email has to be typed out: this is irreversible after a week, and a plain
    /// "are you sure?" is tapped by accident.
    /// </summary>
    public async Task<DeletionStatusDto> RequestAsync(DeleteAccountRequest r, CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        var typed = Input.Trimmed(r.ConfirmEmail);
        if (!string.Equals(typed, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("confirmEmail", "Type your email address exactly to confirm.");

        // An owner who leaves would strand their gym: the rest of the staff must be able to carry on.
        var orphaned = await db.GymStaff.AsNoTracking()
            .Where(m => m.UserId == user.Id && m.Role == Domain.Staff.GymRole.Owner)
            .Where(m => !db.GymStaff.Any(o => o.GymId == m.GymId && o.UserId != user.Id && o.Role == Domain.Staff.GymRole.Owner))
            .CountAsync(ct);
        if (orphaned > 0)
            throw new ConflictException("You are the only owner of a gym. Make someone else an owner first, or write to support@bouldertime.com.", "sole_owner");

        user.RequestDeletion(clock.UtcNow);
        // Hidden from leaderboards straight away: being deleted shouldn't leave you ranked.
        user.SetLeaderboardOptOut(true);
        await db.SaveChangesAsync(ct);
        return Status(user.DeletionRequestedAt);
    }

    /// <summary>Brings the account back, within the week.</summary>
    public async Task<DeletionStatusDto> CancelAsync(CancellationToken ct = default)
    {
        var user = await CurrentAsync(ct);
        user.CancelDeletion();
        user.SetLeaderboardOptOut(false);
        await db.SaveChangesAsync(ct);
        return Status(null);
    }

    private DeletionStatusDto Status(DateTimeOffset? requestedAt) =>
        new(requestedAt is not null, requestedAt, requestedAt?.Add(GracePeriod));

    private async Task<Domain.Users.User> CurrentAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
    }
}
