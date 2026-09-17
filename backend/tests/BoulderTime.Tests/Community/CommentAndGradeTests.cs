using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Community;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

[Collection(DatabaseCollection.Name)]
public sealed class CommentAndGradeTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Comments_are_public_editable_only_by_author_and_likes_are_one_per_user()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var alice = await _f.UserAsync();
        var bob = await _f.UserAsync();

        var created = (await (await alice.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "  Heel hook at the top!  " })).ReadAsync<CommentDto>())!;
        created.Content.Should().Be("Heel hook at the top!");

        (await bob.Client.PatchAsJsonAsync($"/api/comments/{created.Id}", new { content = "hijacked" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await bob.Client.DeleteAsync($"/api/comments/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await alice.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "   " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await bob.Client.PutAsync($"/api/comments/{created.Id}/like", null);
        var liked = (await (await bob.Client.PutAsync($"/api/comments/{created.Id}/like", null)).ReadAsync<CommentDto>())!;
        liked.Likes.Should().Be(1);
        liked.LikedByViewer.Should().BeTrue();

        var edited = (await (await alice.Client.PatchAsJsonAsync($"/api/comments/{created.Id}", new { content = "Heel hook, then match." })).ReadAsync<CommentDto>())!;
        edited.EditedAt.Should().NotBeNull();

        var anon = (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}/comments")).ReadAsync<PagedResult<CommentDto>>())!;
        anon.Items.Should().ContainSingle(c => c.Content == "Heel hook, then match." && c.Likes == 1);

        (await alice.Client.DeleteAsync($"/api/comments/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Staff_hide_comments_from_everyone_but_the_author_and_other_staff()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var climber = await _f.UserAsync();
        var c = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "spam spam" })).ReadAsync<CommentDto>())!;

        (await climber.Client.PostAsync($"/api/comments/{c.Id}/hide", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await w.Staff.Client.PostAsync($"/api/comments/{c.Id}/hide", null)).EnsureSuccessStatusCode();

        async Task<PagedResult<CommentDto>> ListAs(HttpClient client) => (await (await client.GetAsync($"/api/boulders/{b.Id}/comments")).ReadAsync<PagedResult<CommentDto>>())!;
        (await ListAs(climber.Client)).Items.Should().BeEmpty();
        (await ListAs(_f.CreateClient())).Items.Should().BeEmpty();
        (await ListAs(author.Client)).Items.Should().ContainSingle(x => x.Status == CommentStatus.Hidden);
        (await ListAs(w.Staff.Client)).Items.Should().ContainSingle(x => x.Status == CommentStatus.Hidden && x.CanModerate);
    }

    [Fact]
    public async Task Grade_suggestions_need_an_attempt_are_one_per_system_and_never_change_the_official_grade()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("6A");
        var font = w.Font;
        async Task Attempt(TestUser u) => await u.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 2, completed = true });
        object Vote(string label) => new { gradeSystemId = font.Id, gradeValueId = font.Values.Single(v => v.Label == label).Id };

        var alice = await _f.UserAsync();
        (await alice.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions", Vote("6A+"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var voters = new[] { alice, await _f.UserAsync(), await _f.UserAsync(), await _f.UserAsync() };
        foreach (var v in voters) await Attempt(v);
        await voters[0].Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions", Vote("6B"));
        await voters[0].Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions", Vote("6A+")); // changes, doesn't add
        await voters[1].Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions", Vote("6A+"));
        await voters[2].Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions", Vote("6A"));
        var result = (await (await voters[3].Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions", Vote("6B"))).ReadAsync<GradeConsensusDto>())!;

        var fontResult = result.Systems.Single(s => s.GradeSystemId == font.Id);
        fontResult.TotalVotes.Should().Be(4);
        fontResult.Buckets.Select(x => (x.Label, x.Votes)).Should().Equal(("6A", 1), ("6A+", 2), ("6B", 1));
        fontResult.ConsensusValueId.Should().Be(font.Values.Single(v => v.Label == "6A+").Id);
        fontResult.OfficialValueId.Should().Be(font.Values.Single(v => v.Label == "6A").Id);
        fontResult.ViewerValueId.Should().Be(font.Values.Single(v => v.Label == "6B").Id);

        var detail = (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}")).ReadAsync<Application.Boulders.BoulderDetailDto>())!;
        detail.Grades.Should().ContainSingle(g => g.Label == "6A");
    }

    [Fact]
    public async Task Suggestions_must_use_this_gyms_grades()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var other = await ClimbingWorld.CreateAsync(_f, "Other gym");
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 1, completed = false });

        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/grade-suggestions",
            new { gradeSystemId = other.Font.Id, gradeValueId = other.Font.Values[5].Id })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(new[] { 2, 2, 0 }, 0)] // tie between rank 0 and 1 → closest to median (rank 0.5→0), then lower
    [InlineData(new[] { 1, 3, 1 }, 1)] // clear winner
    [InlineData(new[] { 2, 0, 2 }, 0)] // tie far apart → median is 0 → lower
    public void Consensus_rule_is_isolated_and_deterministic(int[] votes, int expectedIndex)
    {
        var buckets = votes.Select((v, i) => new ConsensusBucketDto(Guid.NewGuid(), $"G{i}", i, null, v)).ToList();
        GradeConsensus.Pick(buckets).Should().Be(buckets[expectedIndex].GradeValueId);
    }
}
