using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Users;

/// <summary>
/// How a profile's sections are arranged (ADR-061): the default for every account that never chose, saved with the
/// account (so every device shows the same), back to the default on request. Hiding a section hides no data.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProfileSectionsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static readonly string[] DefaultOrder = ["stats", "favoriteSector", "gyms", "weekly", "highestGrades", "history"];

    [Fact]
    public async Task An_account_that_never_chose_has_the_default_arrangement()
    {
        var anna = await _f.UserAsync();
        var mine = (await (await anna.Client.GetAsync("/api/users/me/profile-sections")).ReadAsync<List<ProfileSectionDto>>())!;
        mine.Select(s => s.Key).Should().Equal(DefaultOrder);
        // Everything on but the statistics: they are shown only by those who choose to.
        mine.Where(s => s.Visible).Select(s => s.Key).Should().Equal(DefaultOrder.Where(k => k != "stats"));
        var seen = (await (await _f.CreateClient().GetAsync($"/api/users/{anna.Id}/profile")).ReadAsync<ProfileDto>())!;
        seen.Sections!.Select(s => s.Key).Should().Equal(DefaultOrder);
    }

    [Fact]
    public async Task The_arrangement_is_saved_with_the_account_and_read_on_any_device_until_reset()
    {
        var anna = await _f.UserAsync();
        var arranged = new[] { new { key = "history", visible = true }, new { key = "stats", visible = false }, new { key = "gyms", visible = true } };
        var saved = (await (await anna.Client.PutAsJsonAsync("/api/users/me/profile-sections", new { sections = arranged })).ReadAsync<List<ProfileSectionDto>>())!;
        // The sections not mentioned follow, visible, in their usual order.
        saved.Select(s => s.Key).Should().Equal("history", "stats", "gyms", "favoriteSector", "weekly", "highestGrades");
        saved.Single(s => s.Key == "stats").Visible.Should().BeFalse();

        // Another phone, same account; and a visitor sees the same order (the data of a hidden section is still there).
        var otherDevice = _f.ClientFor(anna.Id, anna.Email);
        (await (await otherDevice.GetAsync("/api/users/me/profile-sections")).ReadAsync<List<ProfileSectionDto>>())!.Should().Equal(saved);
        var visitor = (await (await _f.CreateClient().GetAsync($"/api/users/{anna.Id}/profile")).ReadAsync<ProfileDto>())!;
        visitor.Sections.Should().Equal(saved);
        visitor.Stats.Should().NotBeNull();

        var reset = (await (await anna.Client.DeleteAsync("/api/users/me/profile-sections")).ReadAsync<List<ProfileSectionDto>>())!;
        reset.Select(s => s.Key).Should().Equal(DefaultOrder);
        (await (await otherDevice.GetAsync("/api/users/me/profile-sections")).ReadAsync<List<ProfileSectionDto>>())!.Select(s => s.Key).Should().Equal(DefaultOrder);
    }

    [Fact]
    public async Task Unknown_or_repeated_sections_are_refused_and_only_the_owner_arranges()
    {
        var anna = await _f.UserAsync();
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-sections", new { sections = new[] { new { key = "friends", visible = true } } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-sections", new { sections = new[] { new { key = "stats", visible = true }, new { key = "stats", visible = false } } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-sections", new { sections = Array.Empty<object>() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _f.CreateClient().PutAsJsonAsync("/api/users/me/profile-sections", new { sections = new[] { new { key = "stats", visible = true } } }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void A_saved_arrangement_that_cannot_be_read_falls_back_to_the_default()
    {
        ProfileLayout.Read("not json").Select(s => s.Key).Should().Equal(DefaultOrder);
        var read = ProfileLayout.Read("""[{"key":"weekly","visible":false},{"key":"gone","visible":true}]""");
        read.Select(s => s.Key).Should().Equal("weekly", "stats", "favoriteSector", "gyms", "highestGrades", "history");
        // The sections it didn't mention come as they are by default.
        read.Single(s => s.Key == "stats").Visible.Should().BeFalse();
        read.Single(s => s.Key == "gyms").Visible.Should().BeTrue();
    }
}
