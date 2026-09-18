using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Localization;
using BoulderTime.Application.Notifications;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Localization;

[Collection(DatabaseCollection.Name)]
public sealed class LanguageTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<List<NotificationDto>> InboxAsync(TestUser u) =>
        (await (await u.Client.GetAsync("/api/notifications")).ReadAsync<PagedResult<NotificationDto>>())!.Items.ToList();

    [Theory]
    [InlineData("it", "it")]
    [InlineData("it-IT,it;q=0.9,en;q=0.8", "it")]
    [InlineData("en-GB,en;q=0.9", "en")]
    [InlineData("fr-FR", "it")]   // unsupported: falls back to the platform default
    [InlineData("", "it")]
    [InlineData(null, "it")]
    public void Accept_language_headers_map_to_a_supported_language(string? header, string expected) =>
        Language.Normalize(header).Should().Be(expected);

    [Theory]
    [InlineData("Write something first.", "Scrivi prima qualcosa.")]
    [InlineData("Keep it to 300 characters or fewer.", "Non superare 300 caratteri.")]
    [InlineData("Videos must be under 100 MB.", "I video devono pesare meno di 100 MB.")]
    [InlineData("This needs the ADMIN role at this gym.", "Serve il ruolo ADMIN in questa palestra.")]
    [InlineData("A message nobody translated yet.", "A message nobody translated yet.")]
    public void Messages_are_translated_with_their_runtime_values(string english, string italian)
    {
        Translations.Translate(english, "it").Should().Be(italian);
        Translations.Translate(english, "en").Should().Be(english);
    }

    [Fact]
    public async Task Errors_come_back_in_the_callers_language()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var italian = await _f.UserAsync(language: "it");
        var english = await _f.UserAsync(language: "en");

        var it = await (await italian.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "" })).Content.ReadAsStringAsync();
        it.Should().Contain("Scrivi prima qualcosa.");
        it.Should().Contain("Richiesta non valida");

        var en = await (await english.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "" })).Content.ReadAsStringAsync();
        en.Should().Contain("Write something first.");
    }

    [Fact]
    public async Task Each_person_is_notified_in_their_own_language()
    {
        var w = await ClimbingWorld.CreateAsync(_f); // staff reads English
        var italian = await _f.UserAsync(language: "it");
        var english = await _f.UserAsync(language: "en");
        var boulders = new List<Guid>();
        for (var i = 0; i < 3; i++) boulders.Add((await w.BoulderAsync()).Id);
        foreach (var user in new[] { italian, english })
            await user.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });

        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = boulders, notifyFollowers = true });

        (await InboxAsync(italian)).Single().Title.Should().Be("Il settore Cave è stato ritracciato");
        (await InboxAsync(italian)).Single().Body.Should().Contain("3 blocchi");
        (await InboxAsync(english)).Single().Title.Should().Be("Sector Cave has been retraced");
    }

    [Fact]
    public async Task Collapsed_notifications_stay_in_the_readers_language()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var italian = await _f.UserAsync(language: "it");
        await italian.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });

        await w.BoulderAsync("6A");
        await w.BoulderAsync("6B");

        var inbox = await InboxAsync(italian);
        inbox.Should().ContainSingle();
        inbox[0].Title.Should().Be("2 nuovi blocchi in Cave");
        inbox[0].Count.Should().Be(2);
    }

    [Fact]
    public async Task People_choose_their_language_in_their_profile()
    {
        var me = await _f.UserAsync(language: "it");

        var initial = (await (await me.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>())!;
        initial.Language.Should().Be("it");

        var switched = (await (await me.Client.PatchAsJsonAsync("/api/users/me", new { displayName = initial.DisplayName, language = "en" })).ReadAsync<CurrentUserDto>())!;
        switched.Language.Should().Be("en");
        (await me.Client.PatchAsJsonAsync("/api/users/me", new { displayName = initial.DisplayName, language = "de" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The new language applies to notifications even when the app still asks in Italian.
        var w = await ClimbingWorld.CreateAsync(_f);
        await me.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/follow", new { });
        await w.BoulderAsync("6A");
        (await InboxAsync(me)).Single().Title.Should().Be("New boulder in Cave");
    }
}
