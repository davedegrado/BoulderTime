using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;

namespace BoulderTime.Api.Auth;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => UserId is not null;

    public Guid? UserId => accessor.HttpContext is { } ctx ? IdentityClaims.Read(ctx.User)?.Subject : null;

    public Guid RequireUserId() => UserId ?? throw new UnauthorizedException();
}
