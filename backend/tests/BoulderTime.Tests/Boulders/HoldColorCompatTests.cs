using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Domain.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Boulders;

/// <summary>
/// Light blue and wood holds (ADR-066): every app may send them; the apps that say they know them see them; the store
/// apps already installed see the closest colour they know, find them under it, and never overwrite them by saving.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class HoldColorCompatTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    /// <summary>The same person in an app from 1.1, which says it knows every hold colour.</summary>
    private HttpClient NewApp(TestUser user)
    {
        var client = _f.ClientFor(user.Id, user.Email);
        client.DefaultRequestHeaders.Add(ClientCapabilities.Header, ClientCapabilities.HoldColors2);
        return client;
    }

    private static object Edit(ClimbingWorld w, BoulderDetailDto b, string holdColor) => new
    {
        sectorId = w.Sector.Id, photoPath = b.PhotoPath, holdColor,
        grades = b.Grades.Select(g => new { gradeSystemId = g.GradeSystemId, gradeValueId = g.GradeValueId }).ToArray(),
    };

    private static async Task<string> HoldsOf(HttpClient client, Guid boulderId)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/boulders/{boulderId}"));
        return doc.RootElement.GetProperty("holdColor").GetString()!;
    }

    private static async Task<List<Guid>> List(HttpClient client, Guid gymId, string holdColor)
    {
        using var doc = JsonDocument.Parse(await client.GetStringAsync($"/api/gyms/{gymId}/boulders?holdColor={holdColor}"));
        return doc.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
    }

    private Task<HoldColor> Stored(Guid boulderId) => _f.WithDbAsync(db => db.Boulders.Where(b => b.Id == boulderId).Select(b => b.HoldColor).FirstAsync());

    [Theory]
    [InlineData("LIGHT_BLUE", HoldColor.LightBlue, "BLUE")]
    [InlineData("WOOD", HoldColor.Wood, "BROWN")]
    public async Task A_new_colour_is_saved_shown_to_new_apps_and_as_the_closest_known_one_to_older_apps(string sent, HoldColor stored, string legacy)
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var staffApp = NewApp(w.Staff);
        (await staffApp.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, sent))).EnsureSuccessStatusCode();

        (await Stored(b.Id)).Should().Be(stored);
        (await HoldsOf(staffApp, b.Id)).Should().Be(sent);
        var climber = await _f.UserAsync();
        (await HoldsOf(climber.Client, b.Id)).Should().Be(legacy, "an app from before 1.1 would show an unknown colour as red");
        (await HoldsOf(_f.CreateClient(), b.Id)).Should().Be(legacy, "signed out too");

        // Lists carry the colour the same way.
        using var list = JsonDocument.Parse(await climber.Client.GetStringAsync($"/api/gyms/{w.Gym.Id}/boulders"));
        list.RootElement.GetProperty("items")[0].GetProperty("holdColor").GetString().Should().Be(legacy);
    }

    [Fact]
    public async Task Older_apps_find_the_new_colours_under_the_one_they_see_new_apps_find_each_on_its_own()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var blue = await w.BoulderAsync();
        var lightBlue = await w.BoulderAsync();
        var staffApp = NewApp(w.Staff);
        (await staffApp.PutAsJsonAsync($"/api/boulders/{blue.Id}", Edit(w, blue, "BLUE"))).EnsureSuccessStatusCode();
        (await staffApp.PutAsJsonAsync($"/api/boulders/{lightBlue.Id}", Edit(w, lightBlue, "LIGHT_BLUE"))).EnsureSuccessStatusCode();
        var climber = await _f.UserAsync();

        (await List(climber.Client, w.Gym.Id, "BLUE")).Should().BeEquivalentTo([blue.Id, lightBlue.Id]);
        (await List(NewApp(climber), w.Gym.Id, "BLUE")).Should().Equal(blue.Id);
        (await List(NewApp(climber), w.Gym.Id, "LIGHT_BLUE")).Should().Equal(lightBlue.Id);
        (await List(NewApp(climber), w.Gym.Id, "light_blue")).Should().Equal([lightBlue.Id], "the case doesn't matter, as before");
        (await climber.Client.GetAsync($"/api/gyms/{w.Gym.Id}/boulders?holdColor=TEAL")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Saving_from_an_older_app_keeps_a_colour_it_could_not_see_but_a_real_change_still_counts()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        (await NewApp(w.Staff).PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, "LIGHT_BLUE"))).EnsureSuccessStatusCode();

        // The 1.0 editor was shown blue and sends blue back with another change: light blue stays.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, "BLUE"))).EnsureSuccessStatusCode();
        (await Stored(b.Id)).Should().Be(HoldColor.LightBlue);

        // Picking another colour there is a real change.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, "RED"))).EnsureSuccessStatusCode();
        (await Stored(b.Id)).Should().Be(HoldColor.Red);

        // A new app can turn light blue into blue on purpose.
        (await NewApp(w.Staff).PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, "LIGHT_BLUE"))).EnsureSuccessStatusCode();
        (await NewApp(w.Staff).PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, "BLUE"))).EnsureSuccessStatusCode();
        (await Stored(b.Id)).Should().Be(HoldColor.Blue);
    }

    [Fact]
    public async Task The_colours_there_were_travel_exactly_as_before_to_every_app()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var climber = await _f.UserAsync();
        foreach (var color in new[] { "RED", "ORANGE", "YELLOW", "GREEN", "BLUE", "PURPLE", "PINK", "WHITE", "BLACK", "GREY", "BROWN", "MIXED" })
        {
            (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, color))).EnsureSuccessStatusCode();
            (await HoldsOf(climber.Client, b.Id)).Should().Be(color);
            (await HoldsOf(NewApp(climber), b.Id)).Should().Be(color);
            (await List(climber.Client, w.Gym.Id, color)).Should().Equal(b.Id);
        }
        // Numbers were refused before and still are.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", new { sectorId = w.Sector.Id, photoPath = b.PhotoPath, holdColor = 4, grades = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Each_new_colour_folds_into_a_colour_older_apps_know()
    {
        foreach (var c in Enum.GetValues<HoldColor>())
            ((int)HoldColors.Legacy(c)).Should().BeLessThanOrEqualTo((int)HoldColor.Mixed, $"{c} must reach older apps as a colour they know");
    }
}
