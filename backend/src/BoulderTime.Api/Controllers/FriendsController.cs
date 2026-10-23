using BoulderTime.Api.Security;
using BoulderTime.Application.Common;
using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// Friendships (ADR-063). Each call answers with the viewer's friendship as it now stands, so the app shows the right
/// button without asking again. Signed in only.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class FriendsController(FriendshipService friends) : ControllerBase
{
    /// <summary>Asks to be friends; with someone who already asked, it means yes. Asking twice changes nothing.</summary>
    [HttpPost("api/users/{userId:guid}/friend-request"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<FriendshipDto> SendRequest(Guid userId, CancellationToken ct) => friends.RequestAsync(userId, ct);

    [HttpPost("api/users/{userId:guid}/friend-request/accept")]
    public Task<FriendshipDto> Accept(Guid userId, CancellationToken ct) => friends.AcceptAsync(userId, ct);

    /// <summary>Cancels the viewer's request, declines theirs, or ends the friendship.</summary>
    [HttpDelete("api/users/{userId:guid}/friendship")]
    public Task<FriendshipDto> Remove(Guid userId, CancellationToken ct) => friends.RemoveAsync(userId, ct);

    [HttpGet("api/users/me/friend-requests")]
    public Task<IReadOnlyList<FriendRequestDto>> Incoming(CancellationToken ct) => friends.IncomingAsync(ct);

    /// <summary>A person's friends, for whoever may see their profile's content.</summary>
    [HttpGet("api/users/{userId:guid}/friends")]
    public Task<PagedResult<FriendDto>> Friends(Guid userId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        friends.FriendsOfAsync(userId, page, pageSize, ct);
}
