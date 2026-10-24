using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Users;

/// <summary>The Friends page: requests sent, and finding climbers by name to add them.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class FriendSearchTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<TestUser> Named(string name)
    {
        var u = await _f.UserAsync();
        (await u.Client.PatchAsJsonAsync("/api/users/me", new { displayName = name })).EnsureSuccessStatusCode();
        return u;
    }

    private static async Task<List<PersonResultDto>> Search(TestUser who, string q) =>
        (await (await who.Client.GetAsync($"/api/users/search?q={Uri.EscapeDataString(q)}")).ReadAsync<List<PersonResultDto>>())!;

    private static async Task<List<FriendRequestDto>> Sent(TestUser who) =>
        (await (await who.Client.GetAsync("/api/users/me/friend-requests/sent")).ReadAsync<List<FriendRequestDto>>())!;

    [Fact]
    public async Task Finds_climbers_by_name_with_where_the_viewer_stands_names_starting_with_it_first()
    {
        var me = await Named("Marco");
        var anna = await Named("Anna Rossi");
        var giovanna = await Named("Giovanna");
        await Named("Luca");
        (await me.Client.PostAsync($"/api/users/{giovanna.Id}/friend-request", null)).EnsureSuccessStatusCode();

        var found = await Search(me, "  ANN ");
        found.Select(r => r.Person.UserId).Should().Equal(anna.Id, giovanna.Id);
        found.Select(r => r.Friendship).Should().Equal(FriendshipState.None, FriendshipState.Outgoing);

        (await Search(me, "a")).Should().BeEmpty("one letter would list half the app");
        (await Search(me, "Marco")).Should().BeEmpty("never oneself");
        (await Search(me, "%")).Should().BeEmpty("wildcards are text");
    }

    [Fact]
    public async Task Finds_by_name_only_never_by_email_and_never_people_hidden_from_the_viewer()
    {
        var me = await Named("Marco");
        var blocked = await Named("Paola Blocked");
        var blocker = await Named("Paola Blocker");
        var suspended = await Named("Paola Suspended");
        var paola = await Named("Paola");
        (await me.Client.PutAsJsonAsync($"/api/users/me/blocks/{blocked.Id}", new { reason = (string?)null })).EnsureSuccessStatusCode();
        (await blocker.Client.PutAsJsonAsync($"/api/users/me/blocks/{me.Id}", new { reason = (string?)null })).EnsureSuccessStatusCode();
        await _f.WithDbAsync(async db =>
        {
            var u = await db.Users.FindAsync(suspended.Id);
            u!.Suspend(paola.Id, null, DateTimeOffset.UtcNow);
            return await db.SaveChangesAsync();
        });

        (await Search(me, "paola")).Select(r => r.Person.UserId).Should().Equal(paola.Id);
        (await Search(me, "example.com")).Should().BeEmpty();
        (await _f.CreateClient().GetAsync("/api/users/search?q=paola")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Lists_the_requests_sent_until_they_are_answered()
    {
        var me = await Named("Marco");
        var anna = await Named("Anna");
        var luca = await Named("Luca");
        (await me.Client.PostAsync($"/api/users/{anna.Id}/friend-request", null)).EnsureSuccessStatusCode();
        (await me.Client.PostAsync($"/api/users/{luca.Id}/friend-request", null)).EnsureSuccessStatusCode();
        (await Sent(me)).Select(r => r.Person.UserId).Should().BeEquivalentTo([anna.Id, luca.Id]);
        (await Sent(anna)).Should().BeEmpty("received is not sent");

        (await anna.Client.PostAsync($"/api/users/{me.Id}/friend-request/accept", null)).EnsureSuccessStatusCode();
        (await Sent(me)).Select(r => r.Person.UserId).Should().Equal(luca.Id);
        (await luca.Client.DeleteAsync($"/api/users/{me.Id}/friendship")).EnsureSuccessStatusCode();
        (await Sent(me)).Should().BeEmpty("declined");
    }
}
