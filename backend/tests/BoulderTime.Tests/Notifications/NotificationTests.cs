using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Notifications;
using BoulderTime.Domain.Notifications;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Community;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Notifications;

[Collection(DatabaseCollection.Name)]
public sealed class NotificationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<List<NotificationDto>> InboxAsync(TestUser u) =>
        (await (await u.Client.GetAsync("/api/notifications")).ReadAsync<PagedResult<NotificationDto>>())!.Items.ToList();

    private static object Announcement(bool notify, Guid? sectorId = null, string type = "ANNOUNCEMENT") =>
        new { type, title = "New circuit in the Cave", content = "Twenty fresh problems this Friday.", notifyFollowers = notify, sectorId, eventDate = type == "EVENT" ? DateTimeOffset.UtcNow.AddDays(3) : (DateTimeOffset?)null };

    [Fact]
    public async Task Announcements_reach_followers_who_opted_in_once_and_never_the_author()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fan = await _f.UserAsync();
        var muted = await _f.UserAsync();
        var settingsOff = await _f.UserAsync();
        var sectorOnly = await _f.UserAsync();
        var both = await _f.UserAsync();
        var stranger = await _f.UserAsync();
        await fan.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await muted.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { notificationsEnabled = false });
        await settingsOff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await settingsOff.Client.PutAsJsonAsync("/api/users/me/notification-settings", new { gymUpdates = false });
        await sectorOnly.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        await both.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await both.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        await w.Staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });

        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements", Announcement(notify: true, w.Sector.Id))).StatusCode.Should().Be(HttpStatusCode.Created);

        (await InboxAsync(fan)).Should().ContainSingle(n => n.Type == NotificationType.GymAnnouncement && n.Link.Contains(w.Gym.Slug));
        (await InboxAsync(sectorOnly)).Should().ContainSingle();
        (await InboxAsync(both)).Should().ContainSingle();
        (await InboxAsync(muted)).Should().BeEmpty();
        (await InboxAsync(settingsOff)).Should().BeEmpty();
        (await InboxAsync(stranger)).Should().BeEmpty();
        (await InboxAsync(w.Staff)).Should().BeEmpty();
    }

    [Fact]
    public async Task Announcements_without_notify_are_published_silently_and_climbers_cannot_publish()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fan = await _f.UserAsync();
        await fan.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });

        (await fan.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements", Announcement(true))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements", Announcement(false))).EnsureSuccessStatusCode();
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements", new { type = "EVENT", title = "Party", content = "Come!" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await InboxAsync(fan)).Should().BeEmpty();
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/announcements")).ReadAsync<PagedResult<AnnouncementDto>>())!;
        list.Items.Should().ContainSingle(a => !a.NotifiedFollowers);
    }

    [Fact]
    public async Task Retracing_a_sector_sends_one_notification_per_person_not_one_per_boulder()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var boulders = new List<Guid>();
        for (var i = 0; i < 25; i++) boulders.Add((await w.BoulderAsync()).Id);
        var sectorFan = await _f.UserAsync();
        var boulderFan = await _f.UserAsync();
        var everything = await _f.UserAsync();
        await sectorFan.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        foreach (var id in boulders.Take(5)) await boulderFan.Client.PutAsJsonAsync($"/api/boulders/{id}/follow", new { });
        await everything.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        foreach (var id in boulders.Take(3)) await everything.Client.PutAsJsonAsync($"/api/boulders/{id}/follow", new { });

        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = boulders, notifyFollowers = true })).EnsureSuccessStatusCode();

        foreach (var user in new[] { sectorFan, boulderFan, everything })
        {
            var inbox = await InboxAsync(user);
            inbox.Should().ContainSingle();
            inbox[0].Type.Should().Be(NotificationType.SectorRetraced);
            inbox[0].Title.Should().Be("Sector Cave has been retraced");
            inbox[0].Body.Should().Contain("25 boulders");
        }
        (await _f.Db(db => db.Notifications.CountAsync())).Should().Be(3);
    }

    [Fact]
    public async Task Removing_without_notify_sends_nothing()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var fan = await _f.UserAsync();
        await fan.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });

        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { b.Id } });

        (await InboxAsync(fan)).Should().BeEmpty();
    }

    [Fact]
    public async Task Official_beta_notifies_boulder_followers_and_climbers_projecting_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var follower = await _f.UserAsync();
        var projecting = await _f.UserAsync();
        var sent = await _f.UserAsync();
        await follower.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/follow", new { });
        await projecting.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 6, completed = false });
        await sent.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 1, completed = true });

        var path = await w.Staff.UploadVideoAsync(_f, b.Id, "BETA");
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/beta", new { storagePath = path })).EnsureSuccessStatusCode();
        // Caption-only edit does not notify again.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/beta", new { storagePath = path, caption = "Typo fix" })).EnsureSuccessStatusCode();

        (await InboxAsync(follower)).Should().ContainSingle(n => n.Type == NotificationType.OfficialBeta);
        (await InboxAsync(projecting)).Should().ContainSingle(n => n.Type == NotificationType.OfficialBeta);
        (await InboxAsync(sent)).Should().BeEmpty();
    }

    [Fact]
    public async Task Comment_bursts_collapse_into_one_unread_notification_and_start_fresh_after_reading()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var follower = await _f.UserAsync();
        var chatty = await _f.UserAsync();
        await follower.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/follow", new { });

        for (var i = 0; i < 3; i++) await chatty.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = $"Comment {i}" });
        await follower.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "My own comment" });

        var inbox = await InboxAsync(follower);
        inbox.Should().ContainSingle();
        inbox[0].Count.Should().Be(3);
        inbox[0].Title.Should().Be("3 new comments on a boulder you follow");
        (await (await follower.Client.GetAsync("/api/notifications/unread-count")).ReadAsync<UnreadCountDto>())!.Unread.Should().Be(1);

        (await follower.Client.PostAsync($"/api/notifications/{inbox[0].Id}/read", null)).EnsureSuccessStatusCode();
        await chatty.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "One more" });

        var after = await InboxAsync(follower);
        after.Should().HaveCount(2);
        after[0].IsRead.Should().BeFalse();
        after[0].Count.Should().Be(1);
    }

    [Fact]
    public async Task Moderation_results_reach_the_uploader_and_the_reporter()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var reporter = await _f.UserAsync();
        var approved = await author.SubmitVideoAsync(_f, b.Id);
        var rejected = await author.SubmitVideoAsync(_f, b.Id);
        await w.Staff.Client.PostAsync($"/api/videos/{approved.Id}/approve", null);
        await w.Staff.Client.PostAsJsonAsync($"/api/videos/{rejected.Id}/reject", new { reason = "Shaky and dark" });

        var comment = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "hmm" })).ReadAsync<Application.Community.CommentDto>())!;
        var report = (await (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "COMMENT", entityId = comment.Id, reason = "SPAM" })).ReadAsync<Application.Community.ReportDto>())!;
        await w.Staff.Client.PostAsJsonAsync($"/api/reports/{report.Id}/dismiss", new { });

        var authorInbox = await InboxAsync(author);
        authorInbox.Should().ContainSingle(n => n.Type == NotificationType.VideoApproved);
        authorInbox.Should().ContainSingle(n => n.Type == NotificationType.VideoRejected && n.Body == "Shaky and dark");
        (await InboxAsync(reporter)).Should().ContainSingle(n => n.Type == NotificationType.ReportReviewed && n.Link == $"/boulders/{b.Id}");
    }

    [Fact]
    public async Task Users_manage_only_their_own_inbox()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fan = await _f.UserAsync();
        var other = await _f.UserAsync();
        await fan.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { });
        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements", Announcement(true));
        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements", Announcement(true, type: "EVENT"));
        var inbox = await InboxAsync(fan);

        (await other.Client.PostAsync($"/api/notifications/{inbox[0].Id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _f.CreateClient().GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await (await fan.Client.PostAsync("/api/notifications/read-all", null)).ReadAsync<UnreadCountDto>())!.Unread.Should().Be(0);
        (await InboxAsync(fan)).Should().OnlyContain(n => n.IsRead);
        (await (await fan.Client.GetAsync("/api/users/me/notification-settings")).ReadAsync<NotificationSettingsDto>())!.GymUpdates.Should().BeTrue();
    }
}
