using System.Net;
using System.Net.Http.Json;
using BoulderTime.Domain.Notifications;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Push;

/// <summary>Registering a device for notifications: each person registers their own, and only their own.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class PushSubscriptionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static object Device(string endpoint = "https://push.example/abc") =>
        new { endpoint, keys = new { p256dh = "BJxc0000000000000000000000000000000000000000", auth = "c2VjcmV0MTIzNDU2" } };

    [Fact]
    public async Task A_device_registers_once_however_many_times_it_subscribes()
    {
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync("/api/users/me/push", Device())).EnsureSuccessStatusCode();
        (await climber.Client.PostAsJsonAsync("/api/users/me/push", Device())).EnsureSuccessStatusCode();

        (await _f.Db(db => db.PushSubscriptions.CountAsync(s => s.UserId == climber.Id))).Should().Be(1);
    }

    [Fact]
    public async Task A_device_that_changes_hands_follows_the_person_signed_in_on_it()
    {
        var first = await _f.UserAsync();
        var second = await _f.UserAsync();
        await first.Client.PostAsJsonAsync("/api/users/me/push", Device());

        await second.Client.PostAsJsonAsync("/api/users/me/push", Device());

        // The endpoint is the device's address: it can only belong to one account at a time.
        (await _f.Db(db => db.PushSubscriptions.CountAsync(s => s.UserId == first.Id))).Should().Be(0);
        (await _f.Db(db => db.PushSubscriptions.CountAsync(s => s.UserId == second.Id))).Should().Be(1);
    }

    [Fact]
    public async Task A_store_app_registers_with_its_notification_token_instead_of_browser_keys()
    {
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync("/api/users/me/push", new { platform = "NATIVE", token = "fcm-token-abc123" }))
            .EnsureSuccessStatusCode();

        var device = await _f.Db(db => db.PushSubscriptions.FirstAsync(s => s.UserId == climber.Id));
        device.Platform.Should().Be(PushPlatform.Native);
        device.Address.Should().Be("fcm-token-abc123");
        device.P256dh.Should().BeEmpty();

        // Firebase reissues tokens; the same device registering again updates its row rather than adding one.
        (await climber.Client.PostAsJsonAsync("/api/users/me/push", new { platform = "NATIVE", token = "fcm-token-abc123" }))
            .EnsureSuccessStatusCode();
        (await _f.Db(db => db.PushSubscriptions.CountAsync(s => s.UserId == climber.Id))).Should().Be(1);
    }

    [Fact]
    public async Task A_store_app_without_a_usable_token_is_refused()
    {
        var climber = await _f.UserAsync();

        foreach (var token in new[] { "", "   ", "has space" })
        {
            (await climber.Client.PostAsJsonAsync("/api/users/me/push", new { platform = "NATIVE", token }))
                .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        (await _f.Db(db => db.PushSubscriptions.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task A_phone_and_a_browser_are_two_devices_of_the_same_person()
    {
        var climber = await _f.UserAsync();

        await climber.Client.PostAsJsonAsync("/api/users/me/push", Device());
        await climber.Client.PostAsJsonAsync("/api/users/me/push", new { platform = "NATIVE", token = "fcm-token-xyz" });

        var devices = await _f.Db(db => db.PushSubscriptions.Where(s => s.UserId == climber.Id).ToListAsync());
        devices.Select(d => d.Platform).Should().BeEquivalentTo(new[] { PushPlatform.Web, PushPlatform.Native });
    }

    [Fact]
    public async Task Turning_notifications_off_removes_the_device()
    {
        var climber = await _f.UserAsync();
        await climber.Client.PostAsJsonAsync("/api/users/me/push", Device());

        (await climber.Client.DeleteAsync("/api/users/me/push?address=https%3A%2F%2Fpush.example%2Fabc")).EnsureSuccessStatusCode();

        (await _f.Db(db => db.PushSubscriptions.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task Incomplete_or_untrusted_subscriptions_are_refused_and_anonymous_callers_get_nowhere()
    {
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync("/api/users/me/push", new { endpoint = "https://push.example/x", keys = new { p256dh = "", auth = "" } }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await climber.Client.PostAsJsonAsync("/api/users/me/push", Device("http://push.example/insecure")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _f.CreateClient().PostAsJsonAsync("/api/users/me/push", Device()))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _f.Db(db => db.PushSubscriptions.CountAsync())).Should().Be(0);
    }
}
