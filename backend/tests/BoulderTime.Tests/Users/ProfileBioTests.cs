using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Users;

/// <summary>A profile's bio, and the number of friends under the name.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ProfileBioTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<ProfileDto> Profile(HttpClient client, Guid userId) =>
        (await (await client.GetAsync($"/api/users/{userId}/profile")).ReadAsync<ProfileDto>())!;

    private static Task<HttpResponseMessage> Patch(TestUser who, object body) => who.Client.PatchAsJsonAsync("/api/users/me", body);

    [Fact]
    public async Task The_bio_is_saved_tidied_shown_on_the_profile_and_left_alone_by_apps_that_dont_send_it()
    {
        var anna = await _f.UserAsync();
        var me = await (await Patch(anna, new { displayName = "Anna", bio = "  Crimper.  \n\n\n\nBlocchi a Milano \n" })).ReadAsync<CurrentUserDto>();
        me!.Bio.Should().Be("Crimper.\n\nBlocchi a Milano");
        (await Profile((await _f.UserAsync()).Client, anna.Id)).Bio.Should().Be("Crimper.\n\nBlocchi a Milano");

        // An older app sends only the name: the bio stays.
        (await (await Patch(anna, new { displayName = "Anna B" })).ReadAsync<CurrentUserDto>())!.Bio.Should().Be("Crimper.\n\nBlocchi a Milano");
        // Blank clears it.
        (await (await Patch(anna, new { displayName = "Anna B", bio = "   " })).ReadAsync<CurrentUserDto>())!.Bio.Should().BeNull();

        (await Patch(anna, new { displayName = "Anna B", bio = new string('a', 161) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_private_profile_still_shows_its_bio_but_not_its_number_of_friends()
    {
        var anna = await _f.UserAsync();
        var luca = await _f.UserAsync();
        var other = await _f.UserAsync();
        (await Patch(anna, new { displayName = "Anna", bio = "Ciao" })).EnsureSuccessStatusCode();
        (await luca.Client.PostAsync($"/api/users/{anna.Id}/friend-request", null)).EnsureSuccessStatusCode();
        (await other.Client.GetAsync($"/api/users/{anna.Id}/profile")).EnsureSuccessStatusCode();

        (await Profile(other.Client, anna.Id)).FriendCount.Should().Be(0, "a request is not a friend");
        (await anna.Client.PostAsync($"/api/users/{luca.Id}/friend-request/accept", null)).EnsureSuccessStatusCode();
        (await Profile(other.Client, anna.Id)).FriendCount.Should().Be(1);
        (await Profile(anna.Client, anna.Id)).FriendCount.Should().Be(1);

        (await anna.Client.PutAsJsonAsync("/api/users/me/profile-privacy", new { @private = true })).EnsureSuccessStatusCode();
        var hidden = await Profile(other.Client, anna.Id);
        hidden.ContentHidden.Should().BeTrue();
        hidden.Bio.Should().Be("Ciao");
        hidden.FriendCount.Should().BeNull();
        (await Profile(luca.Client, anna.Id)).FriendCount.Should().Be(1, "a friend sees it");
    }
}
