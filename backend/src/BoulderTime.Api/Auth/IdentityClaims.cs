using System.Security.Claims;
using System.Text.Json;
using BoulderTime.Application.Users;

namespace BoulderTime.Api.Auth;

/// <summary>
/// Reads the verified Supabase token. Deliberately ignores "role", "app_metadata" and any other
/// authorization-ish claims: permissions are loaded from the database, never trusted from the token.
/// </summary>
public static class IdentityClaims
{
    public static VerifiedIdentity? Read(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var subject)) return null;

        var email = principal.FindFirst("email")?.Value;
        string? displayName = null, avatarUrl = null, legalVersion = null;
        var minimumAge = false;

        var metadata = principal.FindFirst("user_metadata")?.Value;
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            try
            {
                using var doc = JsonDocument.Parse(metadata);
                var root = doc.RootElement;
                // "display_name" is set by our sign-up form; "full_name"/"name"/"picture" come from Google.
                displayName = FirstString(root, "display_name", "full_name", "name");
                avatarUrl = FirstString(root, "avatar_url", "picture");
                // Ticked on our sign-up form (ADR-041). The person's own answers, like everything they post.
                legalVersion = FirstString(root, "legal_version");
                minimumAge = root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("minimum_age_confirmed", out var age) && age.ValueKind == JsonValueKind.True;
            }
            catch (JsonException)
            {
                // Malformed metadata is not fatal; fall back to email-derived defaults.
            }
        }

        return new VerifiedIdentity(subject, email, displayName, avatarUrl, legalVersion, minimumAge);
    }

    private static string? FirstString(JsonElement root, params string[] names)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()))
                return v.GetString();
        return null;
    }
}
