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

    /// <summary>
    /// Platform-wide administrator. Set only via the server-side CLI — never from token claims or client input.
    /// </summary>
    public bool IsPlatformAdmin { get; private set; }

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

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.", nameof(email));
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>Collapses whitespace and trims. Length rules are validated by the application layer.</summary>
    public static string SanitizeDisplayName(string value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
