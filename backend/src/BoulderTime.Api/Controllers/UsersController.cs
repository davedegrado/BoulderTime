using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(UserService users, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>The signed-in user's account.</summary>
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<CurrentUserDto> GetMe(CancellationToken ct) =>
        users.GetAsync(currentUser.RequireUserId(), ct);

    /// <summary>Update the signed-in user's profile.</summary>
    [HttpPatch("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<CurrentUserDto> UpdateMe([FromBody] UpdateProfileRequest request, CancellationToken ct) =>
        users.UpdateProfileAsync(currentUser.RequireUserId(), request, ct);
}
