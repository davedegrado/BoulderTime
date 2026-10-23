using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Users;

/// <summary>
/// A private profile and the leaderboards are two separate choices (ADR-061). A private profile still opens, with name
/// and photo only, and the server sends nothing more to anyone but its owner — whatever the app. The answers keep the
/// shape older apps read, and every account made before keeps a public profile.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProfilePrivacyTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Send(TestUser u, Guid boulderId) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 3, completed = true })).EnsureSuccessStatusCode();

    private static async Task SetPrivate(TestUser u, bool isPrivate) =>
        (await u.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = isPrivate })).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task SetInLeaderboards(TestUser u, bool shown) =>
        (await u.Client.PutAsJsonAsync("/api/users/me/leaderboard-visibility", new { hidden = !shown })).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<ProfileDto> Profile(HttpClient client, Guid userId) =>
        (await (await client.GetAsync($"/api/users/{userId}/profile")).ReadAsync<ProfileDto>())!;

    [Fact]
    public async Task Accounts_are_public_until_their_owner_says_otherwise_and_the_profile_keeps_every_field_older_apps_read()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var anna = await _f.UserAsync();
        await Send(anna, b.Id);

        var me = (await (await anna.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        me.ProfilePrivate.Should().BeFalse();

        // The JSON an older app reads, field by field, unchanged.
        var raw = await (await _f.CreateClient().GetAsync($"/api/users/{anna.Id}/profile")).Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(raw).RootElement;
        foreach (var field in new[] { "id", "displayName", "avatarUrl", "memberSince", "isMe", "stats", "highestGrades", "weekly", "followedGyms",
                     "recentCompletions", "staffDistinctions", "isBlocked", "favoriteSector" })
            json.TryGetProperty(field, out _).Should().BeTrue($"older apps read {field}");
        json.GetProperty("stats").GetProperty("completed").GetInt32().Should().Be(1);
        json.GetProperty("isPrivate").GetBoolean().Should().BeFalse();
        json.GetProperty("contentHidden").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task A_private_profile_shows_only_name_and_photo_to_others_everywhere_and_everything_to_its_owner()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var anna = await _f.UserAsync();
        await Send(anna, b.Id);
        await SetPrivate(anna, true);

        foreach (var viewer in new[] { _f.CreateClient(), (await _f.UserAsync()).Client, w.Staff.Client })
        {
            var p = await Profile(viewer, anna.Id);
            p.DisplayName.Should().NotBeEmpty();
            p.IsPrivate.Should().BeTrue();
            p.ContentHidden.Should().BeTrue();
            p.Stats.Should().Be(new ClimbingStatsDto(0, 0, 0, 0));
            p.HighestGrades.Should().BeEmpty();
            p.Weekly.Should().BeEmpty();
            p.FollowedGyms.Should().BeEmpty();
            p.RecentCompletions.Should().BeEmpty();
            p.StaffDistinctions.Should().BeEmpty();
            p.FavoriteSector.Should().BeNull();
            // The other door to the same data is shut too.
            var history = (await (await viewer.GetAsync($"/api/users/{anna.Id}/history?filter=COMPLETED")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!;
            history.Total.Should().Be(0);
            history.Items.Should().BeEmpty();
        }

        var own = await Profile(anna.Client, anna.Id);
        own.ContentHidden.Should().BeFalse();
        own.IsPrivate.Should().BeTrue();
        own.Stats.Completed.Should().Be(1);
        (await (await anna.Client.GetAsync($"/api/users/{anna.Id}/history")).ReadAsync<PagedResult<ClimbHistoryItemDto>>())!.Total.Should().Be(1);

        // Public again: everything is back, nothing was lost.
        await SetPrivate(anna, false);
        (await Profile(_f.CreateClient(), anna.Id)).Stats.Completed.Should().Be(1);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public async Task Leaderboards_follow_their_own_setting_whatever_the_privacy(bool isPrivate, bool inLeaderboards, bool listed)
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("7A");
        var anna = await _f.UserAsync();
        await Send(anna, b.Id);
        await SetPrivate(anna, isPrivate);
        await SetInLeaderboards(anna, inLeaderboards);

        var board = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/leaderboard?metric=POINTS&period=ALL")).ReadAsync<LeaderboardDto>())!;
        board.Entries.Any(e => e.Climber.UserId == anna.Id).Should().Be(listed);
        var people = (await (await (await _f.UserAsync()).Client.GetAsync($"/api/boulders/{b.Id}/completions")).ReadAsync<BoulderCompletionsDto>())!;
        people.People.Items.Any(p => p.Person.UserId == anna.Id).Should().Be(listed);
        people.Total.Should().Be(1); // counted either way, as always

        // Her results are untouched by either switch.
        (await Profile(anna.Client, anna.Id)).Stats.Completed.Should().Be(1);
    }

    [Fact]
    public async Task Only_the_owner_changes_it_and_the_request_must_say_what()
    {
        (await _f.CreateClient().PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var anna = await _f.UserAsync();
        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SetPrivate(anna, true);
        (await (await anna.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!.ProfilePrivate.Should().BeTrue();
    }
}
