using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BoulderTime.Application.Users;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Users;

[Collection(DatabaseCollection.Name)]
public sealed class UsersApiTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(postgres);

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task First_authenticated_request_provisions_the_user_from_token_claims()
    {
        var id = Guid.NewGuid();
        var client = _factory.ClientFor(id, "Marco.Rossi@Example.com", new { full_name = "Marco Rossi", avatar_url = "https://img.example/m.png" });

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/me");

        me!.Id.Should().Be(id);
        me.Email.Should().Be("marco.rossi@example.com");
        me.DisplayName.Should().Be("Marco Rossi");
        me.AvatarUrl.Should().Be("https://img.example/m.png");
        me.IsPlatformAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Concurrent_first_requests_create_exactly_one_user()
    {
        var id = Guid.NewGuid();
        var client = _factory.ClientFor(id);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetAsync("/api/users/me")));

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        (await _factory.WithDbAsync(db => db.Users.CountAsync(u => u.Id == id))).Should().Be(1);
    }

    [Fact]
    public async Task Role_and_app_metadata_claims_in_the_token_never_grant_platform_admin()
    {
        var extra = new Dictionary<string, object>
        {
            ["app_metadata"] = JsonSerializer.SerializeToElement(new { role = "platform_admin", is_platform_admin = true }),
            ["is_platform_admin"] = true,
        };
        var id = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestTokens.For(id, extraClaims: extra));

        var me = await client.GetFromJsonAsync<CurrentUserDto>("/api/users/me");

        me!.IsPlatformAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task Display_name_set_by_user_is_not_overwritten_by_later_token_metadata()
    {
        var id = Guid.NewGuid();
        await _factory.ClientFor(id, metadata: new { full_name = "Google Name" }).GetAsync("/api/users/me");
        (await _factory.ClientFor(id).PatchAsJsonAsync("/api/users/me", new { displayName = "Crimp Queen" })).EnsureSuccessStatusCode();

        // A fresh factory clears the provisioning cache, forcing the middleware to run again.
        await using var fresh = new ApiFactory(postgres);
        var me = await fresh.ClientFor(id, metadata: new { full_name = "Google Name" }).GetFromJsonAsync<CurrentUserDto>("/api/users/me");

        me!.DisplayName.Should().Be("Crimp Queen");
    }

    [Theory]
    [InlineData("A")]
    [InlineData("   ")]
    [InlineData("This display name is far too long to be accepted here")]
    public async Task Invalid_display_names_return_field_level_validation_errors(string name)
    {
        var client = _factory.ClientFor(Guid.NewGuid());

        var res = await client.PatchAsJsonAsync("/api/users/me", new { displayName = name });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
        body.RootElement.GetProperty("errors").TryGetProperty("displayName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Display_name_whitespace_is_normalized()
    {
        var client = _factory.ClientFor(Guid.NewGuid());

        var res = await client.PatchAsJsonAsync("/api/users/me", new { displayName = "  Sloper   Sam  " });

        (await res.Content.ReadFromJsonAsync<CurrentUserDto>())!.DisplayName.Should().Be("Sloper Sam");
    }
}
