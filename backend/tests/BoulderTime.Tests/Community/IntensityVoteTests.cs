using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Community;
using BoulderTime.Domain.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

/// <summary>
/// Climbers vote how intense a boulder is, next to the grade: once each, only after trying it, and never changing the
/// intensity staff set. The answer is the grade consensus, with the intensity added for the apps that read it.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class IntensityVoteTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Attempt(TestUser u, Guid boulderId) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 1, completed = false })).EnsureSuccessStatusCode();

    private static async Task<GradeConsensusDto> Vote(TestUser u, Guid boulderId, string intensity)
    {
        var res = await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/intensity-vote", new { intensity });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<GradeConsensusDto>())!;
    }

    [Fact]
    public async Task Votes_need_an_attempt_are_one_per_climber_and_leave_the_gyms_intensity_as_it_is()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        await _f.WithDbAsync(async db =>
        {
            var boulder = await db.Boulders.FindAsync(b.Id);
            boulder!.Update(boulder.SectorId, boulder.PhotoPath, boulder.ThumbnailPath, boulder.HoldColor, boulder.SetterUserId, BoulderIntensity.Medium);
            return await db.SaveChangesAsync();
        });

        var alice = await _f.UserAsync();
        (await alice.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/intensity-vote", new { intensity = "HIGH" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var voters = new List<TestUser> { alice, await _f.UserAsync(), await _f.UserAsync() };
        foreach (var v in voters) await Attempt(v, b.Id);
        await Vote(voters[0], b.Id, "LOW");
        await Vote(voters[0], b.Id, "HIGH"); // changes the vote, doesn't add one
        await Vote(voters[1], b.Id, "HIGH");
        var result = await Vote(voters[2], b.Id, "MEDIUM");

        var i = result.Intensity!;
        i.TotalVotes.Should().Be(3);
        i.MostVoted.Should().Be(BoulderIntensity.High);
        i.Viewer.Should().Be(BoulderIntensity.Medium);
        i.Official.Should().Be(BoulderIntensity.Medium);
        i.Buckets.Select(x => (x.Intensity, x.Votes)).Should().Equal((BoulderIntensity.Low, 0), (BoulderIntensity.Medium, 1), (BoulderIntensity.High, 2));

        // Read by anyone, signed in or not; the gym's intensity stays the gym's.
        var anonymous = (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}/grade-consensus")).ReadAsync<GradeConsensusDto>())!;
        anonymous.Intensity!.MostVoted.Should().Be(BoulderIntensity.High);
        anonymous.Intensity.Viewer.Should().BeNull();
        (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}")).ReadAsync<BoulderDetailDto>())!.Intensity.Should().Be(BoulderIntensity.Medium);

        // Withdrawn: gone from the count.
        var withdrawn = (await (await voters[0].Client.DeleteAsync($"/api/boulders/{b.Id}/intensity-vote")).ReadAsync<GradeConsensusDto>())!;
        withdrawn.Intensity!.TotalVotes.Should().Be(2);
        withdrawn.Intensity.Viewer.Should().BeNull();
    }

    [Fact]
    public async Task Something_that_is_not_an_intensity_is_refused_and_a_boulder_nobody_voted_has_no_most_voted()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await Attempt(me, b.Id);
        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/intensity-vote", new { intensity = (string?)null })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/intensity-vote", new { intensity = "EXTREME" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var none = (await (await me.Client.GetAsync($"/api/boulders/{b.Id}/grade-consensus")).ReadAsync<GradeConsensusDto>())!;
        none.Intensity!.TotalVotes.Should().Be(0);
        none.Intensity.MostVoted.Should().BeNull();
        none.Intensity.Official.Should().BeNull();
    }

    [Theory]
    [InlineData(new[] { 1, 0, 1 }, BoulderIntensity.Low)]    // a tie between the ends: the lower one
    [InlineData(new[] { 1, 1, 1 }, BoulderIntensity.Medium)] // a three-way tie: the median
    [InlineData(new[] { 0, 2, 2 }, BoulderIntensity.Medium)] // median of M,M,H,H is M
    public void Ties_go_toward_the_median_then_the_lower_level(int[] votes, BoulderIntensity expected) =>
        IntensityConsensus.Pick([
            new(BoulderIntensity.Low, votes[0]), new(BoulderIntensity.Medium, votes[1]), new(BoulderIntensity.High, votes[2]),
        ]).Should().Be(expected);
}
