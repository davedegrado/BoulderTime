using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

public sealed record AcceptLegalRequest(string? Version);
public sealed record LegalAcceptanceDto(string CurrentVersion, string? AcceptedVersion, DateTimeOffset? AcceptedAt, bool AcceptanceNeeded);

/// <summary>
/// Records that a person accepted the terms and the privacy notice. What is stored is the version they saw, so a
/// later change can ask again instead of assuming an old agreement covers new terms.
/// </summary>
public sealed class LegalAcceptanceService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<LegalAcceptanceDto> AcceptAsync(AcceptLegalRequest r, CancellationToken ct = default)
    {
        var version = Input.Trimmed(r.Version);
        // The app must accept the version it actually displayed; anything else would record a meaningless agreement.
        if (version != LegalDocuments.CurrentVersion)
            throw new ValidationException("version", "Reload the app and read the current terms.");

        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        user.AcceptLegal(version, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return new LegalAcceptanceDto(LegalDocuments.CurrentVersion, user.AcceptedLegalVersion, user.AcceptedLegalAt, false);
    }
}
