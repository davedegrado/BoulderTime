using BoulderTime.Domain.Staff;

namespace BoulderTime.Application.Staff;

public sealed record StaffMemberDto(Guid UserId, string DisplayName, string Email, string? AvatarUrl, GymRole Role, DateTimeOffset Since);

public sealed record GymInvitationDto(Guid Id, string Email, GymRole Role, InvitationStatus Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string InvitedBy);

/// <summary>An invitation as seen by the invitee.</summary>
public sealed record MyInvitationDto(Guid Id, Guid GymId, string GymSlug, string GymName, string GymCity, GymRole Role, string InvitedBy, DateTimeOffset ExpiresAt);

public sealed record MyStaffGymDto(Guid GymId, string Slug, string Name, string City, string? LogoUrl, GymRole Role);

public sealed record InviteStaffRequest(string? Email, GymRole? Role);
public sealed record ChangeRoleRequest(GymRole? Role);
