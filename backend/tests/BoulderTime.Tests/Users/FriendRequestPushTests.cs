using System.Collections.Concurrent;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Notifications;
using BoulderTime.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Users;

/// <summary>
/// A friend request buzzes the phone of the person asked (ADR-063): once per request, in their language, opening the
/// asker's profile; never for someone who turned it off or who blocked the asker.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class FriendRequestPushTests : IAsyncLifetime
{
    private sealed class FakePush : IPushQueue
    {
        public ConcurrentQueue<(Guid UserId, PushMessage Message)> Sent { get; } = new();
        public void Enqueue(Guid userId, PushMessage message) => Sent.Enqueue((userId, message));
    }

    private readonly FakePush _push = new();
    private readonly ApiFactory _f;

    public FriendRequestPushTests(PostgresFixture postgres) =>
        _f = new ApiFactory(postgres) { OverrideServices = s => s.AddSingleton<IPushQueue>(_push) };

    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Ask(TestUser from, TestUser to) =>
        (await from.Client.PostAsync($"/api/users/{to.Id}/friend-request", null)).EnsureSuccessStatusCode();

    private static async Task Rename(TestUser who, string name) =>
        (await who.Client.PatchAsJsonAsync("/api/users/me", new { displayName = name })).EnsureSuccessStatusCode();

    [Fact]
    public async Task A_request_notifies_the_person_asked_once_in_their_language_and_opens_the_askers_profile()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync(language: "it");
        await Rename(anna, "Anna");

        await Ask(anna, luca);
        await Ask(anna, luca); // asking again changes nothing, and says nothing
        var sent = _push.Sent.Should().ContainSingle().Subject;
        sent.UserId.Should().Be(luca.Id);
        sent.Message.Title.Should().Be("Anna ti ha chiesto l'amicizia");
        sent.Message.Url.Should().Be($"/users/{anna.Id}");
        sent.Message.Tag.Should().Be($"friend-request:{anna.Id}");

        // Saying yes by asking back is an answer, not a new request: Anna isn't notified.
        await Ask(luca, anna);
        _push.Sent.Should().ContainSingle();
    }

    [Fact]
    public async Task Nobody_is_notified_who_turned_friend_requests_off_or_blocked_the_asker()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        var sara = await _f.UserAsync();

        var off = await (await luca.Client.PutAsJsonAsync("/api/users/me/notification-settings", new { friendRequests = false }))
            .ReadAsync<NotificationSettingsDto>();
        off!.FriendRequests.Should().BeFalse();
        off.GymUpdates.Should().BeTrue("the other switches stay as they were");
        await Ask(anna, luca);

        (await sara.Client.PutAsJsonAsync($"/api/users/me/blocks/{anna.Id}", new { reason = (string?)null })).EnsureSuccessStatusCode();
        await Ask(anna, sara);

        _push.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task Friend_requests_are_on_by_default_and_older_apps_leave_the_switch_alone()
    {
        var luca = await _f.UserAsync();
        (await (await luca.Client.GetAsync("/api/users/me/notification-settings")).ReadAsync<NotificationSettingsDto>())!
            .FriendRequests.Should().BeTrue();

        await luca.Client.PutAsJsonAsync("/api/users/me/notification-settings", new { friendRequests = false });
        // An app that doesn't know the switch sends the others only.
        var after = await (await luca.Client.PutAsJsonAsync("/api/users/me/notification-settings", new { gymUpdates = false }))
            .ReadAsync<NotificationSettingsDto>();
        after!.FriendRequests.Should().BeFalse();
    }
}
