using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;

namespace BoulderTime.Application.Gyms;

/// <param name="IsFoundingGym">The single gym that launched BoulderTime with us.</param>
/// <param name="IsEarlyPartner">Currently in the early-adopter programme (several gyms can be).</param>
public sealed record GymSummaryDto(
    Guid Id, string Slug, string Name, string City, string? LogoUrl, string? CoverImageUrl, GymStatus Status,
    double? Latitude = null, double? Longitude = null, double? DistanceKm = null,
    bool IsFoundingGym = false, bool IsEarlyPartner = false)
{
    public static GymSummaryDto From(Gym g, bool isEarlyPartner = false) =>
        new(g.Id, g.Slug, g.Name, g.City, g.LogoUrl, g.CoverImageUrl, g.Status, g.Latitude, g.Longitude,
            IsFoundingGym: g.IsFoundingGym, IsEarlyPartner: isEarlyPartner);
}

public sealed record GymPinDto(Guid Id, string Slug, string Name, string City, string? LogoUrl, double Latitude, double Longitude);

public sealed record GeocodeResultDto(double Latitude, double Longitude, string DisplayName);

public sealed record GymDetailDto(
    Guid Id, string Slug, string Name, string? Description, string? Address, string City,
    string? Website, string? Email, string? Phone, string? InstagramUrl, string? FacebookUrl, string? LogoUrl, string? CoverImageUrl,
    GymStatus Status, DateTimeOffset CreatedAt,
    /// <summary>The viewer's role at this gym (null when anonymous or not staff). Drives "Manage gym" UI only.</summary>
    GymRole? ViewerRole,
    Follows.GymFollowState? Follow = null,
    int FollowerCount = 0,
    double? Latitude = null,
    double? Longitude = null,
    bool IsFoundingGym = false,
    bool IsEarlyPartner = false,
    /// <summary>Climbers of this gym can upload their own beta.</summary>
    bool CommunityVideosEnabled = false,
    DateTimeOffset? EarlyPartnerSince = null,
    /// <summary>The floor plan the sectors are drawn on, with its size in pixels (for its proportions).</summary>
    string? FloorPlanUrl = null,
    int? FloorPlanWidth = null,
    int? FloorPlanHeight = null)
{
    public static GymDetailDto From(Gym g, GymRole? viewerRole) => new(
        g.Id, g.Slug, g.Name, g.Description, g.Address, g.City, g.Website, g.Email, g.Phone, g.InstagramUrl, g.FacebookUrl,
        g.LogoUrl, g.CoverImageUrl, g.Status, g.CreatedAt, viewerRole, Latitude: g.Latitude, Longitude: g.Longitude,
        IsFoundingGym: g.IsFoundingGym, CommunityVideosEnabled: g.CommunityVideosEnabled,
        FloorPlanUrl: g.FloorPlanUrl, FloorPlanWidth: g.FloorPlanWidth, FloorPlanHeight: g.FloorPlanHeight);
}

/// <param name="Latitude">Set both coordinates, or send ClearLocation to remove them; omit both to keep the current ones.</param>
public sealed record UpdateGymRequest(string? Name, string? Description, string? Address, string? City, string? Website, string? Email, string? Phone,
    string? InstagramUrl, string? FacebookUrl,
    double? Latitude = null, double? Longitude = null, bool? ClearLocation = null);

/// <summary>A point on the floor plan, in fractions of its width (X) and height (Y), 0–1.</summary>
public sealed record MapPoint(double X, double Y);

/// <summary>
/// A sector's area on the floor plan: its outline (3–64 points) and where its label sits. `HideLabel` leaves the name
/// off the plan, for sectors whose names would cover each other; tapping the area still names it.
/// </summary>
public sealed record MapZoneDto(IReadOnlyList<MapPoint> Points, MapPoint Label, bool HideLabel = false)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web);
    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this, Json);
    public static MapZoneDto? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<MapZoneDto>(json, Json); }
        catch (System.Text.Json.JsonException) { return null; }
    }
}

/// <param name="ActiveBoulders">Boulders on the wall in this sector.</param>
/// <param name="NewThisWeek">Of those, the ones set in the last seven days.</param>
public sealed record SectorDto(Guid Id, Guid GymId, string Name, string? Description, string? ImageUrl, int SortOrder, bool IsActive, bool IsFollowing = false,
    MapZoneDto? Zone = null, int ActiveBoulders = 0, int NewThisWeek = 0)
{
    public static SectorDto From(Sector s, bool isFollowing = false, int activeBoulders = 0, int newThisWeek = 0) =>
        new(s.Id, s.GymId, s.Name, s.Description, s.ImageUrl, s.SortOrder, s.IsActive, isFollowing, MapZoneDto.FromJson(s.MapZone), activeBoulders, newThisWeek);
}

/// <summary>The outline to draw, or no points to take the sector off the plan. Without a label, it goes in the middle.</summary>
public sealed record SetSectorZoneRequest(IReadOnlyList<MapPoint>? Points, MapPoint? Label, bool? HideLabel = null);

/// <summary>An uploaded floor plan and its size in pixels, or no path to remove it.</summary>
public sealed record SetFloorPlanRequest(string? Path, int? Width, int? Height);

public sealed record CreateSectorRequest(string? Name, string? Description);
public sealed record UpdateSectorRequest(string? Name, string? Description, bool? IsActive);
public sealed record ReorderSectorsRequest(IReadOnlyList<Guid>? SectorIds);
