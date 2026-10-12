using System.Net.Http.Json;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Leaderboards;

/// <summary>Leaderboards are opt-in: a new account appears only once the person turns it on.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class LeaderboardOptInTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<LeaderboardDto> Board(ClimbingWorld w) =>
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/leaderboard?period=ALL")).ReadAsync<LeaderboardDto>())!;

    private static async Task Send(TestUser u, Guid boulderId) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 1, completed = true })).EnsureSuccessStatusCode();

    [Fact]
    public async Task A_new_account_is_out_of_the_leaderboards_until_the_person_turns_them_on()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("6A");
        var newcomer = await _f.UserAsync(inLeaderboards: false);
        await Send(newcomer, b.Id);

        var me = (await (await newcomer.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        me.LeaderboardOptOut.Should().BeTrue();
        (await Board(w)).Entries.Should().BeEmpty();

        (await newcomer.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = false })).EnsureSuccessStatusCode();
        (await Board(w)).Entries.Should().ContainSingle(e => e.Climber.UserId == newcomer.Id);
    }

    [Fact]
    public async Task Asking_to_delete_leaves_the_board_at_once_and_changing_your_mind_gives_back_your_own_choice()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("6A");
        var ranked = await _f.UserAsync();
        var hidden = await _f.UserAsync(inLeaderboards: false);
        await Send(ranked, b.Id);
        await Send(hidden, b.Id);

        foreach (var u in new[] { ranked, hidden })
            (await u.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = u.Email })).EnsureSuccessStatusCode();
        (await Board(w)).Entries.Should().BeEmpty();

        foreach (var u in new[] { ranked, hidden })
            (await u.Client.DeleteAsync("/api/users/me/deletion")).EnsureSuccessStatusCode();
        // Back as they were: the one who was ranked is ranked again, the one who wasn't stays out.
        (await Board(w)).Entries.Select(e => e.Climber.UserId).Should().Equal(ranked.Id);
    }
}
