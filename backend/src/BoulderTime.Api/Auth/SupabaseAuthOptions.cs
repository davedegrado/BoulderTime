namespace BoulderTime.Api.Auth;

public sealed class SupabaseAuthOptions
{
    public const string Section = "Supabase";

    /// <summary>Project URL, e.g. https://abcd.supabase.co or http://127.0.0.1:54321 for the local CLI.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Optional legacy HS256 JWT secret. Only needed for projects (or local stacks) that still sign
    /// sessions symmetrically. Projects on asymmetric signing keys are verified via the public JWKS.
    /// This is a server secret — never expose it to the frontend.
    /// </summary>
    public string? JwtSecret { get; set; }

    /// <summary>Supabase issues user session tokens with this audience.</summary>
    public string Audience { get; set; } = "authenticated";

    public string Issuer => $"{Url.TrimEnd('/')}/auth/v1";
    public string JwksUrl => $"{Issuer}/.well-known/jwks.json";
}
