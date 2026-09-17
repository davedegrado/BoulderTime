using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BoulderTime.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Infrastructure.Geocoding;

/// <summary>
/// OpenStreetMap Nominatim. Usage policy: max 1 request/second, identify the application (User-Agent + contact email),
/// no bulk geocoding. We only geocode on explicit staff action, serialised through a gate.
/// For production volume, point Geocoding:BaseUrl to a hosted Nominatim-compatible service.
/// </summary>
public sealed class NominatimGeocoder(HttpClient http, IConfiguration configuration, ILogger<NominatimGeocoder> logger) : IGeocoder
{
    public const string HttpClientName = "nominatim";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _last = DateTimeOffset.MinValue;

    public async Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct = default)
    {
        var baseUrl = (configuration["Geocoding:BaseUrl"] ?? "https://nominatim.openstreetmap.org").TrimEnd('/');
        var email = configuration["Geocoding:Email"];
        var url = $"{baseUrl}/search?format=jsonv2&limit=1&q={Uri.EscapeDataString(query)}{(string.IsNullOrWhiteSpace(email) ? "" : $"&email={Uri.EscapeDataString(email)}")}";

        await Gate.WaitAsync(ct);
        try
        {
            var wait = _last.AddSeconds(1.1) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _last = DateTimeOffset.UtcNow;
            var results = await http.GetFromJsonAsync<List<NominatimResult>>(url, ct);
            var best = results?.FirstOrDefault();
            if (best is null) return null;
            return new GeoPoint(double.Parse(best.Lat, CultureInfo.InvariantCulture), double.Parse(best.Lon, CultureInfo.InvariantCulture), best.DisplayName ?? query);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Geocoding failed for {Query}", query);
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }

    private sealed record NominatimResult(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon,
        [property: JsonPropertyName("display_name")] string? DisplayName);
}

/// <summary>Used when geocoding is disabled (Geocoding:Provider = None): staff place the pin manually.</summary>
public sealed class DisabledGeocoder : IGeocoder
{
    public Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct = default) => Task.FromResult<GeoPoint?>(null);
}
