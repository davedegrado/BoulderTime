using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Community;
using BoulderTime.Infrastructure.Seeding;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Seeding;

/// <summary>The demo data is what a gym sees first, so it has to be Italian, complete and safe to re-run.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class DemoSeedTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task SeedAsync()
    {
        await using var scope = _f.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(null);
    }

    [Fact]
    public async Task Seeding_fills_the_demo_gyms_with_italian_content_and_climbing_history()
    {
        await SeedAsync();

        var gyms = await _f.Db(db => db.Gyms.CountAsync());
        gyms.Should().Be(4);
        (await _f.Db(db => db.Sectors.Select(s => s.Name).ToListAsync())).Should().Contain(["Grotta", "Placca", "Sala Grande"]);
        (await _f.Db(db => db.Boulders.CountAsync(b => b.Status == BoulderStatus.Active))).Should().BeGreaterThan(40);

        // Demo climbers: profiles without a Supabase account, marked by their email domain.
        var climbers = await _f.Db(db => db.Users.CountAsync(u => u.Email.EndsWith(DemoSeeder.DemoEmailDomain)));
        climbers.Should().Be(6);

        (await _f.Db(db => db.BoulderAttempts.CountAsync(a => a.Completed))).Should().BeGreaterThan(20);
        (await _f.Db(db => db.BoulderAttempts.CountAsync(a => !a.Completed && a.Attempts > 0))).Should().BeGreaterThan(0);
        (await _f.Db(db => db.BoulderRatings.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.GradeSuggestions.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.Comments.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.GymFollows.CountAsync())).Should().BeGreaterThan(0);
        (await _f.Db(db => db.Reports.CountAsync(r => r.Status == ReportStatus.Pending))).Should().Be(1);
    }

    [Fact]
    public async Task Each_climber_has_at_most_one_row_per_boulder()
    {
        await SeedAsync();

        // The database enforces this, so a duplicate would make seeding fail outright — assert it explicitly
        // because the boulder-picking cycle can otherwise land on the same boulder twice for one climber.
        var pairs = await _f.Db(db => db.BoulderAttempts.Select(a => new { a.UserId, a.BoulderId }).ToListAsync());
        pairs.Distinct().Count().Should().Be(pairs.Count);

        var ratings = await _f.Db(db => db.BoulderRatings.Select(r => new { r.UserId, r.BoulderId }).ToListAsync());
        ratings.Distinct().Count().Should().Be(ratings.Count);
    }

    [Fact]
    public async Task Sends_are_spread_over_the_past_weeks_so_periods_and_charts_look_real()
    {
        await SeedAsync();
        var now = DateTimeOffset.UtcNow;

        var completedAt = await _f.Db(db => db.BoulderAttempts.Where(a => a.Completed).Select(a => a.CompletedAt!.Value).ToListAsync());
        completedAt.Should().OnlyContain(d => d <= now && d > now.AddDays(-70));
        completedAt.Select(d => d.Date).Distinct().Count().Should().BeGreaterThan(5);
    }

    [Fact]
    public async Task Seeding_twice_changes_nothing()
    {
        await SeedAsync();
        var before = await Counts();

        await SeedAsync();

        (await Counts()).Should().Be(before);
    }

    private async Task<string> Counts() => await _f.Db(async db =>
        $"{await db.Gyms.CountAsync()}/{await db.Sectors.CountAsync()}/{await db.Boulders.CountAsync()}/" +
        $"{await db.Users.CountAsync()}/{await db.Comments.CountAsync()}/{await db.BoulderAttempts.CountAsync()}/" +
        $"{await db.GymAnnouncements.CountAsync()}/{await db.Reports.CountAsync()}");
}
