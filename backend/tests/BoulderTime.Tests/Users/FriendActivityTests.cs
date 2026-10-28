using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Users;

/// <summary>
/// The list of friends with what each climbed lately, ordered by name as always or by activity, and the friends in
/// common on a request (1.1): added fields only, shown only where the viewer may see that friend's content.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FriendActivityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<TestUser> Named(string name)
    {
        var u = await _f.UserAsync();
        await _f.WithDbAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bouldertime.users SET display_name = {name} WHERE id = {u.Id}"));
        return u;
    }

    private static async Task Befriend(TestUser a, TestUser b)
    {
        (await a.Client.PostAsync($"/api/users/{b.Id}/friend-request", null)).EnsureSuccessStatusCode();
        (await b.Client.PostAsync($"/api/users/{a.Id}/friend-request/accept", null)).EnsureSuccessStatusCode();
    }

    private static Task Send(TestUser who, Guid boulderId) =>
        who.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 2, completed = true });

    private static async Task<PagedResult<FriendDto>> FriendsOf(TestUser reader, Guid userId, string query = "") =>
        (await (await reader.Client.GetAsync($"/api/users/{userId}/friends{query}")).ReadAsync<PagedResult<FriendDto>>())!;

    private Task Backdate(Guid userId, Guid boulderId, DateTimeOffset at) => _f.WithDbAsync(db =>
        db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bouldertime.boulder_attempts SET completed_at = {at}, updated_at = {at} WHERE user_id = {userId} AND boulder_id = {boulderId}"));

    [Fact]
    public async Task Friends_come_with_their_latest_send_and_the_most_active_first_when_asked()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var green = await w.BoulderAsync("6A", "Green");
        var other = await w.BoulderAsync("6B");
        var me = await _f.UserAsync();
        var anna = await Named("Anna");
        var bruno = await Named("Bruno");
        var carla = await Named("Carla");
        foreach (var f in new[] { anna, bruno, carla }) await Befriend(me, f);
        await Send(bruno, green.Id);           // Bruno: today
        await Send(anna, other.Id);            // Anna: two months ago
        await Backdate(anna.Id, other.Id, DateTimeOffset.UtcNow.AddMonths(-2));
        await Send(anna, green.Id);            // ...and once more, earlier still
        await Backdate(anna.Id, green.Id, DateTimeOffset.UtcNow.AddMonths(-3));
        // Carla never sent anything.

        // As always without asking: by name, same fields as before plus the activity.
        var byName = await FriendsOf(me, me.Id);
        byName.Items.Select(f => f.Person.DisplayName).Should().Equal("Anna", "Bruno", "Carla");

        var byActivity = await FriendsOf(me, me.Id, "?sort=activity");
        byActivity.Items.Select(f => f.Person.DisplayName).Should().Equal("Bruno", "Anna", "Carla");
        var b = byActivity.Items[0].Activity!;
        b.BoulderId.Should().Be(green.Id);
        b.GymName.Should().Be(w.Gym.Name);
        b.Grade!.Label.Should().Be("6A");
        b.SentThisMonth.Should().Be(1);
        var a = byActivity.Items[1].Activity!;
        a.BoulderId.Should().Be(other.Id, "Anna's latest session");
        a.SentThisMonth.Should().Be(0);
        byActivity.Items[2].Activity.Should().BeNull("Carla never sent anything");
    }

    [Fact]
    public async Task A_friend_shows_the_hardest_boulder_of_their_latest_session_even_when_sent_first()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var hard = await w.BoulderAsync("7A");
        var easy = await w.BoulderAsync("6A");
        var older = await w.BoulderAsync("8A");
        var me = await _f.UserAsync();
        var anna = await Named("Anna");
        await Befriend(me, anna);
        var day = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(-7), TimeSpan.Zero);
        foreach (var b in new[] { hard, easy, older }) await Send(anna, b.Id);
        await Backdate(anna.Id, hard.Id, day.AddHours(9));     // the hardest of the session, sent first
        await Backdate(anna.Id, easy.Id, day.AddHours(16));    // the latest send
        await Backdate(anna.Id, older.Id, day.AddHours(-12));  // harder still, but the session before

        var a = (await FriendsOf(me, me.Id, "?sort=activity")).Items.Single().Activity!;
        a.BoulderId.Should().Be(hard.Id);
        a.Grade!.Label.Should().Be("7A");
        a.LastSendAt.Should().Be(day.AddHours(16), "when the session ended, as the list is ordered by it");
    }

    [Fact]
    public async Task A_private_friend_s_activity_is_shown_only_to_who_may_see_it_and_never_orders_the_list_for_others()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var boulder = await w.BoulderAsync("6A");
        var owner = await Named("Owner");
        var hidden = await Named("Zeno");
        var open = await Named("Alba");
        await Befriend(owner, hidden);
        await Befriend(owner, open);
        await Send(hidden, boulder.Id);
        (await hidden.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).EnsureSuccessStatusCode();

        // The owner is Zeno's friend: they see it.
        (await FriendsOf(owner, owner.Id, "?sort=activity")).Items.Single(f => f.Person.DisplayName == "Zeno").Activity.Should().NotBeNull();

        // A stranger reading the owner's public list: Zeno is listed, without activity, and not put first by it.
        var stranger = await _f.UserAsync();
        var seen = await FriendsOf(stranger, owner.Id, "?sort=activity");
        seen.Items.Single(f => f.Person.DisplayName == "Zeno").Activity.Should().BeNull();
        seen.Items.Select(f => f.Person.DisplayName).Should().Equal(new[] { "Alba", "Zeno" }, "no visible activity: by name");
    }

    [Fact]
    public async Task A_request_says_how_many_friends_the_two_have_in_common()
    {
        var me = await _f.UserAsync();
        var asker = await _f.UserAsync();
        var both1 = await _f.UserAsync();
        var both2 = await _f.UserAsync();
        var onlyMine = await _f.UserAsync();
        foreach (var x in new[] { both1, both2, onlyMine }) await Befriend(me, x);
        foreach (var x in new[] { both1, both2 }) await Befriend(asker, x);
        (await asker.Client.PostAsync($"/api/users/{me.Id}/friend-request", null)).EnsureSuccessStatusCode();

        var requests = (await (await me.Client.GetAsync("/api/users/me/friend-requests")).ReadAsync<List<FriendRequestDto>>())!;
        requests.Single().MutualFriends.Should().Be(2);
    }

    [Fact]
    public async Task What_the_1_0_apps_read_is_unchanged_only_fields_are_added()
    {
        var me = await _f.UserAsync();
        var friend = await _f.UserAsync();
        await Befriend(me, friend);
        var raw = await (await me.Client.GetAsync($"/api/users/{me.Id}/friends")).Content.ReadAsStringAsync();
        var item = JsonDocument.Parse(raw).RootElement.GetProperty("items")[0];
        item.EnumerateObject().Select(p => p.Name).Should().Contain(["person", "since"]);
        item.GetProperty("person").EnumerateObject().Select(p => p.Name).Should().Contain(["userId", "displayName", "avatarUrl"]);
        (await me.Client.GetAsync($"/api/users/{me.Id}/friends?sort=nonsense")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
