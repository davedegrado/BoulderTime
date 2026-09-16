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
    DateTimeOffset CreatedAt)
{
    public static CurrentUserDto From(User u) => new(u.Id, u.Email, u.DisplayName, u.AvatarUrl, u.IsPlatformAdmin, u.CreatedAt);
}

public sealed record UpdateProfileRequest(string? DisplayName);
