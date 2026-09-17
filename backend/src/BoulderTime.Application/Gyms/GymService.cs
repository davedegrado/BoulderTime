using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Gyms;

public sealed class GymService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IGeocoder geocoder)
{
    public const int MaxPins = 500;

    /// <summary>
    /// Public discovery: active gyms only, matched on name or city. With a position (<paramref name="near"/>), results
    /// are ordered by distance and carry DistanceKm; gyms without coordinates come last.
    /// </summary>
    public async Task<PagedResult<GymSummaryDto>> SearchAsync(string? query, int? page, int? pageSize, CancellationToken ct = default,
        (double Latitude, double Longitude)? near = null)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var q = db.Gyms.AsNoTracking().Where(g => g.Status == GymStatus.Active);

        var text = Input.Trimmed(query);
        if (text.Length > 0)
        {
            var pattern = Input.LikeContains(text.ToLowerInvariant());
            q = q.Where(g => EF.Functions.Like(g.Name.ToLower(), pattern, @"\") || EF.Functions.Like(g.City.ToLower(), pattern, @"\"));
        }

        var total = await q.CountAsync(ct);
        List<Gym> items;
        if (near is { } origin)
        {
            GymValidation.ValidateLocation(origin.Latitude, origin.Longitude);
            // Equirectangular approximation: accurate enough to rank gyms by distance, and translatable to SQL.
            var lat = origin.Latitude;
            var lng = origin.Longitude;
            var cos = Math.Cos(lat * Math.PI / 180);
            items = await q.OrderBy(g => g.Latitude == null)
                .ThenBy(g => (g.Latitude - lat) * (g.Latitude - lat) + (g.Longitude - lng) * cos * (g.Longitude - lng) * cos)
                .ThenBy(g => g.Name)
                .Skip((p - 1) * size).Take(size).ToListAsync(ct);
        }
        else
        {
            items = await q.OrderBy(g => g.Name).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        }

        return new PagedResult<GymSummaryDto>(items.Select(g => GymSummaryDto.From(g) with
        {
            DistanceKm = near is { } o && g.Latitude is { } la && g.Longitude is { } lo ? Math.Round(HaversineKm(o.Latitude, o.Longitude, la, lo), 1) : null,
        }).ToList(), p, size, total);
    }

    /// <summary>Pins for the gyms inside the visible map area (active gyms with coordinates).</summary>
    public async Task<IReadOnlyList<GymPinDto>> PinsAsync(double south, double west, double north, double east, CancellationToken ct = default)
    {
        new Validator()
            .Check(south is >= -90 and <= 90 && north is >= -90 and <= 90 && south <= north, "south", "Invalid map bounds.")
            .Check(west is >= -180 and <= 180 && east is >= -180 and <= 180, "west", "Invalid map bounds.")
            .ThrowIfInvalid();
        var q = db.Gyms.AsNoTracking().Where(g => g.Status == GymStatus.Active && g.Latitude != null && g.Longitude != null
                                                  && g.Latitude >= south && g.Latitude <= north);
        // A view crossing the antimeridian has west > east.
        q = west <= east
            ? q.Where(g => g.Longitude >= west && g.Longitude <= east)
            : q.Where(g => g.Longitude >= west || g.Longitude <= east);
        return await q.OrderBy(g => g.Name).Take(MaxPins)
            .Select(g => new GymPinDto(g.Id, g.Slug, g.Name, g.City, g.LogoUrl, g.Latitude!.Value, g.Longitude!.Value))
            .ToListAsync(ct);
    }

    /// <summary>Suggests coordinates from the gym's address (staff confirm or adjust them on the map).</summary>
    public async Task<GeocodeResultDto> GeocodeAsync(Guid gymId, string? query, CancellationToken ct = default)
    {
        var (gym, _) = await access.RequireRoleAsync(gymId, Domain.Staff.GymRole.Admin, ct);
        var text = Input.Trimmed(query);
        if (text.Length == 0) text = string.Join(", ", new[] { gym.Address, gym.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (text.Length < 3) throw new ValidationException("query", "Add the gym's address first.");
        var point = await geocoder.GeocodeAsync(text, ct)
                    ?? throw new NotFoundException("Address", text);
        return new GeocodeResultDto(point.Latitude, point.Longitude, point.DisplayName);
    }

    public static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 6371 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Active gyms are public. Draft/archived gyms are visible only to their staff and platform admins.</summary>
    public async Task<GymDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Slug == normalized, ct)
                  ?? throw new NotFoundException("Gym", slug);
        var role = await access.GetRoleAsync(gym.Id, ct);
        if (!gym.IsPubliclyVisible && role is null) throw new NotFoundException("Gym", slug);

        Follows.GymFollowState? follow = null;
        if (currentUser.UserId is { } uid)
        {
            var f = await db.GymFollows.AsNoTracking().FirstOrDefaultAsync(x => x.GymId == gym.Id && x.UserId == uid, ct);
            follow = f is null ? new(false, false, false) : new(true, f.IsFavorite, f.NotificationsEnabled);
        }
        var followers = await db.GymFollows.CountAsync(x => x.GymId == gym.Id, ct);
        return GymDetailDto.From(gym, role) with { Follow = follow, FollowerCount = followers };
    }

    public async Task<GymDetailDto> UpdateAsync(Guid gymId, UpdateGymRequest r, CancellationToken ct = default)
    {
        var (gym, role) = await access.RequireRoleAsync(gymId, Domain.Staff.GymRole.Admin, ct);
        GymValidation.ValidateProfile(r.Name, r.City, r.Description, r.Address, r.Website, r.Email, r.Phone);
        GymValidation.ValidateLocation(r.Latitude, r.Longitude);
        gym.UpdateProfile(r.Name!, r.Description, r.Address, r.City!, r.Website, r.Email, r.Phone);
        if (r.ClearLocation == true) gym.SetLocation(null, null);
        else if (r.Latitude is not null) gym.SetLocation(r.Latitude, r.Longitude);
        await db.SaveChangesAsync(ct);
        return GymDetailDto.From(gym, role);
    }
}
