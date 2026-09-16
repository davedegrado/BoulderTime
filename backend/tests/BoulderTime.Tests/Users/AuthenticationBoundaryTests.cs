using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Tests.Users;

[Collection(DatabaseCollection.Name)]
public sealed class AuthenticationBoundaryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _factory = new(postgres);

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task Me_without_token_is_401_problem_details()
    {
        var res = await _factory.CreateClient().GetAsync("/api/users/me");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await res.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions.Should().ContainKey("traceId");
    }

    public static TheoryData<string, string> InvalidTokens => new()
    {
        { "wrong signature", TestTokens.For(Guid.NewGuid(), secret: "a-completely-different-secret-of-32-bytes!!") },
        { "wrong audience", TestTokens.For(Guid.NewGuid(), audience: "anon") },
        { "wrong issuer", TestTokens.For(Guid.NewGuid(), issuer: "https://evil.supabase.co/auth/v1") },
        { "expired", TestTokens.For(Guid.NewGuid(), expires: DateTime.UtcNow.AddMinutes(-10)) },
    };

    [Theory]
    [MemberData(nameof(InvalidTokens))]
    public async Task Invalid_tokens_are_rejected_and_never_provision_users(string reason, string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await client.GetAsync("/api/users/me");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized, reason);
        (await _factory.WithDbAsync(db => Task.FromResult(db.Users.Count()))).Should().Be(0);
    }

    [Fact]
    public async Task Health_is_public()
    {
        var res = await _factory.CreateClient().GetAsync("/api/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
