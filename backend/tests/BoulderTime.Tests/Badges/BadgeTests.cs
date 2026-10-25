using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Admin;
using BoulderTime.Application.Badges;
using BoulderTime.Domain.Badges;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Badges;

/// <summary>
/// Badges (ADR-069): earned from what people record, once, never taken back; shown with the profile's content; the
/// leaderboard titles settled when a period closes, from the gym's launch; the beta testers' and the founder's.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BadgeTests : IAsyncLifetime
{
    private sealed class FakePush : IPushQueue
    {
        public ConcurrentQueue<(Guid UserId, PushMessage Message)> Sent { get; } = new();
        public void Enqueue(Guid userId, PushMessage message) => Sent.Enqueue((userId, message));
    }

    private sealed record Switches(bool PushEnabled) : IBadgeSettings;

    private readonly FakePush _push = new();
    private readonly ApiFactory _f;

    public BadgeTests(PostgresFixture postgres) => _f = new ApiFactory(postgres)
    {
        OverrideServices = s =>
        {
            s.AddSingleton<IPushQueue>(_push);
            s.AddSingleton<IBadgeSettings>(new Switches(true));
        },
    };

    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Record(TestUser u, Guid boulderId, int attempts, bool completed) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts, completed })).EnsureSuccessStatusCode();

    private static async Task<UserBadgesDto> Badges(TestUser viewer, Guid userId) =>
        (await (await viewer.Client.GetAsync($"/api/users/{userId}/badges")).ReadAsync<UserBadgesDto>())!;

    /// <summary>Badges are awarded in the background: wait for them, for a while.</summary>
    private static async Task<UserBadgesDto> BadgesOnceAsync(TestUser viewer, Guid userId, Func<UserBadgesDto, bool> ready)
    {
        for (var i = 0; i < 100; i++)
        {
            var b = await Badges(viewer, userId);
            if (ready(b)) return b;
            await Task.Delay(100);
        }
        return await Badges(viewer, userId);
    }

    private static bool Has(UserBadgesDto b, string id) => b.Badges.Any(x => x.BadgeId == id);

    private Task<T> InScope<T>(Func<IServiceProvider, Task<T>> work) => Run(work);

    private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _f.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    [Fact]
    public async Task Recording_earns_the_badges_it_reaches_once_and_undoing_it_takes_none_back()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync(language: "it");
        var a = await w.BoulderAsync("6A");
        var b = await w.BoulderAsync("6B");

        await Record(anna, a.Id, 6, true);
        await Record(anna, b.Id, 4, false);
        var got = await BadgesOnceAsync(anna, anna.Id, x => Has(x, "EXPERIENCE_ATTEMPTS_10"));
        got.Badges.Select(x => x.BadgeId).Should().BeEquivalentTo("PROGRESSION_COMPLETIONS_1", "EXPLORATION_GYMS_1", "EXPERIENCE_ATTEMPTS_10");
        var first = got.Badges.Single(x => x.BadgeId == "PROGRESSION_COMPLETIONS_1");
        first.Category.Should().Be(BadgeCategory.Progression);
        first.Art.Should().Be("progression-1");
        first.Name.Should().Be("Primo blocco", "names come in the language of the request");

        // Undone: the badges stay, and doing it again earns nothing twice.
        await Record(anna, a.Id, 0, false);
        await Record(anna, a.Id, 6, true);
        await Task.Delay(500);
        (await Badges(anna, anna.Id)).Badges.Should().HaveCount(3);
        (await _f.Db(db => db.BadgeAwards.CountAsync(x => x.UserId == anna.Id))).Should().Be(3);

        // One phone notification per badge, in the person's language, for the apps that show badges, opening the profile.
        _push.Sent.Should().HaveCount(3);
        _push.Sent.Should().AllSatisfy(p =>
        {
            p.UserId.Should().Be(anna.Id);
            p.Message.MinimumAppVersion.Should().Be("1.1.0");
            p.Message.Url.Should().Be($"/users/{anna.Id}");
            p.Message.Title.Should().StartWith("Nuovo badge: ");
        });

        // One day of climbing so far, in Italian time.
        (await _f.Db(db => db.ActivityDays.CountAsync(x => x.UserId == anna.Id))).Should().Be(1);
    }

    [Fact]
    public async Task A_project_is_closed_only_by_completing_a_boulder_marked_as_one_before()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        var plain = await w.BoulderAsync("6A");
        var project = await w.BoulderAsync("7A");

        await Record(anna, plain.Id, 1, true);
        await BadgesOnceAsync(anna, anna.Id, x => Has(x, "PROGRESSION_COMPLETIONS_1"));
        (await anna.Client.PutAsync($"/api/boulders/{plain.Id}/project", null)).StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        await Task.Delay(300);
        Has(await Badges(anna, anna.Id), "EXPERIENCE_PROJECT_CLOSED").Should().BeFalse("it was completed before it became a project");

        (await anna.Client.PutAsync($"/api/boulders/{project.Id}/project", null)).EnsureSuccessStatusCode();
        await Record(anna, project.Id, 3, true);
        Has(await BadgesOnceAsync(anna, anna.Id, x => Has(x, "EXPERIENCE_PROJECT_CLOSED")), "EXPERIENCE_PROJECT_CLOSED").Should().BeTrue();
    }

    [Fact]
    public async Task Up_to_three_earned_badges_go_in_the_foreground_in_order_and_only_their_owner_chooses()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        await Record(anna, (await w.BoulderAsync("6A")).Id, 10, true);
        var mine = (await BadgesOnceAsync(anna, anna.Id, x => x.Badges.Count == 3)).Badges.Select(x => x.Id).ToList();
        var lucasBoulder = await w.BoulderAsync("6B");
        await Record(luca, lucasBoulder.Id, 1, true);
        var his = (await BadgesOnceAsync(luca, luca.Id, x => x.Badges.Count == 2)).Badges[0].Id;

        Task<HttpResponseMessage> Feature(TestUser who, params Guid[] ids) => who.Client.PutAsJsonAsync("/api/users/me/badges/featured", new { ids });

        var set = await (await Feature(anna, mine[2], mine[0])).ReadAsync<UserBadgesDto>();
        set!.Featured.Should().Equal(mine[2], mine[0]);
        (await Badges(luca, anna.Id)).Featured.Should().Equal(mine[2], mine[0]);

        (await Feature(anna, mine[0], mine[0])).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Feature(anna, his)).StatusCode.Should().Be(HttpStatusCode.BadRequest, "nobody shows a badge they don't have");
        (await Feature(anna, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Feature(anna, mine[0], mine[1], mine[2], his)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A refused choice changes nothing.
        (await Badges(anna, anna.Id)).Featured.Should().Equal(mine[2], mine[0]);

        (await Feature(anna)).EnsureSuccessStatusCode();
        (await Badges(anna, anna.Id)).Featured.Should().BeEmpty();
    }

    [Fact]
    public async Task A_private_profile_shows_its_badges_to_its_owner_and_friends_only()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        var friend = await _f.UserAsync();
        var stranger = await _f.UserAsync();
        await Record(anna, (await w.BoulderAsync("6A")).Id, 1, true);
        await BadgesOnceAsync(anna, anna.Id, x => x.Badges.Count == 2);
        (await friend.Client.PostAsync($"/api/users/{anna.Id}/friend-request", null)).EnsureSuccessStatusCode();
        (await anna.Client.PostAsync($"/api/users/{friend.Id}/friend-request/accept", null)).EnsureSuccessStatusCode();
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).EnsureSuccessStatusCode();

        (await Badges(stranger, anna.Id)).Badges.Should().BeEmpty();
        (await Badges(friend, anna.Id)).Badges.Should().HaveCount(2);
        (await Badges(anna, anna.Id)).Badges.Should().HaveCount(2);
    }

    [Fact]
    public async Task New_badges_are_told_once_then_marked_seen()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        await Record(anna, (await w.BoulderAsync("6A")).Id, 1, true);
        await BadgesOnceAsync(anna, anna.Id, x => x.Badges.Count == 2);

        (await (await anna.Client.GetAsync("/api/users/me/badges/new")).ReadAsync<List<EarnedBadgeDto>>()).Should().HaveCount(2);
        (await anna.Client.PostAsync("/api/users/me/badges/seen", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await (await anna.Client.GetAsync("/api/users/me/badges/new")).ReadAsync<List<EarnedBadgeDto>>()).Should().BeEmpty();
    }

    [Fact]
    public async Task The_past_is_backfilled_from_the_dates_recorded_dated_silently_and_only_once()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        var boulders = new List<Guid>();
        for (var i = 0; i < 3; i++) boulders.Add((await w.BoulderAsync("6A")).Id);
        // Recorded before badges existed: straight into the database, on three different days.
        var days = new[] { new DateTimeOffset(2026, 9, 1, 18, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 3, 22, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero) };
        await _f.Db(async db =>
        {
            for (var i = 0; i < 3; i++)
            {
                var row = BoulderTime.Domain.Climbing.BoulderAttempt.Start(anna.Id, boulders[i]);
                row.Record(2, true, days[i]);
                row.CreatedAt = days[i];
                row.UpdatedAt = days[i];
                db.BoulderAttempts.Add(row);
            }
            await db.SaveChangesAsync();
            // The audit stamps write "now": put the dates back.
            for (var i = 0; i < 3; i++)
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE bouldertime.boulder_attempts SET created_at = {days[i]}, updated_at = {days[i]} WHERE boulder_id = {boulders[i]}");
            return 0;
        });

        var report = await InScope(sp => sp.GetRequiredService<BadgeBackfill>().RunAsync());
        report.Days.Should().Be(3);
        report.Awards.Should().ContainKeys("PROGRESSION_COMPLETIONS_1", "CONSISTENCY_DAYS_3", "EXPLORATION_GYMS_1");
        var badges = await Badges(anna, anna.Id);
        badges.Badges.Single(x => x.BadgeId == "PROGRESSION_COMPLETIONS_1").EarnedAt.Should().Be(days[0], "dated by the first completion");
        badges.Badges.Single(x => x.BadgeId == "CONSISTENCY_DAYS_3").EarnedAt.UtcDateTime.Date.Should().Be(new DateTime(2026, 9, 9));
        (await _f.Db(db => db.ActivityDays.Where(x => x.UserId == anna.Id).Select(x => x.Day).ToListAsync()))
            .Should().BeEquivalentTo(new[] { new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), new DateOnly(2026, 9, 9) },
                "22:30 UTC on 3 September is 00:30 on the 4th in Italy");
        _push.Sent.Should().BeEmpty("the past is not news");

        var again = await InScope(sp => sp.GetRequiredService<BadgeBackfill>().RunAsync());
        again.Days.Should().Be(0);
        again.Awards.Should().BeEmpty();
    }

    [Fact]
    public async Task Champions_are_settled_once_per_closed_period_from_the_launch_ties_shared_and_titles_kept()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        var sara = await _f.UserAsync();
        var hard = await w.BoulderAsync("7A");
        var easy = await w.BoulderAsync("6A");
        var other = await w.BoulderAsync("7A");
        var launch = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        await _f.Db(async db =>
        {
            (await db.Gyms.FirstAsync(g => g.Id == w.Gym.Id)).Launch(launch);
            void Send(TestUser u, Guid boulder, DateTimeOffset at)
            {
                var row = BoulderTime.Domain.Climbing.BoulderAttempt.Start(u.Id, boulder);
                row.Record(1, true, at);
                db.BoulderAttempts.Add(row);
            }
            // July: Sara's send before the launch doesn't count; Anna and Luca tie after it.
            Send(sara, hard.Id, new DateTimeOffset(2026, 7, 5, 10, 0, 0, TimeSpan.Zero));
            Send(anna, hard.Id, new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero));
            Send(luca, other.Id, new DateTimeOffset(2026, 7, 21, 10, 0, 0, TimeSpan.Zero));
            // August: Anna alone.
            Send(anna, easy.Id, new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero));
            return await db.SaveChangesAsync();
        });

        // July: Anna and Luca; August: Anna, and her Doppietta; the months after: nobody climbed.
        (await InScope(sp => sp.GetRequiredService<CompetitionTitles>().SettleDueAsync())).Should().BeGreaterThanOrEqualTo(4);
        (await InScope(sp => sp.GetRequiredService<CompetitionTitles>().SettleDueAsync())).Should().Be(0, "a period is settled once");

        var annas = await Badges(anna, anna.Id);
        annas.Badges.Where(x => x.BadgeId == "COMPETITION_MONTH_CHAMPION").Select(x => x.Period!.Start.Month).Should().Equal(8, 7);
        annas.Badges.Single(x => x.BadgeId == "COMPETITION_MONTH_CHAMPION" && x.Period!.Start.Month == 7).Gym!.Id.Should().Be(w.Gym.Id);
        Has(annas, "COMPETITION_DOUBLE").Should().BeTrue();
        Has(annas, "COMPETITION_DOMINATOR").Should().BeFalse();
        (await Badges(luca, luca.Id)).Badges.Should().ContainSingle(x => x.BadgeId == "COMPETITION_MONTH_CHAMPION");
        Has(await Badges(sara, sara.Id), "COMPETITION_MONTH_CHAMPION").Should().BeFalse("her send came before the launch");

        // Points change afterwards: the titles stay.
        await _f.Db(async db =>
        {
            (await db.Boulders.FirstAsync(b => b.Id == hard.Id)).SetPointsOverride(1);
            return await db.SaveChangesAsync();
        });
        (await InScope(sp => sp.GetRequiredService<CompetitionTitles>().SettleDueAsync())).Should().Be(0);
        (await Badges(anna, anna.Id)).Badges.Count(x => x.BadgeId == "COMPETITION_MONTH_CHAMPION").Should().Be(2);
    }

    [Fact]
    public async Task Two_years_won_at_the_same_gym_make_its_legend()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var anna = await _f.UserAsync();
        var a = await w.BoulderAsync("6A");
        var b = await w.BoulderAsync("6B");
        await _f.Db(async db =>
        {
            (await db.Gyms.FirstAsync(g => g.Id == w.Gym.Id)).Launch(new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero));
            var r1 = BoulderTime.Domain.Climbing.BoulderAttempt.Start(anna.Id, a.Id); r1.Record(1, true, new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero));
            var r2 = BoulderTime.Domain.Climbing.BoulderAttempt.Start(anna.Id, b.Id); r2.Record(1, true, new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero));
            db.BoulderAttempts.AddRange(r1, r2);
            return await db.SaveChangesAsync();
        });
        await InScope(sp => sp.GetRequiredService<CompetitionTitles>().SettleDueAsync());
        var got = await Badges(anna, anna.Id);
        got.Badges.Count(x => x.BadgeId == "COMPETITION_YEAR_CHAMPION").Should().Be(2);
        Has(got, "COMPETITION_GYM_LEGEND").Should().BeTrue();
    }

    [Fact]
    public async Task Closing_the_beta_test_launches_the_gym_and_makes_its_climbers_beta_testers_for_good()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var onWall = await w.BoulderAsync("6A");
        var removed = await w.BoulderAsync("6B");
        var tried = await _f.UserAsync();
        var sent = await _f.UserAsync();
        var later = await _f.UserAsync();
        await Record(tried, onWall.Id, 2, false);
        await Record(sent, removed.Id, 1, true);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { removed.Id }, notifyFollowers = false })).EnsureSuccessStatusCode();

        var admin = await _f.UserAsync(platformAdmin: true);
        async Task<BetaTestResultDto> Run(string url = "beta-test") =>
            (await (await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{w.Gym.Id}/{url}", new { gymSlug = w.Gym.Slug })).ReadAsync<BetaTestResultDto>())!;

        (await Run()).BetaTesters.Should().Be(2);
        var launched = await _f.Db(db => db.Gyms.Where(g => g.Id == w.Gym.Id).Select(g => g.LaunchedAt).FirstAsync());
        launched.Should().NotBeNull();
        Has(await Badges(tried, tried.Id), "SPECIAL_BETA_TESTER").Should().BeTrue();
        Has(await Badges(sent, sent.Id), "SPECIAL_BETA_TESTER").Should().BeTrue();

        await Record(later, onWall.Id, 1, true);
        (await Run()).BetaTesters.Should().Be(1, "run again, it adds whoever climbed since, and nobody twice");
        (await _f.Db(db => db.Gyms.Where(g => g.Id == w.Gym.Id).Select(g => g.LaunchedAt).FirstAsync())).Should().Be(launched, "the first launch counts");

        (await Run("beta-test/undo")).Changed.Should().Be(1);
        (await _f.Db(db => db.Gyms.Where(g => g.Id == w.Gym.Id).Select(g => g.LaunchedAt).FirstAsync())).Should().BeNull();
        Has(await Badges(tried, tried.Id), "SPECIAL_BETA_TESTER").Should().BeTrue("undoing takes the launch back, not the badges");
    }

    [Fact]
    public async Task Only_badges_given_by_hand_can_be_granted_and_only_once()
    {
        var anna = await _f.UserAsync();
        (await InScope(sp => sp.GetRequiredService<BadgeAwarder>().GrantAsync(anna.Id, "SPECIAL_FOUNDER"))).Should().BeTrue();
        (await InScope(sp => sp.GetRequiredService<BadgeAwarder>().GrantAsync(anna.Id, "SPECIAL_FOUNDER"))).Should().BeFalse();
        var grantEarned = () => InScope(sp => sp.GetRequiredService<BadgeAwarder>().GrantAsync(anna.Id, "PROGRESSION_COMPLETIONS_500"));
        await grantEarned.Should().ThrowAsync<ArgumentException>();
        (await Badges(anna, anna.Id)).Badges.Should().ContainSingle(x => x.BadgeId == "SPECIAL_FOUNDER" && x.Category == BadgeCategory.Special);
    }

    [Fact]
    public async Task Community_badges_are_described_but_nobody_earns_or_sees_them_yet()
    {
        BadgeCatalog.All.Where(b => b.Category == BadgeCategory.Community).Should().NotBeEmpty().And.OnlyContain(b => !b.Enabled);
        var anna = await _f.UserAsync();
        await _f.Db(async db =>
        {
            db.BadgeAwards.Add(BadgeAward.Earn(anna.Id, "COMMUNITY_FIRST_COMMENT", DateTimeOffset.UtcNow));
            db.BadgeAwards.Add(BadgeAward.Earn(anna.Id, "SOMETHING_A_NEWER_SERVER_KNOWS", DateTimeOffset.UtcNow));
            return await db.SaveChangesAsync();
        });
        (await Badges(anna, anna.Id)).Badges.Should().BeEmpty();
    }

    [Fact]
    public void Catalog_ids_and_pictures_are_unique_and_days_are_counted_in_Italian_time()
    {
        BadgeCatalog.All.Select(b => b.Id).Should().OnlyHaveUniqueItems();
        BadgeCatalog.All.Select(b => b.Art).Should().OnlyHaveUniqueItems();
        BadgeCatalog.All.Should().OnlyContain(b => b.Id.Length <= BadgeAward.BadgeIdMaxLength);

        ItalianTime.Day(new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 10, 25), "summer time, +2");
        ItalianTime.Day(new DateTimeOffset(2026, 12, 31, 22, 59, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2026, 12, 31), "winter time, +1");
        ItalianTime.Day(new DateTimeOffset(2026, 12, 31, 23, 0, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2027, 1, 1));
    }
}
