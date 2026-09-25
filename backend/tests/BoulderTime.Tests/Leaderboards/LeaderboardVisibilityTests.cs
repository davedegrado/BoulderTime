using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Leaderboards;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Leaderboards;

[Collection(DatabaseCollection.Name)]
public sealed class LeaderboardVisibilityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    /// <summary>A gym with one staff member and one climber who has a send on the board.</summary>
    private async Task<(ClimbingWorld World, TestUser Staff, TestUser Climber)> BoardAsync()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var staff = await _f.UserAsync();
        var climber = await _f.UserAsync();
        await _f.StaffAsync(world.Gym, staff, GymRole.Staff);
        (await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/attempt", new { attempts = 2, completed = true }))
            .EnsureSuccessStatusCode();
        return (world, staff, climber);
    }

    private async Task<LeaderboardDto> BoardFor(TestUser viewer, ClimbingWorld world) =>
        (await (await viewer.Client.GetAsync($"/api/gyms/{world.Gym.Id}/leaderboard?period=ALL")).ReadAsync<LeaderboardDto>())!;

    [Fact]
    public async Task A_climber_can_hide_themselves_and_disappears_instead_of_showing_zero_points()
    {
        var (world, staff, climber) = await BoardAsync();
        (await BoardFor(staff, world)).Entries.Should().Contain(e => e.Climber.UserId == climber.Id);

        (await climber.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = true })).EnsureSuccessStatusCode();

        // Gone for everyone, including themselves — a zero-point row would still name them.
        (await BoardFor(staff, world)).Entries.Should().NotContain(e => e.Climber.UserId == climber.Id);
        (await BoardFor(climber, world)).Entries.Should().NotContain(e => e.Climber.UserId == climber.Id);
        var me = (await (await climber.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        me.LeaderboardOptOut.Should().BeTrue();
        me.LeaderboardExcluded.Should().BeFalse(); // their own choice is not an exclusion

        (await climber.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = false })).EnsureSuccessStatusCode();
        (await BoardFor(staff, world)).Entries.Should().Contain(e => e.Climber.UserId == climber.Id);
    }

    [Fact]
    public async Task Gyms_report_but_only_BoulderTime_excludes()
    {
        var (world, staff, climber) = await BoardAsync();
        var admin = await _f.UserAsync(platformAdmin: true);

        var report = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports",
            new { userId = climber.Id, reason = "Trenta blocchi in dieci minuti." })).ReadAsync<LeaderboardReportDto>())!;
        report.Status.Should().Be(LeaderboardReportStatus.Pending);
        report.ClimberIsExcluded.Should().BeFalse(); // reporting alone changes nothing

        // The climber is still on the board while the report waits.
        (await BoardFor(staff, world)).Entries.Should().Contain(e => e.Climber.UserId == climber.Id);

        // Staff cannot see or handle the queue.
        (await staff.Client.GetAsync("/api/admin/leaderboard-reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.Client.PostAsJsonAsync($"/api/admin/leaderboard-reports/{report.Id}/handle", new { exclude = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await climber.Client.GetAsync("/api/admin/leaderboard-exclusions")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var handled = (await (await admin.Client.PostAsJsonAsync($"/api/admin/leaderboard-reports/{report.Id}/handle",
            new { exclude = true, note = "Verificato con la palestra." })).ReadAsync<LeaderboardReportDto>())!;
        handled.Status.Should().Be(LeaderboardReportStatus.Excluded);
        (await BoardFor(staff, world)).Entries.Should().NotContain(e => e.Climber.UserId == climber.Id);
    }

    [Fact]
    public async Task An_excluded_climber_is_told_so_on_their_own_profile_and_nobody_else_is()
    {
        var (world, staff, climber) = await BoardAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        var report = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports",
            new { userId = climber.Id, reason = "Risultati implausibili." })).ReadAsync<LeaderboardReportDto>())!;
        await admin.Client.PostAsJsonAsync($"/api/admin/leaderboard-reports/{report.Id}/handle", new { exclude = true });

        var theirs = (await (await climber.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        theirs.LeaderboardExcluded.Should().BeTrue();
        theirs.LeaderboardOptOut.Should().BeFalse(); // it was not their choice

        // The public profile says nothing about it.
        var publicProfile = await (await staff.Client.GetAsync($"/api/users/{climber.Id}/profile")).Content.ReadAsStringAsync();
        publicProfile.Should().NotContain("Excluded");
        publicProfile.ToLowerInvariant().Should().NotContain("leaderboardexcluded");
    }

    [Fact]
    public async Task An_exclusion_can_be_undone_without_touching_the_climbers_own_choice()
    {
        var (world, staff, climber) = await BoardAsync();
        var admin = await _f.UserAsync(platformAdmin: true);
        await climber.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = true });
        var report = (await (await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports",
            new { userId = climber.Id, reason = "Sospetto." })).ReadAsync<LeaderboardReportDto>())!;
        await admin.Client.PostAsJsonAsync($"/api/admin/leaderboard-reports/{report.Id}/handle", new { exclude = true });

        (await admin.Client.DeleteAsync($"/api/admin/leaderboard-exclusions/{climber.Id}")).EnsureSuccessStatusCode();

        var me = (await (await climber.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        me.LeaderboardExcluded.Should().BeFalse();
        me.LeaderboardOptOut.Should().BeTrue(); // untouched
        (await BoardFor(staff, world)).Entries.Should().NotContain(e => e.Climber.UserId == climber.Id);
    }

    [Fact]
    public async Task Reporting_is_limited_to_that_gyms_staff_and_never_oneself()
    {
        var (world, staff, climber) = await BoardAsync();
        var otherGym = await _f.GymAsync(GymStatus.Active, "Volume Lab");
        var otherStaff = await _f.UserAsync();
        await _f.StaffAsync(otherGym, otherStaff, GymRole.Staff);

        (await climber.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports", new { userId = staff.Id, reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await otherStaff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports", new { userId = climber.Id, reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports", new { userId = staff.Id, reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports", new { userId = climber.Id, reason = "" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Reporting the same climber twice keeps one open report.
        await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports", new { userId = climber.Id, reason = "Prima volta." });
        await staff.Client.PostAsJsonAsync($"/api/gyms/{world.Gym.Id}/leaderboard-reports", new { userId = climber.Id, reason = "Ancora." });
        (await _f.Db(db => db.LeaderboardReports.CountAsync(r => r.ReportedUserId == climber.Id))).Should().Be(1);
    }
}
