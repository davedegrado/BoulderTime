using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BoulderTime.Api.Security;

/// <summary>
/// Request limits so a single client can't hammer the API or flood the database with comments, reports or uploads.
/// Limits are per signed-in user, or per IP for anonymous callers, and are generous enough that normal use
/// (browsing a gym, logging attempts) never hits them. Disabled in tests via RateLimiting:Enabled=false.
/// </summary>
public static class RateLimiting
{
    /// <summary>Applied to endpoints that create content or start uploads.</summary>
    public const string WritePolicy = "writes";

    public static IServiceCollection AddBoulderTimeRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue("RateLimiting:Enabled", true)) return services;

        var perMinute = configuration.GetValue("RateLimiting:RequestsPerMinute", 300);
        var writesPerMinute = configuration.GetValue("RateLimiting:WritesPerMinute", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc9110#section-15.5.29",
                    title = "Too many requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "Slow down for a moment and try again.",
                }, ct);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetFixedWindowLimiter(Client(http), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = perMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));

            options.AddPolicy(WritePolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter($"w:{Client(http)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = writesPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });
        return services;
    }

    /// <summary>The signed-in user when known, otherwise the caller's address.</summary>
    private static string Client(HttpContext http) =>
        http.User.FindFirst("sub")?.Value
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
