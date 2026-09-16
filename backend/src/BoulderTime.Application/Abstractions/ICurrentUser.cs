namespace BoulderTime.Application.Abstractions;

/// <summary>
/// The caller of the current request, resolved server-side from a verified access token.
/// Only the identity (who) comes from the token; permissions (what) always come from the database.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's id, or null for anonymous requests.</summary>
    Guid? UserId { get; }

    /// <exception cref="Common.UnauthorizedException">No authenticated user.</exception>
    Guid RequireUserId();
}
