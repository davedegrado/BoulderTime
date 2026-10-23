using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Users;

/// <summary>
/// Friendships (ADR-063): asked, accepted, ended; what a friend sees of a private profile; and blocking, which ends a
/// friendship and is never revealed by one.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FriendshipTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<FriendshipState> Ask(TestUser from, TestUser to)
    {
        var res = await from.Client.PostAsync($"/api/users/{to.Id}/friend-request", null);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await res.ReadAsync<FriendshipDto>())!.State;
    }

    private static async Task<FriendshipState> Accept(TestUser who, TestUser asker) =>
        (await (await who.Client.PostAsync($"/api/users/{asker.Id}/friend-request/accept", null)).ReadAsync<FriendshipDto>())!.State;

    private static async Task<FriendshipState> Remove(TestUser who, TestUser other) =>
        (await (await who.Client.DeleteAsync($"/api/users/{other.Id}/friendship")).ReadAsync<FriendshipDto>())!.State;

    private static async Task<ProfileDto> Profile(HttpClient client, Guid userId) =>
        (await (await client.GetAsync($"/api/users/{userId}/profile")).ReadAsync<ProfileDto>())!;

    private static async Task<List<FriendRequestDto>> Requests(TestUser who) =>
        (await (await who.Client.GetAsync("/api/users/me/friend-requests")).ReadAsync<List<FriendRequestDto>>())!;

    private static async Task<PagedResult<FriendDto>> FriendsOf(HttpClient client, Guid userId) =>
        (await (await client.GetAsync($"/api/users/{userId}/friends")).ReadAsync<PagedResult<FriendDto>>())!;

    [Fact]
    public async Task One_asks_the_other_accepts_and_each_profile_says_where_they_stand()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        (await Profile(anna.Client, luca.Id)).Friendship.Should().Be(FriendshipState.None);

        (await Ask(anna, luca)).Should().Be(FriendshipState.Outgoing);
        (await Ask(anna, luca)).Should().Be(FriendshipState.Outgoing, "asking twice changes nothing");
        (await Profile(anna.Client, luca.Id)).Friendship.Should().Be(FriendshipState.Outgoing);
        (await Profile(luca.Client, anna.Id)).Friendship.Should().Be(FriendshipState.Incoming);
        (await Requests(luca)).Select(r => r.Person.UserId).Should().Equal(anna.Id);
        (await Requests(anna)).Should().BeEmpty();
        // Only the one who was asked can say yes.
        (await anna.Client.PostAsync($"/api/users/{luca.Id}/friend-request/accept", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Accept(luca, anna)).Should().Be(FriendshipState.Friends);
        (await Profile(anna.Client, luca.Id)).Friendship.Should().Be(FriendshipState.Friends);
        (await Profile(luca.Client, anna.Id)).Friendship.Should().Be(FriendshipState.Friends);
        (await Requests(luca)).Should().BeEmpty();
        (await FriendsOf(anna.Client, anna.Id)).Items.Select(f => f.Person.UserId).Should().Equal(luca.Id);

        // Nothing to say on one's own profile, nor signed out.
        (await Profile(anna.Client, anna.Id)).Friendship.Should().BeNull();
        (await Profile(_f.CreateClient(), anna.Id)).Friendship.Should().BeNull();
    }

    [Fact]
    public async Task Asking_someone_who_asked_you_is_saying_yes_and_either_can_cancel_decline_or_end_it()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        await Ask(anna, luca);
        (await Ask(luca, anna)).Should().Be(FriendshipState.Friends);

        (await Remove(luca, anna)).Should().Be(FriendshipState.None);
        (await Profile(anna.Client, luca.Id)).Friendship.Should().Be(FriendshipState.None);

        await Ask(anna, luca);
        (await Remove(anna, luca)).Should().Be(FriendshipState.None, "cancelled");
        await Ask(anna, luca);
        (await Remove(luca, anna)).Should().Be(FriendshipState.None, "declined");
        (await Requests(luca)).Should().BeEmpty();
        (await Remove(luca, anna)).Should().Be(FriendshipState.None, "nothing left to remove is not an error");
    }

    [Fact]
    public async Task A_friend_sees_a_private_profile_a_pending_request_does_not_and_it_closes_again_when_it_ends()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        var other = await _f.UserAsync();
        (await anna.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 2, completed = true })).EnsureSuccessStatusCode();
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).EnsureSuccessStatusCode();

        await Ask(luca, anna);
        (await Profile(luca.Client, anna.Id)).ContentHidden.Should().BeTrue("a request is not enough");

        await Accept(anna, luca);
        var seen = await Profile(luca.Client, anna.Id);
        seen.ContentHidden.Should().BeFalse();
        seen.Stats.Completed.Should().Be(1);
        (await (await luca.Client.GetAsync($"/api/users/{anna.Id}/history")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!.Total.Should().Be(1);
        (await FriendsOf(luca.Client, anna.Id)).Items.Should().ContainSingle(f => f.Person.UserId == luca.Id);
        // Everyone else still sees name and photo only, and no friends.
        (await Profile(other.Client, anna.Id)).ContentHidden.Should().BeTrue();
        (await FriendsOf(other.Client, anna.Id)).Total.Should().Be(0);

        await Remove(anna, luca);
        (await Profile(luca.Client, anna.Id)).ContentHidden.Should().BeTrue();
        (await (await luca.Client.GetAsync($"/api/users/{anna.Id}/history")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!.Total.Should().Be(0);
    }

    [Fact]
    public async Task A_public_profiles_friends_are_there_for_anyone_signed_in()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        await Ask(anna, luca);
        await Accept(luca, anna);
        // Who knows whom needs an account, as the names of who completed a boulder do.
        (await _f.CreateClient().GetAsync($"/api/users/{anna.Id}/friends")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await FriendsOf((await _f.UserAsync()).Client, luca.Id)).Items.Select(f => f.Person.UserId).Should().Equal(anna.Id);
    }

    [Fact]
    public async Task Blocking_ends_the_friendship_and_a_blocked_person_is_never_told()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        await Ask(anna, luca);
        await Accept(luca, anna);

        (await anna.Client.PutAsJsonAsync($"/api/users/me/blocks/{luca.Id}", new { reason = (string?)null })).EnsureSuccessStatusCode();
        (await Profile(luca.Client, anna.Id)).Friendship.Should().Be(FriendshipState.None);
        (await FriendsOf(anna.Client, anna.Id)).Total.Should().Be(0);

        // Luca asks again: it looks sent, like any other request, and Anna never sees it while she blocks him.
        (await Ask(luca, anna)).Should().Be(FriendshipState.Outgoing);
        (await Requests(anna)).Should().BeEmpty();
        (await Profile(anna.Client, luca.Id)).Friendship.Should().Be(FriendshipState.None);
        (await anna.Client.PostAsync($"/api/users/{luca.Id}/friend-request/accept", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // Anna knows she blocked him: to ask, she unblocks first.
        (await anna.Client.PostAsync($"/api/users/{luca.Id}/friend-request", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Nobody_befriends_themselves_strangers_to_the_app_or_anyone_while_signed_out()
    {
        var anna = await _f.UserAsync();
        (await anna.Client.PostAsync($"/api/users/{anna.Id}/friend-request", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await anna.Client.PostAsync($"/api/users/{Guid.NewGuid()}/friend-request", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var anonymous = _f.CreateClient();
        (await anonymous.PostAsync($"/api/users/{anna.Id}/friend-request", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/users/me/friend-requests")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.DeleteAsync($"/api/users/{anna.Id}/friendship")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Requests_have_a_daily_limit_against_spam()
    {
        var anna = await _f.UserAsync();
        for (var i = 0; i < FriendshipService.RequestsPerDay; i++) await Ask(anna, await _f.UserAsync());
        var res = await anna.Client.PostAsync($"/api/users/{(await _f.UserAsync()).Id}/friend-request", null);
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
