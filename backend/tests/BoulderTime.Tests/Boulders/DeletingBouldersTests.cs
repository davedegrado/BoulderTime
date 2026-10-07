using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Community;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Boulders;

/// <summary>
/// Removing a boulder takes it off the wall. Deleting it erases what the gym put on it — but never the sends:
/// a climber's history and points are theirs, and a gym tidying its wall must not take them away.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class DeletingBouldersTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task RemoveAsync(TestUser staff, Guid gymId, Guid boulderId) =>
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gymId}/boulders/remove",
            new { boulderIds = new[] { boulderId }, notifyFollowers = false })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Climbers_can_browse_removed_boulders_not_only_staff()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        await RemoveAsync(world.Staff, world.Gym.Id, boulder.Id);

        // A send does not stop existing the day the sector is retraced, so the boulder stays findable.
        foreach (var client in new[] { climber.Client, _f.CreateClient() })
        {
            var list = (await (await client.GetAsync($"/api/gyms/{world.Gym.Id}/boulders?status=REMOVED")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
            list.Items.Select(b => b.Id).Should().Equal(boulder.Id);
            (await client.GetAsync($"/api/boulders/{boulder.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Deleting_keeps_every_send_and_the_points_they_are_worth()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        (await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/attempt", new { attempts = 3, completed = true })).EnsureSuccessStatusCode();
        var before = (await (await climber.Client.GetAsync($"/api/users/{climber.Id}/profile")).ReadAsync<ProfileDto>())!;
        before.Stats.Completed.Should().Be(1);

        await RemoveAsync(world.Staff, world.Gym.Id, boulder.Id);
        (await world.Staff.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).EnsureSuccessStatusCode();

        // The one thing deletion must not touch.
        var after = (await (await climber.Client.GetAsync($"/api/users/{climber.Id}/profile")).ReadAsync<ProfileDto>())!;
        after.Stats.Completed.Should().Be(1);
        after.Stats.TotalAttempts.Should().Be(3);
        var history = (await (await climber.Client.GetAsync($"/api/users/{climber.Id}/history?filter=COMPLETED")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!;
        history.Items.Should().HaveCount(1);
        var kept = history.Items[0];
        kept.Boulder.Id.Should().Be(boulder.Id);
        kept.Boulder.Status.Should().Be(BoulderStatus.Deleted);
        // Its grades survive too: the leaderboard scores a send by its grade, so losing them would zero it.
        kept.Boulder.Grades.Select(g => g.Label).Should().Equal("6A");
        // What it has lost is the photo — there is none left to point at.
        kept.Boulder.PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task A_deleted_boulder_is_gone_from_the_gym_for_everyone_including_staff()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        await RemoveAsync(world.Staff, world.Gym.Id, boulder.Id);
        (await world.Staff.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).EnsureSuccessStatusCode();

        foreach (var status in new[] { "ACTIVE", "REMOVED" })
        {
            var list = (await (await world.Staff.Client.GetAsync($"/api/gyms/{world.Gym.Id}/boulders?status={status}")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
            list.Items.Should().BeEmpty();
        }
        (await world.Staff.Client.GetAsync($"/api/boulders/{boulder.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Nor can it be asked for by name, which would otherwise be a way back in.
        (await world.Staff.Client.GetAsync($"/api/gyms/{world.Gym.Id}/boulders?status=DELETED")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // And it cannot be restored: there is no photo to restore it to.
        (await world.Staff.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/restore", new { })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_takes_the_gym_content_with_it_and_frees_the_storage()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        await climber.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Tacca piccola a metà" });
        await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/attempt", new { attempts = 1, completed = true });
        await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/rating", new { rating = 4 });
        var path = await world.Staff.UploadVideoAsync(_f, boulder.Id, "BETA");
        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta", new { storagePath = path })).EnsureSuccessStatusCode();

        var impact = (await (await world.Staff.Client.GetAsync($"/api/boulders/{boulder.Id}/deletion-impact")).ReadAsync<DeletionImpactDto>())!;
        impact.Sends.Should().Be(1);
        impact.Comments.Should().Be(1);
        impact.HasBeta.Should().BeTrue();

        await RemoveAsync(world.Staff, world.Gym.Id, boulder.Id);
        (await world.Staff.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).EnsureSuccessStatusCode();

        await _f.Db(async db =>
        {
            (await db.Comments.CountAsync(c => c.BoulderId == boulder.Id)).Should().Be(0);
            (await db.BoulderRatings.CountAsync(r => r.BoulderId == boulder.Id)).Should().Be(0);
            (await db.BoulderBetas.CountAsync(b => b.BoulderId == boulder.Id)).Should().Be(0);
            (await db.BoulderFollows.CountAsync(f => f.BoulderId == boulder.Id)).Should().Be(0);
            // The row survives, emptied, because the sends still point at it.
            var row = await db.Boulders.AsNoTracking().FirstAsync(b => b.Id == boulder.Id);
            row.PhotoPath.Should().BeEmpty();
            (await db.BoulderAttempts.CountAsync(a => a.BoulderId == boulder.Id)).Should().Be(1);
            (await db.BoulderGrades.CountAsync(g => g.BoulderId == boulder.Id)).Should().Be(1);
            return 0;
        });
    }

    [Fact]
    public async Task Only_a_gym_admin_deletes_and_only_after_the_boulder_is_off_the_wall()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();

        // Deleting is not part of setting: a boulder still on the wall has to come off first.
        var tooSoon = await world.Staff.Client.DeleteAsync($"/api/boulders/{boulder.Id}");
        tooSoon.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await tooSoon.Content.ReadAsStringAsync()).Should().Contain("boulder_not_removed");

        await RemoveAsync(world.Staff, world.Gym.Id, boulder.Id);

        // Setters may take boulders off the wall; erasing one is a gym-admin decision.
        var setter = await _f.UserAsync();
        await _f.StaffAsync(world.Gym, setter, GymRole.Staff);
        (await climber.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await setter.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await world.Staff.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).EnsureSuccessStatusCode();

        // Deleting twice is the same as deleting once, and must not fail.
        (await world.Staff.Client.DeleteAsync($"/api/boulders/{boulder.Id}")).EnsureSuccessStatusCode();
    }
}
