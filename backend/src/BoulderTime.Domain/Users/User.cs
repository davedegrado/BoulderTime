using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Users;

/// <summary>
/// A BoulderTime account. One account can simultaneously be a climber, staff at several gyms
/// and a platform administrator — roles are relationships (e.g. GymStaff), never separate accounts.
/// </summary>
public class User : IAuditable
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 40;
    public const int EmailMaxLength = 320;

    /// <summary>Equal to the Supabase Auth user id (JWT <c>sub</c>). Not a database FK to auth.users by design.</summary>
    public Guid Id { get; private set; }

    /// <summary>Lower-cased copy of the identity-provider email, kept in sync on sign-in.</summary>
    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    /// <summary>Storage path when the avatar was uploaded to BoulderTime (null for provider avatars such as Google).</summary>
    public string? AvatarPath { get; private set; }
    /// <summary>Language for the app and for notifications addressed to this user ("it" or "en").</summary>
    public string Language { get; private set; } = "it";

    /// <summary>
    /// Platform-wide administrator. Set only via the server-side CLI — never from token claims or client input.
    /// </summary>
    public bool IsPlatformAdmin { get; private set; }

    /// <summary>The person chose not to appear in leaderboards. Their own activity and history stay untouched.</summary>
    public bool LeaderboardOptOut { get; private set; }

    /// <summary>
    /// When the person asked to delete their account. The account is hidden from everyone immediately but kept for
    /// a week, so a decision taken in a bad moment can be undone; after that it is erased for good.
    /// </summary>
    public DateTimeOffset? DeletionRequestedAt { get; private set; }

    /// <summary>
    /// Which version of the terms and privacy notice this person accepted, and when. The version matters as much as
    /// the date: without it we would know that someone agreed, but not to what.
    /// </summary>
    public string? AcceptedLegalVersion { get; private set; }
    public DateTimeOffset? AcceptedLegalAt { get; private set; }

    public bool IsPendingDeletion => DeletionRequestedAt is not null;

    /// <summary>
    /// Set by a BoulderTime administrator when sends look implausible. Kept apart from <see cref="LeaderboardOptOut"/>
    /// so that clearing one never clears the other, and so the person can still be told why they are missing.
    /// </summary>
    public DateTimeOffset? LeaderboardExcludedAt { get; private set; }
    public Guid? LeaderboardExcludedByUserId { get; private set; }

    public bool AppearsInLeaderboards => !LeaderboardOptOut && LeaderboardExcludedAt is null;

    /// <summary>Profiles are public by default; the field exists so privacy settings can be added without a remodel.</summary>
    public ProfileVisibility ProfileVisibility { get; private set; } = ProfileVisibility.Public;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private User() { } // EF Core

    public static User Provision(Guid id, string email, string displayName, string? avatarUrl)
    {
        if (id == Guid.Empty) throw new ArgumentException("User id is required.", nameof(id));
        return new User
        {
            Id = id,
            Email = NormalizeEmail(email),
            DisplayName = SanitizeDisplayName(displayName),
            AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim(),
        };
    }

    public void SyncEmail(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized != Email) Email = normalized;
    }

    public void Rename(string displayName) => DisplayName = SanitizeDisplayName(displayName);

    public void SetLanguage(string language) => Language = language;

    /// <returns>The previously uploaded avatar path, so the old file can be deleted.</returns>
    public string? SetAvatar(string? url, string? path)
    {
        var previous = AvatarPath != path ? AvatarPath : null;
        AvatarUrl = url;
        AvatarPath = path;
        return previous;
    }

    public void GrantPlatformAdmin() => IsPlatformAdmin = true;
    public void RevokePlatformAdmin() => IsPlatformAdmin = false;

    public void SetLeaderboardOptOut(bool optOut) => LeaderboardOptOut = optOut;

    public void AcceptLegal(string version, DateTimeOffset now)
    {
        AcceptedLegalVersion = version;
        AcceptedLegalAt = now;
    }

    public void RequestDeletion(DateTimeOffset now) => DeletionRequestedAt ??= now;

    public void CancelDeletion() => DeletionRequestedAt = null;

    /// <summary>
    /// Strips everything personal, leaving a nameless placeholder so the gym's own content (boulders set, official
    /// beta) keeps working. The address is replaced rather than blanked, because it must stay unique and must not
    /// allow signing in again.
    /// </summary>
    public void Anonymise(DateTimeOffset now)
    {
        DisplayName = "Utente eliminato";
        Email = $"deleted-{Id:N}@deleted.invalid";
        AvatarUrl = null;
        AvatarPath = null;
        IsPlatformAdmin = false;
        LeaderboardOptOut = true;
        DeletionRequestedAt = now;
    }

    public void ExcludeFromLeaderboards(Guid byUserId, DateTimeOffset now)
    {
        LeaderboardExcludedAt = now;
        LeaderboardExcludedByUserId = byUserId;
    }

    public void AllowInLeaderboards()
    {
        LeaderboardExcludedAt = null;
        LeaderboardExcludedByUserId = null;
    }

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>Collapses whitespace and trims. Length rules are validated by the application layer.</summary>
    public static string SanitizeDisplayName(string value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
