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
    int PendingInvitations)
{
    public static CurrentUserDto From(User u, IReadOnlyList<MyStaffGymDto>? staffGyms = null, int pendingInvitations = 0) =>
        new(u.Id, u.Email, u.DisplayName, u.AvatarUrl, u.IsPlatformAdmin, u.CreatedAt, staffGyms ?? [], pendingInvitations);
}

public sealed record UpdateProfileRequest(string? DisplayName);
