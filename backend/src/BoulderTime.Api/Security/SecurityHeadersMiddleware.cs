namespace BoulderTime.Api.Security;

/// <summary>
/// Response headers that cost nothing and remove whole classes of attack against the API: no MIME sniffing,
/// no framing (the API is never embedded), no referrer leakage of API URLs, and an explicit cross-origin policy.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-site";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        return next(context);
    }
}
