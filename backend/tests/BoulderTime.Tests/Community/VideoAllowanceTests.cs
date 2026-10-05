using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Gyms;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Community;

/// <summary>
/// How much video a gym may keep. Storage is the one cost that grows on its own, so the allowance is set by
/// BoulderTime and enforced by the API rather than by hiding buttons.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class VideoAllowanceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static object Upload(string kind = "Community") =>
        new { kind, contentType = "video/mp4", sizeBytes = 2_000_000 };

    [Fact]
    public async Task A_new_gym_starts_with_climber_videos_off_and_a_beta_limit()
    {
        var gym = await _f.GymAsync(GymStatus.Active);

        var stored = await _f.Db(db => db.Gyms.FirstAsync(g => g.Id == gym.Id));
        stored.CommunityVideosEnabled.Should().BeFalse();
        stored.OfficialBetaLimit.Should().Be(Gym.DefaultOfficialBetaLimit);
    }

    [Fact]
    public async Task Climbers_cannot_upload_until_BoulderTime_turns_it_on_for_that_gym()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);

        var refused = await climber.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/video-uploads", Upload());
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("community_videos_disabled");

        (await admin.Client.PutAsJsonAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance",
            new { communityVideosEnabled = true, officialBetaLimit = 20 })).EnsureSuccessStatusCode();

        (await climber.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/video-uploads", Upload())).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Only_BoulderTime_changes_the_allowance()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var climber = await _f.UserAsync();

        foreach (var user in new[] { world.Staff, climber })
        {
            (await user.Client.PutAsJsonAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance",
                new { communityVideosEnabled = true, officialBetaLimit = 999 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        (await _f.Db(db => db.Gyms.Where(g => g.Id == world.Gym.Id).Select(g => g.CommunityVideosEnabled).FirstAsync()))
            .Should().BeFalse();
    }

    [Fact]
    public async Task The_beta_limit_stops_new_videos_but_never_a_replacement()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var admin = await _f.UserAsync(platformAdmin: true);
        var first = await world.BoulderAsync("6A");
        var second = await world.BoulderAsync("6B");
        await admin.Client.PutAsJsonAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance",
            new { communityVideosEnabled = false, officialBetaLimit = 1 });

        // One beta is within the allowance.
        var path = await world.Staff.UploadVideoAsync(_f, first.Id, "BETA");
        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{first.Id}/beta", new { storagePath = path })).EnsureSuccessStatusCode();

        // The second boulder is refused...
        var refused = await world.Staff.Client.PostAsJsonAsync($"/api/boulders/{second.Id}/video-uploads", Upload("BETA"));
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("beta_limit_reached");

        // ...while replacing the first one costs no extra storage and stays allowed.
        (await world.Staff.Client.PostAsJsonAsync($"/api/boulders/{first.Id}/video-uploads", Upload("BETA"))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Removing_a_boulder_takes_its_official_beta_with_it()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var path = await world.Staff.UploadVideoAsync(_f, boulder.Id, "BETA");
        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta", new { storagePath = path })).EnsureSuccessStatusCode();

        (await world.Staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/boulders/remove",
            new { boulderIds = new[] { boulder.Id } })).EnsureSuccessStatusCode();

        // The beta showed a route that is no longer on the wall, and it was the heaviest thing we kept for it.
        (await _f.Db(db => db.BoulderBetas.CountAsync(b => b.BoulderId == boulder.Id))).Should().Be(0);
        (await _f.CreateClient().GetAsync($"/api/boulders/{boulder.Id}/beta")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The slot it occupied is free again.
        var admin = await _f.UserAsync(platformAdmin: true);
        var allowance = (await (await admin.Client.GetAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance")).ReadAsync<VideoAllowanceDto>())!;
        allowance.OfficialBetaUsed.Should().Be(0);
    }

    [Fact]
    public async Task No_limit_means_no_limit()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var admin = await _f.UserAsync(platformAdmin: true);
        await admin.Client.PutAsJsonAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance",
            new { communityVideosEnabled = true, unlimited = true });

        (await _f.Db(db => db.Gyms.Where(g => g.Id == world.Gym.Id).Select(g => g.OfficialBetaLimit).FirstAsync()))
            .Should().BeNull();

        for (var i = 0; i < 3; i++)
        {
            var boulder = await world.BoulderAsync("6A");
            var uploaded = await world.Staff.UploadVideoAsync(_f, boulder.Id, "BETA");
            (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta", new { storagePath = uploaded })).EnsureSuccessStatusCode();
        }
    }
}
