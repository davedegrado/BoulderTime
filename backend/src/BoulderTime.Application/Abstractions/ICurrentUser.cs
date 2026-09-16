namespace BoulderTime.Application.Abstractions;

/// <summary>
/// The caller of the current request, resolved server-side from a verified access token.
/// Only the identity (who) comes from the token; permissions (what) always come from the database.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <exception cref="Common.UnauthorizedException">No authenticated user.</exception>
    Guid RequireUserId();
}
