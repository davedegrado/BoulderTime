using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BoulderTime.Api.Auth;

public static class AuthenticationSetup
{
    public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SupabaseAuthOptions>()
            .Bind(configuration.GetSection(SupabaseAuthOptions.Section))
            .Validate(o => Uri.TryCreate(o.Url, UriKind.Absolute, out _), "Supabase:Url must be an absolute URL.")
            .ValidateOnStart();

        services.AddHttpClient(nameof(SupabaseJwksProvider), c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<SupabaseJwksProvider>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configure bearer options from DI so the JWKS provider and bound options are available.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<SupabaseAuthOptions>, SupabaseJwksProvider>((bearer, supabaseOptions, jwks) =>
            {
                var o = supabaseOptions.Value;
                SecurityKey? symmetricKey = string.IsNullOrWhiteSpace(o.JwtSecret)
                    ? null
                    : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.JwtSecret));

                bearer.MapInboundClaims = false; // keep "sub", "email" etc. as-is
                bearer.RequireHttpsMetadata = false; // no metadata endpoint is used; keys come from the JWKS provider
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = o.Issuer,
                    ValidateAudience = true,
                    ValidAudience = o.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    NameClaimType = "sub",
                    ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSha256, SecurityAlgorithms.HmacSha256],
                    IssuerSigningKeyResolver = (_, securityToken, kid, _) =>
                    {
                        var alg = (securityToken as JsonWebToken)?.Alg;
                        if (alg == SecurityAlgorithms.HmacSha256)
                            return symmetricKey is null ? [] : [symmetricKey];
                        // The resolver API is synchronous; keys are cached so this rarely touches the network.
                        return jwks.GetKeysAsync(kid).GetAwaiter().GetResult();
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }
}
