using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;

namespace BoulderTime.Application.Gyms;

public sealed record GymSummaryDto(
    Guid Id, string Slug, string Name, string City, string? LogoUrl, string? CoverImageUrl, GymStatus Status,
    double? Latitude = null, double? Longitude = null, double? DistanceKm = null)
{
    public static GymSummaryDto From(Gym g) => new(g.Id, g.Slug, g.Name, g.City, g.LogoUrl, g.CoverImageUrl, g.Status, g.Latitude, g.Longitude);
}

public sealed record GymPinDto(Guid Id, string Slug, string Name, string City, string? LogoUrl, double Latitude, double Longitude);

public sealed record GeocodeResultDto(double Latitude, double Longitude, string DisplayName);

public sealed record GymDetailDto(
    Guid Id, string Slug, string Name, string? Description, string? Address, string City,
    string? Website, string? Email, string? Phone, string? LogoUrl, string? CoverImageUrl,
    GymStatus Status, DateTimeOffset CreatedAt,
    /// <summary>The viewer's role at this gym (null when anonymous or not staff). Drives "Manage gym" UI only.</summary>
    GymRole? ViewerRole,
    Follows.GymFollowState? Follow = null,
    int FollowerCount = 0,
    double? Latitude = null,
    double? Longitude = null)
{
    public static GymDetailDto From(Gym g, GymRole? viewerRole) => new(
        g.Id, g.Slug, g.Name, g.Description, g.Address, g.City, g.Website, g.Email, g.Phone,
        g.LogoUrl, g.CoverImageUrl, g.Status, g.CreatedAt, viewerRole, Latitude: g.Latitude, Longitude: g.Longitude);
}

/// <param name="Latitude">Set both coordinates, or send ClearLocation to remove them; omit both to keep the current ones.</param>
public sealed record UpdateGymRequest(string? Name, string? Description, string? Address, string? City, string? Website, string? Email, string? Phone,
    double? Latitude = null, double? Longitude = null, bool? ClearLocation = null);

public sealed record SectorDto(Guid Id, Guid GymId, string Name, string? Description, string? ImageUrl, int SortOrder, bool IsActive, bool IsFollowing = false)
{
    public static SectorDto From(Sector s, bool isFollowing = false) => new(s.Id, s.GymId, s.Name, s.Description, s.ImageUrl, s.SortOrder, s.IsActive, isFollowing);
}

public sealed record CreateSectorRequest(string? Name, string? Description);
public sealed record UpdateSectorRequest(string? Name, string? Description, bool? IsActive);
public sealed record ReorderSectorsRequest(IReadOnlyList<Guid>? SectorIds);
