using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Climbing;

/// <summary>"Completed by N people" and the sector a climber completed most (ADR-047).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class CompletionsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static Task Send(TestUser u, Guid boulderId, bool completed = true, int attempts = 2) =>
        u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts, completed });

    [Fact]
    public async Task Each_person_counts_once_and_an_undone_send_stops_counting()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var alice = await _f.UserAsync();
        var bob = await _f.UserAsync();
        var carl = await _f.UserAsync();

        await Send(alice, b.Id);
        await Send(alice, b.Id, attempts: 5); // more tries, still one person
        await Send(bob, b.Id);
        await Send(carl, b.Id, completed: false); // a project is not a send
        await Send(bob, b.Id, completed: false);  // undone

        (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}")).ReadAsync<BoulderDetailDto>())!.Completions.Should().Be(1);
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        list.Items.Single().Completions.Should().Be(1);
    }

    [Fact]
    public async Task Names_need_an_account_and_leave_out_who_asked_not_to_appear_and_who_is_blocked()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();
        var shy = await _f.UserAsync();
        var rude = await _f.UserAsync();
        var friend = await _f.UserAsync();
        foreach (var u in new[] { me, shy, rude, friend }) await Send(u, b.Id);
        await shy.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = true });
        await me.Client.PutAsJsonAsync($"/api/users/me/blocks/{rude.Id}", new { });
        await me.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = true });

        (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}/completions")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var seen = (await (await me.Client.GetAsync($"/api/boulders/{b.Id}/completions")).ReadAsync<BoulderCompletionsDto>())!;
        seen.Total.Should().Be(4);
        // The viewer always sees themself, even having asked not to appear to others.
        seen.People.Items.Select(p => p.Person.UserId).Should().BeEquivalentTo(new[] { me.Id, friend.Id });
        seen.People.Total.Should().Be(2);

        var byFriend = (await (await friend.Client.GetAsync($"/api/boulders/{b.Id}/completions")).ReadAsync<BoulderCompletionsDto>())!;
        byFriend.People.Items.Select(p => p.Person.UserId).Should().BeEquivalentTo(new[] { friend.Id, rude.Id });
    }

    [Fact]
    public async Task A_suspended_account_counts_nowhere()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var cheat = await _f.UserAsync();
        await Send(cheat, b.Id);
        var admin = await _f.UserAsync(platformAdmin: true);
        (await admin.Client.PutAsJsonAsync($"/api/admin/users/{cheat.Id}/suspension", new { reason = "Spam" })).EnsureSuccessStatusCode();

        (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}")).ReadAsync<BoulderDetailDto>())!.Completions.Should().Be(0);
    }

    [Fact]
    public async Task The_favourite_sector_is_where_most_boulders_were_completed_taken_down_ones_included()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var slab = await _f.SectorAsync(w.Gym, "Slab");
        var me = await _f.UserAsync();

        var cave1 = await w.BoulderAsync();
        var cave2 = await w.BoulderAsync("6B");
        await Send(me, cave1.Id);
        await Send(me, cave2.Id);
        // Two sends in Slab, but one is undone: Cave still leads.
        var s1 = await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            BoulderTestData.BoulderBody(slab, await w.Staff.UploadPhotoAsync(_f, w.Gym), w.Font, "6A"))).ReadAsync<BoulderDetailDto>();
        var s2 = await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            BoulderTestData.BoulderBody(slab, await w.Staff.UploadPhotoAsync(_f, w.Gym), w.Font, "6B"))).ReadAsync<BoulderDetailDto>();
        await Send(me, s1!.Id);
        await Send(me, s2!.Id);
        await Send(me, s2.Id, completed: false);

        // Cave is retraced: its sends stay in the person's history and still count.
        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { cave1.Id, cave2.Id }, notifyFollowers = false });

        var profile = (await (await _f.CreateClient().GetAsync($"/api/users/{me.Id}/profile")).ReadAsync<ProfileDto>())!;
        profile.FavoriteSector!.SectorName.Should().Be(w.Sector.Name);
        profile.FavoriteSector.Completed.Should().Be(2);
        profile.FavoriteSector.GymSlug.Should().Be(w.Gym.Slug);

        var newcomer = await _f.UserAsync();
        (await (await _f.CreateClient().GetAsync($"/api/users/{newcomer.Id}/profile")).ReadAsync<ProfileDto>())!.FavoriteSector.Should().BeNull();
    }
}
