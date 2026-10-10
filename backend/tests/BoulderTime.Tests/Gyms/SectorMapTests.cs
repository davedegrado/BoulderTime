using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Gyms;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Gyms;

/// <summary>Sectors drawn on the gym's floor plan, and the counts the plan shows on them.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class SectorMapTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static object Square(double x, double y, double size) => new
    {
        points = new[] { new { x, y }, new { x = x + size, y }, new { x = x + size, y = y + size }, new { x, y = y + size } },
    };

    [Fact]
    public async Task Staff_draw_a_sector_on_the_plan_and_everyone_gets_its_outline()
    {
        var w = await ClimbingWorld.CreateAsync(_f);

        var drawn = (await (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/zone", Square(0.1, 0.2, 0.4))).ReadAsync<SectorDto>())!;
        drawn.Zone!.Points.Should().HaveCount(4);
        // Without a label of its own, it goes in the middle of the outline.
        drawn.Zone.Label.Should().Be(new MapPoint(0.3, 0.4));

        var listed = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/sectors")).ReadAsync<List<SectorDto>>())!;
        listed.Single().Zone!.Points[2].Should().Be(new MapPoint(0.5, 0.6));

        // Taking it off the plan: no points.
        var cleared = (await (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/zone", new { points = Array.Empty<object>() })).ReadAsync<SectorDto>())!;
        cleared.Zone.Should().BeNull();
    }

    [Fact]
    public async Task An_outline_must_be_a_real_shape_inside_the_plan_and_only_staff_draw_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var url = $"/api/sectors/{w.Sector.Id}/zone";

        (await w.Staff.Client.PutAsJsonAsync(url, new { points = new[] { new { x = 0.1, y = 0.1 }, new { x = 0.2, y = 0.2 } } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await w.Staff.Client.PutAsJsonAsync(url, Square(0.8, 0.8, 0.5))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await w.Staff.Client.PutAsJsonAsync(url, new { points = Enumerable.Range(0, 65).Select(i => new { x = i / 100.0, y = 0.5 }) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var climber = await _f.UserAsync();
        (await climber.Client.PutAsJsonAsync(url, Square(0.1, 0.1, 0.2))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Each_sector_says_how_many_boulders_are_on_the_wall_and_how_many_are_new()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        await w.BoulderAsync("6A");
        await w.BoulderAsync("6B");
        var gone = await w.BoulderAsync("6C");
        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { gone.Id }, notifyFollowers = false });

        var sector = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/sectors")).ReadAsync<List<SectorDto>>())!.Single();
        sector.ActiveBoulders.Should().Be(2);
        sector.NewThisWeek.Should().Be(2);
    }
}
