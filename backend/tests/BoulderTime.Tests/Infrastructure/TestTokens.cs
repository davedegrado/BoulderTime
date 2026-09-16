using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BoulderTime.Tests.Infrastructure;

/// <summary>Mints Supabase-shaped HS256 access tokens for tests.</summary>
public static class TestTokens
{
    public const string SupabaseUrl = "http://supabase.test";
    public const string Secret = "test-jwt-secret-that-is-at-least-32-bytes-long!";

    public static string For(
        Guid userId,
        string email = "climber@example.com",
        object? userMetadata = null,
        string audience = "authenticated",
        string issuer = SupabaseUrl + "/auth/v1",
        string secret = Secret,
        DateTime? expires = null,
        IDictionary<string, object>? extraClaims = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = userId.ToString(),
            ["email"] = email,
            ["role"] = "authenticated",
        };
        if (userMetadata is not null)
            claims["user_metadata"] = JsonSerializer.SerializeToElement(userMetadata);
        if (extraClaims is not null)
            foreach (var (k, v) in extraClaims) claims[k] = v;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = DateTime.UtcNow.AddMinutes(-1),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
