using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Climbing;

/// <summary>
/// Projects the climber chooses (ADR-067): marked and unmarked by hand only, closed by a send, kept through a removal;
/// and everything the apps already installed read — the "projects" filter, the count, the progress — exactly as before.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProjectTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<bool> Mark(TestUser who, Guid boulderId) =>
        (await (await who.Client.PutAsync($"/api/boulders/{boulderId}/project", null)).ReadAsync<ProjectStateDto>())!.IsProject;

    private static async Task<bool> Unmark(TestUser who, Guid boulderId) =>
        (await (await who.Client.DeleteAsync($"/api/boulders/{boulderId}/project")).ReadAsync<ProjectStateDto>())!.IsProject;

    private static Task Attempt(TestUser who, Guid boulderId, int attempts, bool completed) =>
        who.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts, completed });

    private static async Task<List<Guid>> List(TestUser who, Guid gymId, string progress, string status = "ACTIVE") =>
        (await (await who.Client.GetAsync($"/api/gyms/{gymId}/boulders?progress={progress}&status={status}")).ReadAsync<PagedResult<BoulderSummaryDto>>())!
            .Items.Select(b => b.Id).ToList();

    private static async Task<BoulderDetailDto> Detail(TestUser who, Guid boulderId) =>
        (await (await who.Client.GetAsync($"/api/boulders/{boulderId}")).ReadAsync<BoulderDetailDto>())!;

    private static async Task<HomeDto> Home(TestUser who) => (await (await who.Client.GetAsync("/api/users/me/home")).ReadAsync<HomeDto>())!;

    [Fact]
    public async Task Only_the_climber_makes_a_boulder_a_project_trying_it_never_does()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var tried = await w.BoulderAsync();
        var chosen = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await Attempt(me, tried.Id, 5, false);

        (await Detail(me, tried.Id)).IsProject.Should().BeFalse("five attempts don't make a project");
        (await List(me, w.Gym.Id, "MARKED")).Should().BeEmpty();

        (await Mark(me, chosen.Id)).Should().BeTrue();
        (await Mark(me, chosen.Id)).Should().BeTrue("marking twice changes nothing");
        (await Detail(me, chosen.Id)).IsProject.Should().BeTrue();
        (await List(me, w.Gym.Id, "MARKED")).Should().Equal(chosen.Id);
        (await Detail(await _f.UserAsync(), chosen.Id)).IsProject.Should().BeFalse("someone else's project is theirs");

        // On the cards too.
        var cards = (await (await me.Client.GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!.Items;
        cards.Single(c => c.Id == chosen.Id).IsProject.Should().BeTrue();
        cards.Single(c => c.Id == tried.Id).IsProject.Should().BeFalse();

        (await Unmark(me, chosen.Id)).Should().BeFalse();
        (await Unmark(me, chosen.Id)).Should().BeFalse("nothing left to unmark is not an error");
        (await List(me, w.Gym.Id, "MARKED")).Should().BeEmpty();
    }

    [Fact]
    public async Task A_completed_project_goes_among_the_sends_and_comes_back_if_the_send_is_undone()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await Mark(me, b.Id);
        await Attempt(me, b.Id, 3, false);
        (await Home(me)).Stats.MarkedProjects.Should().Be(1);

        await Attempt(me, b.Id, 4, true);
        (await List(me, w.Gym.Id, "MARKED")).Should().BeEmpty();
        (await List(me, w.Gym.Id, "COMPLETED")).Should().Equal(b.Id);
        var home = await Home(me);
        home.Stats.MarkedProjects.Should().Be(0);
        home.MarkedProjects.Should().BeEmpty();
        (await (await me.Client.GetAsync($"/api/users/{me.Id}/history?filter=MARKED")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!.Total.Should().Be(0);

        await Attempt(me, b.Id, 4, false);
        (await List(me, w.Gym.Id, "MARKED")).Should().Equal([b.Id], "the mark was kept, the send undone");
        (await (await me.Client.GetAsync($"/api/users/{me.Id}/history?filter=MARKED")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!.Total.Should().Be(1);
    }

    [Fact]
    public async Task What_older_apps_read_is_unchanged_projects_still_means_tried_and_not_completed()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var tried = await w.BoulderAsync();
        var markedOnly = await w.BoulderAsync();
        var both = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await Attempt(me, tried.Id, 2, false);
        await Mark(me, markedOnly.Id);
        await Mark(me, both.Id);
        await Attempt(me, both.Id, 1, false);

        (await List(me, w.Gym.Id, "PROJECTS")).Should().BeEquivalentTo([tried.Id, both.Id]);
        (await List(me, w.Gym.Id, "UNTRIED")).Should().Equal([markedOnly.Id], "marking is not trying");
        var stats = (await Home(me)).Stats;
        stats.Projects.Should().Be(2);
        stats.MarkedProjects.Should().Be(2);
        (await (await me.Client.GetAsync($"/api/users/{me.Id}/history?filter=PROJECTS")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!.Total.Should().Be(2);

        // A project with no attempt leaves the progress 1.0 reads as it was: none.
        using var doc = JsonDocument.Parse(await me.Client.GetStringAsync($"/api/boulders/{markedOnly.Id}"));
        doc.RootElement.GetProperty("viewer").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("isProject").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Home_lists_the_open_projects_and_how_many_tries_could_become_one()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var p1 = await w.BoulderAsync();
        var p2 = await w.BoulderAsync();
        var t1 = await w.BoulderAsync();
        var t2 = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await Mark(me, p1.Id);
        await Mark(me, p2.Id);
        await Attempt(me, p2.Id, 3, false);
        await Attempt(me, t1.Id, 1, false);
        await Attempt(me, t2.Id, 2, false);

        var home = await Home(me);
        home.MarkedProjects!.Select(b => b.Id).Should().Equal(p2.Id, p1.Id);
        home.UnmarkedTries.Should().Be(2, "tried, not completed and not marked");
        home.Projects.Select(b => b.Id).Should().BeEquivalentTo([p2.Id, t1.Id, t2.Id], "the older list is unchanged");
    }

    [Fact]
    public async Task A_boulder_off_the_wall_cant_become_a_project_but_one_that_was_stays_and_can_be_unmarked()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var kept = await w.BoulderAsync();
        var late = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await Mark(me, kept.Id);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { kept.Id, late.Id } })).EnsureSuccessStatusCode();

        (await me.Client.PutAsync($"/api/boulders/{late.Id}/project", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(me, kept.Id)).IsProject.Should().BeTrue("nothing is erased by a removal");
        (await List(me, w.Gym.Id, "MARKED", "REMOVED")).Should().Equal(kept.Id);
        var home = await Home(me);
        home.MarkedProjects.Should().BeEmpty("open projects are on the wall");
        home.Stats.MarkedProjects.Should().Be(0);
        (await Unmark(me, kept.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Projects_need_an_account_and_a_boulder_that_exists()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        (await _f.CreateClient().PutAsync($"/api/boulders/{b.Id}/project", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await (await _f.UserAsync()).Client.PutAsync($"/api/boulders/{Guid.NewGuid()}/project", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
