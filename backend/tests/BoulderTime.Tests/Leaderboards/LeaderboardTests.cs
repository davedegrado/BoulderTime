using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Domain.Gyms;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Leaderboards;

[Collection(DatabaseCollection.Name)]
public sealed class LeaderboardTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Send(TestUser u, Guid boulderId) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 2, completed = true })).EnsureSuccessStatusCode();

    private async Task<LeaderboardDto> Board(HttpClient client, Guid gymId, string metric, string period, int? limit = null) =>
        (await (await client.GetAsync($"/api/gyms/{gymId}/leaderboard?metric={metric}&period={period}{(limit is null ? "" : $"&limit={limit}")}")).ReadAsync<LeaderboardDto>())!;

    [Theory]
    [InlineData(0, 23, 10)]
    [InlineData(22, 23, 100)]
    [InlineData(11, 23, 55)]
    [InlineData(0, 6, 10)]
    [InlineData(5, 6, 100)]
    [InlineData(2, 6, 46)]
    [InlineData(0, 1, 100)]
    public void Scoring_maps_every_scale_onto_10_to_100_points(int rank, int size, int expected) =>
        new RankBasedScoring().PointsFor(new GradedSend(rank, size)).Should().Be(expected);

    [Fact]
    public async Task Points_reward_difficulty_sends_reward_volume_and_highest_uses_the_primary_scale()
    {
        var w = await ClimbingWorld.CreateAsync(_f); // primary: Fontainebleau (23 grades), secondary: Colour
        var hard = await w.BoulderAsync("7C+");      // rank 16 → 10 + 90*16/22 = 75 points
        var easy = new List<Guid>();
        for (var i = 0; i < 3; i++) easy.Add((await w.BoulderAsync("3")).Id); // rank 0 → 10 points each

        var strong = await _f.UserAsync();
        var volume = await _f.UserAsync();
        await Send(strong, hard.Id);
        foreach (var id in easy) await Send(volume, id);

        var points = await Board(_f.CreateClient(), w.Gym.Id, "POINTS", "ALL");
        points.GradeSystemName.Should().Be("Fontainebleau");
        points.Entries.Select(e => (e.Climber.UserId, e.Points, e.Position)).Should().Equal((strong.Id, 75, 1), (volume.Id, 30, 2));

        var sends = await Board(_f.CreateClient(), w.Gym.Id, "COMPLETED", "ALL");
        sends.Entries.Select(e => (e.Climber.UserId, e.Completed)).Should().Equal((volume.Id, 3), (strong.Id, 1));

        var highest = await Board(_f.CreateClient(), w.Gym.Id, "HIGHEST", "ALL");
        highest.Entries[0].Highest!.Label.Should().Be("7C+");
        highest.Entries[1].Highest!.Label.Should().Be("3");
    }

    [Fact]
    public async Task Periods_use_the_completion_date_and_removed_boulders_still_count()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var recent = await w.BoulderAsync("6A");
        var old = await w.BoulderAsync("6A");
        var me = await _f.UserAsync();
        await Send(me, recent.Id);
        await Send(me, old.Id);
        await _f.Db(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE bouldertime.boulder_attempts SET completed_at = now() - interval '400 days' WHERE boulder_id = {0}", old.Id));
        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { recent.Id } });

        (await Board(_f.CreateClient(), w.Gym.Id, "COMPLETED", "WEEK")).Entries.Single().Completed.Should().Be(1);
        (await Board(_f.CreateClient(), w.Gym.Id, "COMPLETED", "MONTH")).Entries.Single().Completed.Should().Be(1);
        (await Board(_f.CreateClient(), w.Gym.Id, "COMPLETED", "ALL")).Entries.Single().Completed.Should().Be(2);
    }

    [Fact]
    public async Task Only_this_gyms_boulders_count_and_ties_share_a_position()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var other = await ClimbingWorld.CreateAsync(_f, "Other gym");
        var a1 = await w.BoulderAsync("6A");
        var a2 = await w.BoulderAsync("6A");
        var elsewhere = await other.BoulderAsync("8C+");
        var alice = await _f.UserAsync();
        var bob = await _f.UserAsync();
        var carol = await _f.UserAsync();
        await Send(alice, a1.Id);
        await Send(bob, a2.Id);
        await Send(carol, elsewhere.Id);

        var board = await Board(_f.CreateClient(), w.Gym.Id, "POINTS", "ALL");
        board.Climbers.Should().Be(2);
        board.Entries.Select(e => e.Position).Should().Equal(1, 1);
        board.Entries.Should().NotContain(e => e.Climber.UserId == carol.Id);
    }

    [Fact]
    public async Task Viewers_outside_the_top_see_their_own_position()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var hard = await w.BoulderAsync("7A");
        var mid = await w.BoulderAsync("6B");
        var easy = await w.BoulderAsync("4");
        var top1 = await _f.UserAsync();
        var top2 = await _f.UserAsync();
        var me = await _f.UserAsync();
        await Send(top1, hard.Id);
        await Send(top2, mid.Id);
        await Send(me, easy.Id);

        var board = await Board(me.Client, w.Gym.Id, "POINTS", "ALL", limit: 2);

        board.Entries.Should().HaveCount(2);
        board.Entries.Should().NotContain(e => e.IsViewer);
        board.Viewer!.Position.Should().Be(3);
        board.Viewer.IsViewer.Should().BeTrue();

        (await Board(_f.CreateClient(), w.Gym.Id, "POINTS", "ALL", limit: 2)).Viewer.Should().BeNull();
    }

    [Fact]
    public async Task Hidden_gyms_have_no_public_leaderboard()
    {
        var draft = await _f.GymAsync(GymStatus.Draft);
        (await _f.CreateClient().GetAsync($"/api/gyms/{draft.Id}/leaderboard")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
