using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

public sealed record AcceptLegalRequest(string? Version, bool? ConfirmsMinimumAge = null);
public sealed record LegalAcceptanceDto(string CurrentVersion, string? AcceptedVersion, DateTimeOffset? AcceptedAt, bool AcceptanceNeeded);

/// <summary>
/// Records that a person accepted the terms and the privacy notice. What is stored is the version they saw, so a
/// later change can ask again instead of assuming an old agreement covers new terms. Whoever has not yet declared
/// being at least 14 declares it here too (ADR-041).
/// </summary>
public sealed class LegalAcceptanceService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Whether the app must ask before going on: new documents, or no age declaration yet.</summary>
    public static bool IsNeeded(User u) => u.AcceptedLegalVersion != LegalDocuments.CurrentVersion || !u.MinimumAgeConfirmed;

    public async Task<LegalAcceptanceDto> AcceptAsync(AcceptLegalRequest r, CancellationToken ct = default)
    {
        var version = Input.Trimmed(r.Version);
        // The app must accept the version it actually displayed; anything else would record a meaningless agreement.
        if (version != LegalDocuments.CurrentVersion)
            throw new ValidationException("version", "Reload the app and read the current terms.");

        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        if (!user.MinimumAgeConfirmed && r.ConfirmsMinimumAge != true)
            throw new ValidationException("confirmsMinimumAge", "Confirm that you're at least 14.");
        if (r.ConfirmsMinimumAge == true) user.ConfirmMinimumAge(clock.UtcNow);
        user.AcceptLegal(version, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new LegalAcceptanceDto(LegalDocuments.CurrentVersion, user.AcceptedLegalVersion, user.AcceptedLegalAt, false);
    }
}
