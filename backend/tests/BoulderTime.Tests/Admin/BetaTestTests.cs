using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Admin;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Admin;

/// <summary>
/// A gym launching BoulderTime for real after a trial: the boulders off the wall become trial ones, which stay in the
/// testers' history but score nothing and count in no leaderboard. Only a platform administrator can do it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BetaTestTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Send(TestUser u, Guid boulderId) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 2, completed = true })).EnsureSuccessStatusCode();

    private static async Task RemoveAsync(ClimbingWorld w, params Guid[] ids) =>
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = ids, notifyFollowers = false })).EnsureSuccessStatusCode();

    private async Task<BetaTestResultDto> MarkAsync(TestUser admin, ClimbingWorld w, string url = "beta-test")
    {
        var res = await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{w.Gym.Id}/{url}", new { gymSlug = w.Gym.Slug });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<BetaTestResultDto>())!;
    }

    private async Task<LeaderboardDto> Board(Guid gymId, string metric) =>
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{gymId}/leaderboard?metric={metric}&period=ALL")).ReadAsync<LeaderboardDto>())!;

    [Fact]
    public async Task Boulders_off_the_wall_stay_in_history_but_count_in_no_leaderboard_and_those_on_the_wall_are_untouched()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var kept = await w.BoulderAsync("6A");
        var trial = await w.BoulderAsync("7C+");
        var erased = await w.BoulderAsync("7A");
        var tester = await _f.UserAsync();
        foreach (var b in new[] { kept, trial, erased }) await Send(tester, b.Id);
        await RemoveAsync(w, trial.Id, erased.Id);
        (await w.Staff.Client.DeleteAsync($"/api/boulders/{erased.Id}")).EnsureSuccessStatusCode();

        var admin = await _f.UserAsync(platformAdmin: true);
        (await MarkAsync(admin, w)).Should().Be(new BetaTestResultDto(2, 2, BetaTesters: 1));

        // Only the boulder still on the wall counts, in every leaderboard.
        var keptPoints = (await (await _f.CreateClient().GetAsync($"/api/boulders/{kept.Id}")).ReadAsync<BoulderDetailDto>())!.Points;
        (await Board(w.Gym.Id, "POINTS")).Entries.Should().ContainSingle(e => e.Climber.UserId == tester.Id && e.Points == keptPoints);
        (await Board(w.Gym.Id, "COMPLETED")).Entries.Should().ContainSingle(e => e.Climber.UserId == tester.Id && e.Completed == 1);
        (await Board(w.Gym.Id, "HIGHEST")).Entries.Should().ContainSingle(e => e.Highest!.Label == "6A");

        // The testers keep them in their history, marked and without points; the boulder on the wall is as it was.
        var history = (await (await tester.Client.GetAsync($"/api/users/{tester.Id}/history?filter=COMPLETED")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!;
        history.Items.Should().HaveCount(3);
        var trialRow = history.Items.Single(i => i.Boulder.Id == trial.Id).Boulder;
        trialRow.SectorName.Should().Be("Cave · Betatest");
        trialRow.Points.Should().BeNull();
        var keptRow = history.Items.Single(i => i.Boulder.Id == kept.Id).Boulder;
        keptRow.SectorName.Should().Be("Cave");
        keptRow.Points.Should().Be(keptPoints);
        var page = (await (await _f.CreateClient().GetAsync($"/api/boulders/{trial.Id}")).ReadAsync<BoulderDetailDto>())!;
        page.SectorName.Should().Be("Cave · Betatest");
        page.Points.Should().BeNull();
    }

    [Fact]
    public async Task Running_it_again_adds_the_boulders_taken_down_since_and_undo_takes_every_mark_off()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var first = await w.BoulderAsync("6A");
        var forgotten = await w.BoulderAsync("6B");
        await RemoveAsync(w, first.Id);
        var admin = await _f.UserAsync(platformAdmin: true);
        (await MarkAsync(admin, w)).Should().Be(new BetaTestResultDto(1, 1));

        // Staff forgot one: they take it down and the call runs again, adding it and leaving the first as it was.
        await RemoveAsync(w, forgotten.Id);
        (await MarkAsync(admin, w)).Should().Be(new BetaTestResultDto(2, 1));
        (await MarkAsync(admin, w)).Should().Be(new BetaTestResultDto(2, 0));

        (await MarkAsync(admin, w, "beta-test/undo")).Should().Be(new BetaTestResultDto(0, 2));
        var page = (await (await _f.CreateClient().GetAsync($"/api/boulders/{first.Id}")).ReadAsync<BoulderDetailDto>())!;
        page.SectorName.Should().Be("Cave");
        page.Points.Should().NotBeNull();
    }

    [Fact]
    public async Task A_trial_boulder_put_back_on_the_wall_is_an_ordinary_one_again()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var boulder = await w.BoulderAsync("6A");
        await RemoveAsync(w, boulder.Id);
        var admin = await _f.UserAsync(platformAdmin: true);
        await MarkAsync(admin, w);

        (await w.Staff.Client.PostAsync($"/api/boulders/{boulder.Id}/restore", null)).EnsureSuccessStatusCode();
        var page = (await (await _f.CreateClient().GetAsync($"/api/boulders/{boulder.Id}")).ReadAsync<BoulderDetailDto>())!;
        page.SectorName.Should().Be("Cave");
        page.Points.Should().NotBeNull();
    }

    [Fact]
    public async Task Only_a_platform_administrator_can_run_it_and_only_on_the_gym_whose_slug_they_type()
    {
        var w = await ClimbingWorld.CreateAsync(_f); // w.Staff is the gym's OWNER
        var boulder = await w.BoulderAsync("6A");
        await RemoveAsync(w, boulder.Id);
        var staff = await _f.UserAsync();
        await _f.StaffAsync(w.Gym, staff, GymRole.Staff);
        var climber = await _f.UserAsync();

        foreach (var url in new[] { "beta-test", "beta-test/undo" })
        {
            var path = $"/api/admin/gyms/{w.Gym.Id}/{url}";
            var body = new { gymSlug = w.Gym.Slug };
            (await _f.CreateClient().PostAsJsonAsync(path, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            foreach (var user in new[] { climber, staff, w.Staff })
                (await user.Client.PostAsJsonAsync(path, body)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Even an administrator must name the gym: a pasted id alone, or another gym's slug, does nothing.
        var admin = await _f.UserAsync(platformAdmin: true);
        var other = await ClimbingWorld.CreateAsync(_f, "Other Gym");
        foreach (var slug in new[] { null, "", other.Gym.Slug })
            (await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{w.Gym.Id}/beta-test", new { gymSlug = slug })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var page = (await (await _f.CreateClient().GetAsync($"/api/boulders/{boulder.Id}")).ReadAsync<BoulderDetailDto>())!;
        page.SectorName.Should().Be("Cave");
    }
}
