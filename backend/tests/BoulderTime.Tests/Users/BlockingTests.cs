using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Community;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Users;

/// <summary>
/// Blocking someone. It is personal, immediate and silent: no moderator decides it, the blocked person is not told,
/// and nothing is removed for anyone else.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BlockingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<IReadOnlyList<string>> CommentsSeenBy(TestUser viewer, Guid boulderId) =>
        (await (await viewer.Client.GetAsync($"/api/boulders/{boulderId}/comments")).ReadAsync<PagedResult<CommentDto>>())!
            .Items.Select(c => c.Content).ToList();

    [Fact]
    public async Task Blocking_hides_what_each_writes_from_the_other_and_from_nobody_else()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var annoyed = await _f.UserAsync();
        var rude = await _f.UserAsync();
        var bystander = await _f.UserAsync();
        await annoyed.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Bel blocco" });
        await rude.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Commento sgradevole" });

        (await annoyed.Client.PutAsJsonAsync($"/api/users/me/blocks/{rude.Id}", new { })).EnsureSuccessStatusCode();

        // Both directions: otherwise the blocked person could keep replying to someone who stopped reading.
        (await CommentsSeenBy(annoyed, boulder.Id)).Should().Equal("Bel blocco");
        (await CommentsSeenBy(rude, boulder.Id)).Should().Equal("Commento sgradevole");
        // Everyone else still sees both: a block is a personal setting, not moderation.
        (await CommentsSeenBy(bystander, boulder.Id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task The_blocked_person_is_never_told()
    {
        var annoyed = await _f.UserAsync();
        var rude = await _f.UserAsync();
        await annoyed.Client.PutAsJsonAsync($"/api/users/me/blocks/{rude.Id}", new { });

        // Nothing in their notifications, and their own profile view of the blocker says nothing either.
        (await _f.Db(db => db.Notifications.CountAsync(n => n.UserId == rude.Id))).Should().Be(0);
        var seenByRude = (await (await rude.Client.GetAsync($"/api/users/{annoyed.Id}/profile")).ReadAsync<ProfileDto>())!;
        seenByRude.IsBlocked.Should().BeFalse();

        // The blocker does see it, so the block can be found and undone.
        var seenByAnnoyed = (await (await annoyed.Client.GetAsync($"/api/users/{rude.Id}/profile")).ReadAsync<ProfileDto>())!;
        seenByAnnoyed.IsBlocked.Should().BeTrue();
    }

    [Fact]
    public async Task Unblocking_brings_everything_back()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var annoyed = await _f.UserAsync();
        var rude = await _f.UserAsync();
        await rude.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Commento sgradevole" });
        await annoyed.Client.PutAsJsonAsync($"/api/users/me/blocks/{rude.Id}", new { });
        (await CommentsSeenBy(annoyed, boulder.Id)).Should().BeEmpty();

        (await annoyed.Client.DeleteAsync($"/api/users/me/blocks/{rude.Id}")).EnsureSuccessStatusCode();

        (await CommentsSeenBy(annoyed, boulder.Id)).Should().Equal("Commento sgradevole");
        (await (await annoyed.Client.GetAsync("/api/users/me/blocks")).ReadAsync<IReadOnlyList<BlockedPersonDto>>())!.Should().BeEmpty();
    }

    [Fact]
    public async Task Blocking_twice_is_one_block_and_blocking_yourself_is_refused()
    {
        var climber = await _f.UserAsync();
        var other = await _f.UserAsync();

        await climber.Client.PutAsJsonAsync($"/api/users/me/blocks/{other.Id}", new { });
        (await climber.Client.PutAsJsonAsync($"/api/users/me/blocks/{other.Id}", new { })).EnsureSuccessStatusCode();
        (await _f.Db(db => db.UserBlocks.CountAsync())).Should().Be(1);

        (await climber.Client.PutAsJsonAsync($"/api/users/me/blocks/{climber.Id}", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _f.CreateClient().PutAsJsonAsync($"/api/users/me/blocks/{other.Id}", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gym_staff_still_moderate_what_they_blocked()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var rude = await _f.UserAsync();
        await rude.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Commento sgradevole" });
        await world.Staff.Client.PutAsJsonAsync($"/api/users/me/blocks/{rude.Id}", new { });

        // Otherwise blocking a moderator would be a way to put content beyond their reach.
        (await CommentsSeenBy(world.Staff, boulder.Id)).Should().Equal("Commento sgradevole");
    }
}
