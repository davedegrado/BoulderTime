using BoulderTime.Application.Users;
using Microsoft.Extensions.Caching.Memory;

namespace BoulderTime.Api.Auth;

/// <summary>
/// Ensures every authenticated caller has a BoulderTime user row before any endpoint runs, so
/// controllers can rely on it. A short in-memory cache avoids a database round-trip per request.
/// </summary>
public sealed class UserProvisioningMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context, UserService users)
    {
        var identity = IdentityClaims.Read(context.User);
        if (identity is not null)
        {
            var cacheKey = $"provisioned:{identity.Subject}:{identity.Email}";
            if (!cache.TryGetValue(cacheKey, out _))
            {
                await users.EnsureProvisionedAsync(identity, context.RequestAborted, context.Request.Headers.AcceptLanguage.ToString());
                cache.Set(cacheKey, true, CacheFor);
            }
        }
        await next(context);
    }
}
