namespace BoulderTime.Application.Abstractions;

public sealed record GeoPoint(double Latitude, double Longitude, string DisplayName);

/// <summary>Turns an address into coordinates. Used to place gyms on the map; the result is only a suggestion staff confirm.</summary>
public interface IGeocoder
{
    /// <returns>The best match, or null when nothing was found or geocoding is unavailable.</returns>
    Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct = default);
}
