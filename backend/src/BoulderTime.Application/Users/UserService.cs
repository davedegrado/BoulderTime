using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Staff;
using BoulderTime.Domain.Staff;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

public sealed class UserService(IAppDbContext db, IClock clock)
{
    /// <summary>
    /// Creates the BoulderTime user row for a verified identity on first sight, and keeps the email in sync.
    /// Safe under concurrency: two simultaneous first requests resolve to the same row.
    /// Display name and avatar are user-owned after creation and are never overwritten from the token.
    /// </summary>
    /// <param name="language">Preferred language of the request (Accept-Language), used only when the account is created.</param>
    public async Task<User> EnsureProvisionedAsync(VerifiedIdentity identity, CancellationToken ct = default, string? language = null)
    {
        if (string.IsNullOrWhiteSpace(identity.Email))
            throw new ForbiddenException("This sign-in method didn't provide an email address.", "email_required");

        var existing = await db.Users.FirstOrDefaultAsync(u => u.Id == identity.Subject, ct);
        if (existing is not null)
        {
            var before = existing.Email;
            existing.SyncEmail(identity.Email);
            if (existing.Email != before) await db.SaveChangesAsync(ct);
            return existing;
        }

        var user = User.Provision(identity.Subject, identity.Email, DeriveDisplayName(identity), identity.AvatarUrl);
        user.SetLanguage(Localization.Language.Normalize(language)); // the language the app was used in when signing up
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
            return user;
        }
        catch (UniqueConstraintViolationException)
        {
            // Lost a race with a parallel request for the same new user.
            db.Users.Entry(user).State = EntityState.Detached;
            return await db.Users.FirstAsync(u => u.Id == identity.Subject, ct);
        }
    }

    /// <summary>The signed-in account plus what the shell needs: staff memberships and open invitation count.</summary>
    public async Task<CurrentUserDto> GetAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);
        return await ToDtoAsync(user, ct);
    }

    private async Task<CurrentUserDto> ToDtoAsync(User user, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var gyms = await db.GymStaff.AsNoTracking()
            .Where(s => s.UserId == user.Id)
            .Join(db.Gyms, s => s.GymId, g => g.Id, (s, g) => new { s.Role, g })
            .OrderBy(x => x.g.Name)
            .ToListAsync(ct);
        var pending = await db.StaffInvitations.CountAsync(i => i.Email == user.Email && i.Status == InvitationStatus.Pending && i.ExpiresAt > now, ct);
        return CurrentUserDto.From(user, gyms.Select(x => new MyStaffGymDto(x.g.Id, x.g.Slug, x.g.Name, x.g.City, x.g.LogoUrl, x.Role)).ToList(), pending);
    }

    public async Task<CurrentUserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var name = User.SanitizeDisplayName(request.DisplayName ?? string.Empty);
        new Validator()
            .Check(request.Language is null || Localization.Language.IsSupported(request.Language), "language", "Choose a language BoulderTime speaks.")
            .Check(name.Length >= User.DisplayNameMinLength, "displayName", $"Use at least {User.DisplayNameMinLength} characters.")
            .Check(name.Length <= User.DisplayNameMaxLength, "displayName", $"Keep it to {User.DisplayNameMaxLength} characters or fewer.")
            .ThrowIfInvalid();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("User", userId);
        user.Rename(name);
        if (request.Language is not null) user.SetLanguage(Localization.Language.Normalize(request.Language));
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(user, ct);
    }

    internal static string DeriveDisplayName(VerifiedIdentity identity)
    {
        var candidate = User.SanitizeDisplayName(identity.DisplayName ?? string.Empty);
        if (candidate.Length < User.DisplayNameMinLength)
        {
            var local = (identity.Email ?? string.Empty).Split('@')[0];
            candidate = User.SanitizeDisplayName(local.Replace('.', ' ').Replace('_', ' '));
        }
        if (candidate.Length < User.DisplayNameMinLength) candidate = "Climber";
        return candidate.Length > User.DisplayNameMaxLength ? candidate[..User.DisplayNameMaxLength].TrimEnd() : candidate;
    }
}
