using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;

namespace BoulderTime.Api.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid RequireUserId()
    {
        var identity = accessor.HttpContext is { } ctx ? IdentityClaims.Read(ctx.User) : null;
        return identity?.Subject ?? throw new UnauthorizedException();
    }
}
