using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Community;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Users;

/// <summary>Deleting an account: easy to ask for, hard to do by accident, and undoable for a week.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccountDeletionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task The_address_has_to_be_typed_out_before_anything_happens()
    {
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await climber.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = "someone.else@example.com" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await _f.Db(db => db.Users.Where(u => u.Id == climber.Id).Select(u => u.DeletionRequestedAt).FirstAsync()))
            .Should().BeNull();
    }

    [Fact]
    public async Task Asking_hides_the_account_and_leaves_a_week_to_undo_it()
    {
        var climber = await _f.UserAsync();

        var status = (await (await climber.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = climber.Email })).ReadAsync<DeletionStatusDto>())!;
        status.PendingDeletion.Should().BeTrue();
        status.ErasedAfter.Should().NotBeNull();

        // Out of the leaderboards immediately: being on the way out shouldn't leave you ranked.
        (await _f.Db(db => db.Users.Where(u => u.Id == climber.Id).Select(u => u.LeaderboardOptOut).FirstAsync())).Should().BeTrue();

        var back = (await (await climber.Client.DeleteAsync("/api/users/me/deletion")).ReadAsync<DeletionStatusDto>())!;
        back.PendingDeletion.Should().BeFalse();
        (await _f.Db(db => db.Users.Where(u => u.Id == climber.Id).Select(u => u.LeaderboardOptOut).FirstAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task The_only_owner_of_a_gym_is_told_to_hand_it_over_first()
    {
        var gym = await _f.GymAsync(GymStatus.Active);
        var owner = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);

        var refused = await owner.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = owner.Email });
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // With a second owner in place, leaving is fine.
        var second = await _f.UserAsync();
        await _f.StaffAsync(gym, second, GymRole.Owner);
        (await owner.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = owner.Email })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Erasing_removes_what_is_personal_and_leaves_the_gyms_content_standing()
    {
        var world = await Climbing.ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/attempt", new { attempts = 3, completed = true });
        await climber.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { body = "Bel blocco" });
        await climber.Client.PutAsJsonAsync($"/api/gyms/{world.Gym.Id}/follow", new { });
        await climber.Client.PostAsJsonAsync("/api/users/me/push", new { endpoint = "https://push.example/d", keys = new { p256dh = "BJxc0000000000000000000000000000000000000000", auth = "c2VjcmV0MTIzNDU2" } });

        await using (var scope = _f.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AccountEraser>().EraseAsync(climber.Id);

        (await _f.Db(db => db.BoulderAttempts.CountAsync(x => x.UserId == climber.Id))).Should().Be(0);
        (await _f.Db(db => db.Comments.CountAsync(x => x.UserId == climber.Id))).Should().Be(0);
        (await _f.Db(db => db.GymFollows.CountAsync(x => x.UserId == climber.Id))).Should().Be(0);
        (await _f.Db(db => db.PushSubscriptions.CountAsync(x => x.UserId == climber.Id))).Should().Be(0);

        // A nameless placeholder stays so the gym's own content keeps working.
        var erased = await _f.Db(db => db.Users.FirstAsync(u => u.Id == climber.Id));
        erased.DisplayName.Should().Be("Utente eliminato");
        erased.Email.Should().NotBe(climber.Email);
        (await _f.Db(db => db.Boulders.CountAsync(b => b.Id == boulder.Id))).Should().Be(1);
    }

    [Fact]
    public async Task Asking_takes_the_profile_and_the_comments_out_of_the_app_until_it_is_withdrawn()
    {
        var world = await Climbing.ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        var reader = await _f.UserAsync();
        (await climber.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Bel blocco" })).EnsureSuccessStatusCode();

        async Task<int> CommentsSeenBy(TestUser who) =>
            (await (await who.Client.GetAsync($"/api/boulders/{boulder.Id}/comments")).ReadAsync<PagedResult<CommentDto>>())!.Total;

        (await climber.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = climber.Email })).EnsureSuccessStatusCode();
        (await CommentsSeenBy(reader)).Should().Be(0);
        (await reader.Client.GetAsync($"/api/users/{climber.Id}/profile")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        // The person still sees their own, so changing their mind finds everything where it was.
        (await CommentsSeenBy(climber)).Should().Be(1);
        (await climber.Client.GetAsync($"/api/users/{climber.Id}/profile")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await climber.Client.DeleteAsync("/api/users/me/deletion")).EnsureSuccessStatusCode();
        (await CommentsSeenBy(reader)).Should().Be(1);
        (await reader.Client.GetAsync($"/api/users/{climber.Id}/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Erasing_also_removes_reports_on_their_content_their_settings_and_invitations_to_their_address()
    {
        var world = await Climbing.ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        var reporter = await _f.UserAsync();
        var comment = (await (await climber.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Bel blocco" })).ReadAsync<CommentDto>())!;
        (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "COMMENT", entityId = comment.Id, reason = "SPAM" })).EnsureSuccessStatusCode();
        (await climber.Client.PutAsJsonAsync("/api/users/me/notification-settings", new { gymUpdates = false })).EnsureSuccessStatusCode();
        await _f.Db(async db =>
        {
            db.StaffInvitations.Add(StaffInvitation.Create(world.Gym.Id, climber.Email, GymRole.Staff, world.Staff.Id, DateTimeOffset.UtcNow));
            return await db.SaveChangesAsync();
        });

        await using (var scope = _f.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AccountEraser>().EraseAsync(climber.Id);

        (await _f.Db(db => db.Reports.CountAsync(r => r.EntityId == comment.Id))).Should().Be(0);
        (await _f.Db(db => db.NotificationSettings.CountAsync(x => x.UserId == climber.Id))).Should().Be(0);
        (await _f.Db(db => db.StaffInvitations.CountAsync(x => x.Email == climber.Email.ToLower()))).Should().Be(0);
    }

    [Fact]
    public async Task An_erased_account_is_not_erased_again_by_later_runs()
    {
        var climber = await _f.UserAsync();
        (await climber.Client.PostAsJsonAsync("/api/users/me/deletion", new { confirmEmail = climber.Email })).EnsureSuccessStatusCode();
        var longAgo = DateTimeOffset.UtcNow - AccountDeletionService.GracePeriod - TimeSpan.FromDays(1);
        await _f.Db(db => db.Users.Where(u => u.Id == climber.Id).ExecuteUpdateAsync(x => x.SetProperty(u => u.DeletionRequestedAt, (DateTimeOffset?)longAgo)));

        async Task<int> RunAsync()
        {
            await using var scope = _f.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AccountEraser>().EraseDueAsync();
        }

        (await RunAsync()).Should().Be(1);
        // Anonymise stamps the erasure time; make that old too, as it will be a week later.
        await _f.Db(db => db.Users.Where(u => u.Id == climber.Id).ExecuteUpdateAsync(x => x.SetProperty(u => u.DeletionRequestedAt, (DateTimeOffset?)longAgo)));
        (await RunAsync()).Should().Be(0);
    }
}
