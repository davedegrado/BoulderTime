using BoulderTime.Application.Staff;
using BoulderTime.Domain.Users;

namespace BoulderTime.Application.Users;

/// <summary>Claims extracted from a verified Supabase access token.</summary>
public sealed record VerifiedIdentity(Guid Subject, string? Email, string? DisplayName, string? AvatarUrl);

public sealed record CurrentUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool IsPlatformAdmin,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MyStaffGymDto> StaffGyms,
    int PendingInvitations,
    string Language,
    /// <summary>The climber chose to stay out of leaderboards.</summary>
    bool LeaderboardOptOut = false,
    /// <summary>
    /// BoulderTime excluded this climber from leaderboards. Shown only to the person themselves, so that a missing
    /// name reads as a decision they can ask about rather than as a broken app.
    /// </summary>
    bool LeaderboardExcluded = false,
    /// <summary>The version of the terms and privacy notice this person accepted, if any.</summary>
    string? AcceptedLegalVersion = null,
    /// <summary>True when the documents changed (or were never accepted) and the app must ask before going on.</summary>
    bool LegalAcceptanceNeeded = false)
{
    public static CurrentUserDto From(User u, IReadOnlyList<MyStaffGymDto>? staffGyms = null, int pendingInvitations = 0) =>
        new(u.Id, u.Email, u.DisplayName, u.AvatarUrl, u.IsPlatformAdmin, u.CreatedAt, staffGyms ?? [], pendingInvitations, u.Language,
            u.LeaderboardOptOut, u.LeaderboardExcludedAt is not null,
            u.AcceptedLegalVersion, u.AcceptedLegalVersion != LegalDocuments.CurrentVersion);
}

public sealed record UpdateProfileRequest(string? DisplayName, string? Language = null);
