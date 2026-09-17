using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Notifications;
using BoulderTime.Domain.Notifications;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Notifications;

[Collection(DatabaseCollection.Name)]
public sealed class NewBoulderNotificationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<List<NotificationDto>> InboxAsync(TestUser u) =>
        (await (await u.Client.GetAsync("/api/notifications")).ReadAsync<PagedResult<NotificationDto>>())!.Items.ToList();

    [Fact]
    public async Task Sector_followers_hear_about_a_new_boulder_and_a_setting_session_collapses_into_one_notification()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fan = await _f.UserAsync();
        await fan.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });

        var first = await w.BoulderAsync("6A");
        var single = await InboxAsync(fan);
        single.Should().ContainSingle();
        single[0].Type.Should().Be(NotificationType.NewBouldersInSector);
        single[0].Title.Should().Be("New boulder in Cave");
        single[0].Body.Should().Contain("6A · Blue holds");
        single[0].Link.Should().Be($"/boulders/{first.Id}");

        for (var i = 0; i < 4; i++) await w.BoulderAsync("6B");

        var collapsed = await InboxAsync(fan);
        collapsed.Should().ContainSingle();
        collapsed[0].Count.Should().Be(5);
        collapsed[0].Title.Should().Be("5 new boulders in Cave");
        collapsed[0].Link.Should().Be($"/gyms/{w.Gym.Slug}");
        (await InboxAsync(w.Staff)).Should().BeEmpty();
    }

    [Fact]
    public async Task Gym_followers_get_a_gym_level_notice_and_nobody_gets_two_for_the_same_boulder()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var slab = await _f.SectorAsync(w.Gym, "Slab");
        var gymFan = await _f.UserAsync();
        var both = await _f.UserAsync();
        await gymFan.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await both.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await both.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });

        await w.BoulderAsync("6A");  // in Cave
        await w.BoulderAsync("6A");  // in Cave

        (await InboxAsync(gymFan)).Should().ContainSingle(n => n.Type == NotificationType.NewBouldersAtGym && n.Title == "2 new boulders at Crimp Factory");
        (await InboxAsync(both)).Should().ContainSingle(n => n.Type == NotificationType.NewBouldersInSector && n.Title == "2 new boulders in Cave");

        // A boulder in a sector "both" doesn't follow reaches them through the gym instead.
        var photo = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", BoulderTestData.BoulderBody(slab, photo, w.Font, "7A"))).EnsureSuccessStatusCode();
        var bothInbox = await InboxAsync(both);
        bothInbox.Should().HaveCount(2);
        bothInbox.Should().ContainSingle(n => n.Type == NotificationType.NewBouldersAtGym && n.Body!.Contains("Slab"));
    }

    [Fact]
    public async Task Muted_sector_follows_and_disabled_categories_are_respected()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var mutedSector = await _f.UserAsync();
        var categoryOff = await _f.UserAsync();
        await mutedSector.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await mutedSector.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { notificationsEnabled = false });
        await categoryOff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        await categoryOff.Client.PutAsJsonAsync("/api/users/me/notification-settings", new { sectorUpdates = false });

        await w.BoulderAsync("6A");

        (await InboxAsync(mutedSector)).Should().BeEmpty();
        (await InboxAsync(categoryOff)).Should().BeEmpty();
    }

    [Fact]
    public async Task After_reading_the_next_new_boulder_starts_a_fresh_notification()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fan = await _f.UserAsync();
        await fan.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        await w.BoulderAsync("6A");
        await w.BoulderAsync("6A");
        await fan.Client.PostAsync("/api/notifications/read-all", null);

        await w.BoulderAsync("6C");

        var inbox = await InboxAsync(fan);
        inbox.Should().HaveCount(2);
        inbox[0].IsRead.Should().BeFalse();
        inbox[0].Title.Should().Be("New boulder in Cave");
        (await _f.Db(db => db.Notifications.CountAsync(n => n.UserId == fan.Id))).Should().Be(2);
    }
}
