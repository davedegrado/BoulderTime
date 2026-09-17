using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Gyms;

[Collection(DatabaseCollection.Name)]
public sealed class GymMapTests(PostgresFixture postgres) : IAsyncLifetime
{
    private sealed class FakeGeocoder : IGeocoder
    {
        public Task<GeoPoint?> GeocodeAsync(string query, CancellationToken ct = default) =>
            Task.FromResult(query.Contains("Tortona") ? new GeoPoint(45.4526, 9.1623, "Via Tortona 31, Milano") : null);
    }

    private readonly ApiFactory _f = new(postgres) { OverrideServices = s => s.AddSingleton<IGeocoder, FakeGeocoder>() };
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private Task<Gym> GymAt(string name, double? lat, double? lng, GymStatus status = GymStatus.Active) =>
        _f.Db(async db =>
        {
            // Unique slug, trimmed only when it exceeds the column limit (a short name produced a shorter string than the cut).
            var slug = $"{name.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}";
            var g = Gym.Create(name, slug.Length > 40 ? slug[..40] : slug, "City");
            g.SetStatus(status);
            if (lat is not null) g.SetLocation(lat, lng);
            db.Gyms.Add(g);
            await db.SaveChangesAsync();
            return g;
        });

    [Fact]
    public async Task Map_returns_pins_only_for_active_gyms_with_coordinates_inside_the_viewport()
    {
        await GymAt("Milano Gym", 45.4526, 9.1623);
        await GymAt("Torino Gym", 45.0781, 7.6696);
        await GymAt("Roma Gym", 41.9028, 12.4964);
        await GymAt("Secret Milano", 45.46, 9.17, GymStatus.Draft);
        await GymAt("No Pin", null, null);

        // Viewport around Lombardy/Piedmont.
        var pins = (await (await _f.CreateClient().GetAsync("/api/gyms/map?south=44.5&west=6.5&north=46.5&east=10.5")).ReadAsync<List<GymPinDto>>())!;

        pins.Select(p => p.Name).Should().Equal("Milano Gym", "Torino Gym");
        (await _f.CreateClient().GetAsync("/api/gyms/map?south=50&west=0&north=40&east=10")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Search_near_a_position_orders_by_distance_with_distances_and_unlocated_gyms_last()
    {
        await GymAt("Roma Gym", 41.9028, 12.4964);
        await GymAt("Torino Gym", 45.0781, 7.6696);
        await GymAt("Milano Gym", 45.4526, 9.1623);
        await GymAt("Aaa No Pin", null, null);

        // Standing in Milan's Duomo.
        var res = (await (await _f.CreateClient().GetAsync("/api/gyms?lat=45.4642&lng=9.1900")).ReadAsync<PagedResult<GymSummaryDto>>())!;

        res.Items.Select(g => g.Name).Should().Equal("Milano Gym", "Torino Gym", "Roma Gym", "Aaa No Pin");
        res.Items[0].DistanceKm.Should().BeLessThan(3);
        res.Items[1].DistanceKm.Should().BeInRange(120, 135); // Milano → Torino ≈ 126 km
        res.Items[3].DistanceKm.Should().BeNull();
    }

    [Fact]
    public async Task Gym_admins_set_and_clear_the_location_and_can_look_it_up_from_the_address()
    {
        var gym = await GymAt("Crimp Factory", null, null);
        var admin = await _f.UserAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        await _f.StaffAsync(gym, staff, GymRole.Staff);

        (await staff.Client.GetAsync($"/api/gyms/{gym.Id}/geocode?q=Via%20Tortona%2031%20Milano")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var suggestion = (await (await admin.Client.GetAsync($"/api/gyms/{gym.Id}/geocode?q=Via%20Tortona%2031%20Milano")).ReadAsync<GeocodeResultDto>())!;
        suggestion.Latitude.Should().Be(45.4526);
        (await admin.Client.GetAsync($"/api/gyms/{gym.Id}/geocode?q=Nowhere%20street")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = new { name = "Crimp Factory", city = "Milano", latitude = suggestion.Latitude, longitude = suggestion.Longitude };
        (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "Crimp Factory", city = "Milano", latitude = 45.45 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var saved = (await (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", body)).ReadAsync<GymDetailDto>())!;
        saved.Latitude.Should().Be(45.4526);

        // Editing other fields keeps the location; clearing removes it.
        (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "Crimp Factory", city = "Milano", description = "New" })).EnsureSuccessStatusCode();
        (await _f.Db(db => db.Gyms.Where(g => g.Id == gym.Id).Select(g => g.Latitude).FirstAsync())).Should().Be(45.4526);
        (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "Crimp Factory", city = "Milano", clearLocation = true })).EnsureSuccessStatusCode();
        (await _f.Db(db => db.Gyms.Where(g => g.Id == gym.Id).Select(g => g.Latitude).FirstAsync())).Should().BeNull();
    }

    [Fact]
    public void Haversine_matches_known_distances() =>
        GymService.HaversineKm(45.4642, 9.1900, 41.9028, 12.4964).Should().BeInRange(475, 485); // Milano → Roma ≈ 477 km
}
