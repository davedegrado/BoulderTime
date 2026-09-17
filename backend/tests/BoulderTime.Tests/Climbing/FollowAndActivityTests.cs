using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Follows;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Climbing;

[Collection(DatabaseCollection.Name)]
public sealed class FollowAndActivityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Gym_sector_and_boulder_follows_are_independent_and_idempotent()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();

        (await me.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { })).EnsureSuccessStatusCode();
        var fav = await (await me.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { isFavorite = true })).ReadAsync<GymFollowState>();
        fav!.IsFavorite.Should().BeTrue();
        (await me.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { })).EnsureSuccessStatusCode();
        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/follow", new { notificationsEnabled = false })).EnsureSuccessStatusCode();

        (await _f.Db(db => db.GymFollows.CountAsync(x => x.UserId == me.Id))).Should().Be(1);

        // Unfollowing the gym leaves the sector and boulder follows alone.
        (await me.Client.DeleteAsync($"/api/gyms/{w.Gym.Id}/follow")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await me.Client.DeleteAsync($"/api/gyms/{w.Gym.Id}/follow")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var mine = await (await me.Client.GetAsync("/api/users/me/follows")).ReadAsync<MyFollowsDto>();
        mine!.Gyms.Should().BeEmpty();
        mine.Sectors.Should().ContainSingle(s => s.SectorId == w.Sector.Id);
        mine.Boulders.Should().ContainSingle(x => x.BoulderId == b.Id && !x.NotificationsEnabled);

        var gym = await (await me.Client.GetAsync($"/api/gyms/{w.Gym.Slug}")).ReadAsync<GymDetailDto>();
        gym!.Follow!.IsFollowing.Should().BeFalse();
    }

    [Fact]
    public async Task Users_can_favourite_several_gyms_and_cannot_follow_hidden_gyms()
    {
        var a = await _f.GymAsync(GymStatus.Active, "Gym A");
        var b = await _f.GymAsync(GymStatus.Active, "Gym B");
        var draft = await _f.GymAsync(GymStatus.Draft, "Secret");
        var me = await _f.UserAsync();

        (await me.Client.PutAsJsonAsync($"/api/gyms/{a.Id}/follow", new { isFavorite = true })).EnsureSuccessStatusCode();
        (await me.Client.PutAsJsonAsync($"/api/gyms/{b.Id}/follow", new { isFavorite = true })).EnsureSuccessStatusCode();
        (await me.Client.PutAsJsonAsync($"/api/gyms/{draft.Id}/follow", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var home = await (await me.Client.GetAsync("/api/users/me/home")).ReadAsync<HomeDto>();
        home!.Gyms.Where(g => g.IsFavorite).Select(g => g.Gym.Name).Should().Equal("Gym A", "Gym B");
    }

    [Fact]
    public async Task Highest_grade_is_per_system_and_stats_count_completions_and_projects()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var easy = await w.BoulderAsync("5+", "Black");   // colour rank high, font rank low
        var hard = await w.BoulderAsync("7A", "Green");
        var project = await w.BoulderAsync("7C");
        var me = await _f.UserAsync();
        await me.Client.PutAsJsonAsync($"/api/boulders/{easy.Id}/attempt", new { attempts = 1, completed = true });
        await me.Client.PutAsJsonAsync($"/api/boulders/{hard.Id}/attempt", new { attempts = 6, completed = true });
        await me.Client.PutAsJsonAsync($"/api/boulders/{project.Id}/attempt", new { attempts = 9, completed = false });

        var profile = await (await _f.CreateClient().GetAsync($"/api/users/{me.Id}/profile")).ReadAsync<ProfileDto>();

        profile!.Stats.Completed.Should().Be(2);
        profile.Stats.Projects.Should().Be(1);
        profile.Stats.TotalAttempts.Should().Be(16);
        profile.Stats.CompletedThisMonth.Should().Be(2);
        profile.HighestGrades.Single(h => h.SystemType == GradeSystemType.Fontainebleau).Label.Should().Be("7A");
        profile.HighestGrades.Single(h => h.SystemType == GradeSystemType.Color).Label.Should().Be("Black");
        profile.Weekly.Should().HaveCount(ActivityService.Weeks);
        profile.Weekly[^1].Completed.Should().Be(2);
        profile.RecentCompletions.Should().HaveCount(2);
        profile.IsMe.Should().BeFalse();
    }

    [Fact]
    public async Task Home_shows_projects_and_fresh_untried_boulders_from_followed_gyms()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var project = await w.BoulderAsync("6A");
        var sent = await w.BoulderAsync("6B");
        var fresh = await w.BoulderAsync("6C");
        var elsewhere = await ClimbingWorld.CreateAsync(_f, "Not followed");
        await elsewhere.BoulderAsync("6A");
        var me = await _f.UserAsync();
        await me.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await me.Client.PutAsJsonAsync($"/api/boulders/{project.Id}/attempt", new { attempts = 3, completed = false });
        await me.Client.PutAsJsonAsync($"/api/boulders/{sent.Id}/attempt", new { attempts = 1, completed = true });

        var home = await (await me.Client.GetAsync("/api/users/me/home")).ReadAsync<HomeDto>();

        home!.Projects.Select(p => p.Id).Should().Equal(project.Id);
        home.FreshToTry.Select(p => p.Id).Should().Equal(fresh.Id);
        home.RecentCompletions.Should().ContainSingle(r => r.Boulder.Id == sent.Id);
        home.Gyms.Should().ContainSingle(g => g.Gym.Id == w.Gym.Id && g.ActiveBoulders == 3 && g.NewThisWeek == 3);
    }
}
