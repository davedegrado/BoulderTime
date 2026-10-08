using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Community;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Users;

/// <summary>
/// Reporting a person, and suspending an account across BoulderTime (ADR-037). Anyone reports; only BoulderTime
/// decides; a suspended account can do nothing but read why and delete itself, and what it wrote disappears for
/// everyone until the suspension is lifted.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class AccountSuspensionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<IReadOnlyList<string>> CommentsSeenBy(HttpClient viewer, Guid boulderId) =>
        (await (await viewer.GetAsync($"/api/boulders/{boulderId}/comments")).ReadAsync<PagedResult<CommentDto>>())!
            .Items.Select(c => c.Content).ToList();

    private static async Task<IReadOnlyList<UserReportDto>> QueueAsync(TestUser admin, bool includeHandled = false) =>
        (await (await admin.Client.GetAsync($"/api/admin/user-reports?includeHandled={includeHandled}"))
            .ReadAsync<List<UserReportDto>>())!;

    private static Task<HttpResponseMessage> Report(TestUser reporter, Guid userId, string reason = "HARASSMENT") =>
        reporter.Client.PostAsJsonAsync($"/api/users/{userId}/reports", new { reason, description = "Insulti nei commenti." });

    [Fact]
    public async Task A_reported_person_is_suspended_by_BoulderTime_and_disappears()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var rude = await _f.UserAsync();
        var reporter = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        (await rude.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Commento offensivo" }))
            .EnsureSuccessStatusCode();

        (await Report(reporter, rude.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        // Reporting again while the first report is open fills nothing.
        (await Report(reporter, rude.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Only BoulderTime sees the queue — not the reporter, not the gym.
        (await reporter.Client.GetAsync("/api/admin/user-reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await world.Staff.Client.GetAsync("/api/admin/user-reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var queue = await QueueAsync(admin);
        queue.Should().ContainSingle();
        queue[0].Person.UserId.Should().Be(rude.Id);
        queue[0].Reason.Should().Be(UserReportReason.Harassment);
        queue[0].PersonIsSuspended.Should().BeFalse(); // a report alone changes nothing

        var handled = (await (await admin.Client.PostAsJsonAsync($"/api/admin/user-reports/{queue[0].Id}/handle",
            new { suspend = true, note = "Insulti ripetuti." })).ReadAsync<UserReportDto>())!;
        handled.Status.Should().Be(UserReportStatus.Suspended);
        handled.PersonIsSuspended.Should().BeTrue();
        (await QueueAsync(admin)).Should().BeEmpty();

        // Stopped at the door, with a code the app recognises...
        var refused = await rude.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Ancora io" });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("account_suspended");
        (await rude.Client.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // ...but they can still learn what happened, and still delete their account.
        var me = (await (await rude.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        me.IsSuspended.Should().BeTrue();
        (await rude.Client.GetAsync("/api/users/me/deletion")).StatusCode.Should().Be(HttpStatusCode.OK);

        // What they wrote is gone for everyone, signed in or not; their profile no longer exists.
        (await CommentsSeenBy(reporter.Client, boulder.Id)).Should().NotContain("Commento offensivo");
        (await CommentsSeenBy(_f.CreateClient(), boulder.Id)).Should().NotContain("Commento offensivo");
        (await reporter.Client.GetAsync($"/api/users/{rude.Id}/profile")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The gym's staff still see it, so moderation is never blind.
        (await CommentsSeenBy(world.Staff.Client, boulder.Id)).Should().Contain("Commento offensivo");
    }

    [Fact]
    public async Task Lifting_a_suspension_gives_back_the_account_and_what_it_wrote()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var person = await _f.UserAsync();
        var reader = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        await person.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Bel blocco" });

        (await admin.Client.PutAsJsonAsync($"/api/admin/users/{person.Id}/suspension", new { reason = "Per errore" }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CommentsSeenBy(reader.Client, boulder.Id)).Should().NotContain("Bel blocco");

        (await admin.Client.DeleteAsync($"/api/admin/users/{person.Id}/suspension")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CommentsSeenBy(reader.Client, boulder.Id)).Should().Contain("Bel blocco"); // hidden, never deleted
        (await person.Client.PostAsJsonAsync($"/api/boulders/{boulder.Id}/comments", new { content = "Sono tornato" }))
            .EnsureSuccessStatusCode();
        var me = (await (await person.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        me.IsSuspended.Should().BeFalse();
    }

    [Fact]
    public async Task A_dismissed_report_leaves_the_person_alone()
    {
        var person = await _f.UserAsync();
        var reporter = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        await Report(reporter, person.Id, "SPAM");
        var report = (await QueueAsync(admin)).Single();

        var handled = (await (await admin.Client.PostAsJsonAsync($"/api/admin/user-reports/{report.Id}/handle",
            new { suspend = false })).ReadAsync<UserReportDto>())!;

        handled.Status.Should().Be(UserReportStatus.Dismissed);
        handled.PersonIsSuspended.Should().BeFalse();
        (await person.Client.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await QueueAsync(admin, includeHandled: true)).Should().ContainSingle();
        // Handled once is handled.
        (await admin.Client.PostAsJsonAsync($"/api/admin/user-reports/{report.Id}/handle", new { suspend = true }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Suspending_settles_every_open_report_about_the_same_person()
    {
        var person = await _f.UserAsync();
        var first = await _f.UserAsync();
        var second = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        await Report(first, person.Id);
        await Report(second, person.Id, "IMPERSONATION");
        var queue = await QueueAsync(admin);
        queue.Should().HaveCount(2);
        queue.Should().OnlyContain(r => r.OpenReportsAgainstPerson == 2);

        await admin.Client.PostAsJsonAsync($"/api/admin/user-reports/{queue[0].Id}/handle", new { suspend = true });

        (await QueueAsync(admin)).Should().BeEmpty();
        (await QueueAsync(admin, includeHandled: true)).Should().OnlyContain(r => r.Status == UserReportStatus.Suspended);
    }

    [Fact]
    public async Task Nobody_reports_themselves_and_only_BoulderTime_suspends_and_never_its_own()
    {
        var person = await _f.UserAsync();
        var other = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        var otherAdmin = await _f.UserAsync(platformAdmin: true);

        (await Report(person, person.Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await person.Client.PostAsJsonAsync($"/api/users/{other.Id}/reports", new { description = "senza motivo" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Report(person, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await person.Client.PutAsJsonAsync($"/api/admin/users/{other.Id}/suspension", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PutAsJsonAsync($"/api/admin/users/{admin.Id}/suspension", new { }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Client.PutAsJsonAsync($"/api/admin/users/{otherAdmin.Id}/suspension", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_suspended_climber_leaves_the_leaderboard()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        (await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/attempt", new { attempts = 1, completed = true }))
            .EnsureSuccessStatusCode();

        async Task<LeaderboardDto> Board() =>
            (await (await world.Staff.Client.GetAsync($"/api/gyms/{world.Gym.Id}/leaderboard?period=ALL")).ReadAsync<LeaderboardDto>())!;

        (await Board()).Entries.Should().Contain(e => e.Climber.UserId == climber.Id);
        await admin.Client.PutAsJsonAsync($"/api/admin/users/{climber.Id}/suspension", new { });
        (await Board()).Entries.Should().NotContain(e => e.Climber.UserId == climber.Id);
    }
}
