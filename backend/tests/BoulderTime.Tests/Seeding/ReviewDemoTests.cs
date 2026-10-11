using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Infrastructure.Seeding;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Seeding;

/// <summary>The hidden demo gyms for the stores' reviewers (ADR-051).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ReviewDemoTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task An_admin_creates_three_hidden_gyms_with_the_reviewer_as_admin_and_pressing_again_changes_nothing()
    {
        var admin = await _f.UserAsync(platformAdmin: true);
        var reviewer = await _f.UserAsync(email: "review@example.com");

        var result = (await (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = "Review@Example.com" })).ReadAsync<ReviewDemoResult>())!;
        result.GymsCreated.Should().Be(DemoSeeder.ReviewGymCount);
        result.AllHidden.Should().BeTrue();
        result.StaffEmail.Should().Be("review@example.com");

        var gyms = await _f.Db(db => db.Gyms.ToListAsync());
        gyms.Should().HaveCount(3);
        gyms.Should().OnlyContain(g => g.Status == GymStatus.Draft && g.Name.EndsWith("(demo)"));
        var staff = await _f.Db(db => db.GymStaff.ToListAsync());
        staff.Where(s => s.UserId == admin.Id).Should().OnlyContain(s => s.Role == GymRole.Owner);
        staff.Where(s => s.UserId == reviewer.Id).Should().OnlyContain(s => s.Role == GymRole.Admin);
        staff.Count(s => s.UserId == reviewer.Id).Should().Be(3);
        (await _f.Db(db => db.Comments.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.GymAnnouncements.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.Reports.CountAsync())).Should().Be(1);

        // Nobody else sees them: not in search, not on the map.
        var search = (await (await _f.CreateClient().GetAsync("/api/gyms?q=")).ReadAsync<PagedResult<GymSummaryDto>>())!;
        search.Total.Should().Be(0);

        var before = await Counts();
        (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = "review@example.com" })).EnsureSuccessStatusCode();
        (await Counts()).Should().Be(before);
    }

    [Fact]
    public async Task Only_platform_admins_can_and_the_reviewer_must_have_signed_up_first()
    {
        var climber = await _f.UserAsync();
        (await climber.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = (string?)null })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await _f.UserAsync(platformAdmin: true);
        (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = "nobody@example.com" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _f.Db(db => db.Gyms.CountAsync())).Should().Be(0);
    }

    private async Task<string> Counts() => await _f.Db(async db =>
        $"{await db.Gyms.CountAsync()}/{await db.GymStaff.CountAsync()}/{await db.Boulders.CountAsync()}/{await db.Users.CountAsync()}/" +
        $"{await db.Comments.CountAsync()}/{await db.BoulderAttempts.CountAsync()}/{await db.GymAnnouncements.CountAsync()}/{await db.Reports.CountAsync()}");
}
