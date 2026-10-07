using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>People the signed-in climber has chosen not to see.</summary>
[ApiController]
[Authorize]
[Route("api/users/me/blocks")]
[Produces("application/json")]
public sealed class BlocksController(BlockService blocks) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<BlockedPersonDto>> List(CancellationToken ct) => blocks.ListAsync(ct);

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Block(Guid userId, [FromBody] BlockUserRequest request, CancellationToken ct)
    {
        await blocks.BlockAsync(userId, request, ct);
        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken ct)
    {
        await blocks.UnblockAsync(userId, ct);
        return NoContent();
    }
}
