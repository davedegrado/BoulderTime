using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Api.Auth;

/// <summary>
/// Stops a suspended account at the door: every request is refused with <c>account_suspended</c>, except reading
/// one's own account (so the app can say what happened) and deleting it, which nobody may be prevented from doing.
/// Checked on every request rather than cached, so a suspension takes effect at once; it is one primary-key lookup.
/// </summary>
public sealed class AccountSuspensionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAppDbContext db)
    {
        var identity = IdentityClaims.Read(context.User);
        if (identity is not null && !AllowedWhileSuspended(context.Request)
            && await db.Users.AsNoTracking().AnyAsync(u => u.Id == identity.Subject && u.SuspendedAt != null, context.RequestAborted))
        {
            throw new ForbiddenException("This account has been suspended.", "account_suspended");
        }
        await next(context);
    }

    private static bool AllowedWhileSuspended(HttpRequest request)
    {
        var path = (request.Path.Value ?? "").TrimEnd('/');
        if (HttpMethods.IsGet(request.Method) && path.Equals("/api/users/me", StringComparison.OrdinalIgnoreCase)) return true;
        return path.StartsWith("/api/users/me/deletion", StringComparison.OrdinalIgnoreCase);
    }
}
