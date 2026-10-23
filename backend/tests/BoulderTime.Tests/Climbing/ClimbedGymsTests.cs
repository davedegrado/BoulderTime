using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Domain.Grading;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Climbing;

/// <summary>
/// The gyms on a person's profile (ADR-064): only where they completed a boulder, each once, latest first, with the
/// distinct boulders completed and the highest grade per scale of that gym.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ClimbedGymsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Log(TestUser u, Guid boulderId, int attempts, bool completed) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts, completed })).EnsureSuccessStatusCode();

    private static async Task<IReadOnlyList<ClimbedGymDto>> Climbed(HttpClient client, Guid userId) =>
        (await (await client.GetAsync($"/api/users/{userId}/profile")).ReadAsync<ProfileDto>())!.ClimbedGyms!;

    [Fact]
    public async Task Only_gyms_with_a_completed_boulder_each_once_with_distinct_boulders_counted()
    {
        var crimp = await ClimbingWorld.CreateAsync(_f, "Crimp Factory");
        var a = await crimp.BoulderAsync("6A");
        var b = await crimp.BoulderAsync("6B");
        var c = await crimp.BoulderAsync("7A");
        var followedOnly = await ClimbingWorld.CreateAsync(_f, "Volume Lab");
        var tried = await ClimbingWorld.CreateAsync(_f, "Sloper Social");
        var project = await tried.BoulderAsync("6C");
        var anna = await _f.UserAsync();

        await Log(anna, a.Id, 2, true);
        await Log(anna, b.Id, 5, true);
        // The same boulder sent again (more attempts logged, completed once more) is still one boulder.
        await Log(anna, a.Id, 4, true);
        await Log(anna, a.Id, 4, false);
        await Log(anna, a.Id, 5, true);
        // Tried, not sent: no count.
        await Log(anna, c.Id, 3, false);
        // A gym only followed, a gym with only a project: neither appears.
        (await anna.Client.PutAsJsonAsync($"/api/gyms/{followedOnly.Gym.Id}/follow", new { })).EnsureSuccessStatusCode();
        await Log(anna, project.Id, 6, false);

        var gyms = await Climbed(_f.CreateClient(), anna.Id);
        gyms.Should().ContainSingle();
        var g = gyms[0];
        g.Gym.Id.Should().Be(crimp.Gym.Id);
        g.Gym.City.Should().NotBeNullOrEmpty();
        g.Gym.LogoUrl.Should().BeNull("a gym without a logo still has its card");
        g.Completed.Should().Be(2);
        g.LastCompletedAt.Should().NotBeNull();
        // Tried boulders never raise the highest grade: 6B, not 7A.
        g.Highest.Should().ContainSingle(h => h.SystemType == GradeSystemType.Fontainebleau).Which.Label.Should().Be("6B");
    }

    [Fact]
    public async Task Several_gyms_come_latest_send_first_each_with_its_own_highest_grade_per_scale()
    {
        var crimp = await ClimbingWorld.CreateAsync(_f, "Crimp Factory");
        var granite = await ClimbingWorld.CreateAsync(_f, "Granite House");
        var colours = granite.Color.Values.OrderBy(v => v.Rank).ToList();
        var lowerFont = granite.Font.Values.Where(v => v.Rank < granite.Font.Values.Single(x => x.Label == "6A").Rank).MaxBy(v => v.Rank)!.Label;
        var anna = await _f.UserAsync();

        await Log(anna, (await crimp.BoulderAsync("7A")).Id, 3, true);
        await Log(anna, (await granite.BoulderAsync("6A", colours[0].Label)).Id, 1, true);
        await Log(anna, (await granite.BoulderAsync(lowerFont, colours[^1].Label)).Id, 1, true);

        var gyms = await Climbed(_f.CreateClient(), anna.Id);
        gyms.Select(x => x.Gym.Name).Should().Equal("Granite House", "Crimp Factory");
        var g = gyms[0];
        g.Completed.Should().Be(2);
        // Each scale on its own, no equivalence between them: Font by its rank, colours by theirs.
        g.Highest.Select(h => h.SystemType).Should().Equal(GradeSystemType.Fontainebleau, GradeSystemType.Color);
        g.Highest[0].Label.Should().Be("6A");
        g.Highest[1].Label.Should().Be(colours[^1].Label);
        // Another gym's grades never mix in.
        gyms[1].Highest.Should().ContainSingle().Which.Label.Should().Be("7A");
    }

    [Fact]
    public async Task Nobody_without_a_send_has_any_and_a_hidden_profile_shows_none()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        (await Climbed(_f.CreateClient(), anna.Id)).Should().BeEmpty();

        await Log(anna, (await w.BoulderAsync()).Id, 1, true);
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).EnsureSuccessStatusCode();
        (await Climbed(_f.CreateClient(), anna.Id)).Should().BeEmpty();
        (await Climbed(anna.Client, anna.Id)).Should().ContainSingle();
    }
}
