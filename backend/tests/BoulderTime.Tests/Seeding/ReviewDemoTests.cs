using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Follows;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
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
    public async Task An_admin_creates_three_unlisted_gyms_the_reviewer_runs_and_follows_and_pressing_again_changes_nothing()
    {
        var admin = await _f.UserAsync(platformAdmin: true);
        var reviewer = await _f.UserAsync(email: "review@example.com");

        var result = (await (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = "Review@Example.com" })).ReadAsync<ReviewDemoResult>())!;
        result.GymsCreated.Should().Be(DemoSeeder.ReviewGymCount);
        result.StaffEmail.Should().Be("review@example.com");

        var gyms = await _f.Db(db => db.Gyms.ToListAsync());
        gyms.Should().HaveCount(3);
        gyms.Should().OnlyContain(g => g.Status == GymStatus.Active && g.IsUnlisted && g.Name.EndsWith("(demo)"));
        var staff = await _f.Db(db => db.GymStaff.ToListAsync());
        staff.Where(s => s.UserId == admin.Id).Should().OnlyContain(s => s.Role == GymRole.Owner);
        staff.Where(s => s.UserId == reviewer.Id).Should().OnlyContain(s => s.Role == GymRole.Admin);
        staff.Count(s => s.UserId == reviewer.Id).Should().Be(3);
        (await _f.Db(db => db.Comments.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.GymAnnouncements.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.Reports.CountAsync())).Should().Be(1);

        // Nobody else finds them: they are live, yet search offers nothing.
        var search = (await (await _f.CreateClient().GetAsync("/api/gyms?q=")).ReadAsync<PagedResult<GymSummaryDto>>())!;
        search.Total.Should().Be(0);

        // For the reviewer they work like any gym: followed, so on Home.
        var follows = (await (await reviewer.Client.GetAsync("/api/users/me/follows")).ReadAsync<MyFollowsDto>())!;
        follows.Gyms.Should().HaveCount(3);

        var before = await Counts();
        (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = "review@example.com" })).EnsureSuccessStatusCode();
        (await Counts()).Should().Be(before);
    }

    [Fact]
    public async Task Pressing_again_swaps_the_old_drawn_walls_for_photos_and_leaves_the_rest()
    {
        var admin = await _f.UserAsync(platformAdmin: true);
        (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = (string?)null })).EnsureSuccessStatusCode();
        var all = await _f.Db(db => db.Boulders.CountAsync());
        all.Should().BeGreaterThan(5);
        (await _f.Db(db => db.Boulders.ToListAsync())).Should().OnlyContain(b => b.PhotoPath.Contains("/demo-photo-"));

        // Five boulders as the version with drawn walls made them; one more as staff uploaded it.
        var drawn = await _f.Db(async db =>
        {
            var bs = await db.Boulders.OrderBy(b => b.Id).Take(6).ToListAsync();
            foreach (var b in bs.Take(5))
                b.Update(b.SectorId, $"{BoulderService.PhotoPrefix(b.GymId)}demo-wall-1-blue.jpg", null, HoldColor.Mixed, b.SetterUserId, b.Intensity);
            bs[5].Update(bs[5].SectorId, $"{BoulderService.PhotoPrefix(bs[5].GymId)}staff-upload.jpg", null, HoldColor.Mixed, bs[5].SetterUserId, bs[5].Intensity);
            await db.SaveChangesAsync();
            return bs.Select(b => b.Id).ToList();
        });

        var result = (await (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = (string?)null })).ReadAsync<ReviewDemoResult>())!;
        result.PhotosReplaced.Should().Be(5);
        var after = await _f.Db(db => db.Boulders.Where(b => drawn.Contains(b.Id)).ToListAsync());
        after.Where(b => b.Id != drawn[5]).Should().OnlyContain(b => b.PhotoPath.Contains("/demo-photo-") && b.HoldColor != HoldColor.Mixed && b.ThumbnailPath == null);
        after.Single(b => b.Id == drawn[5]).PhotoPath.Should().EndWith("staff-upload.jpg", "a photo staff chose is theirs");
        (await _f.Db(db => db.Boulders.CountAsync())).Should().Be(all);

        var again = (await (await admin.Client.PostAsJsonAsync("/api/admin/review-demo", new { staffEmail = (string?)null })).ReadAsync<ReviewDemoResult>())!;
        again.PhotosReplaced.Should().Be(0);
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
