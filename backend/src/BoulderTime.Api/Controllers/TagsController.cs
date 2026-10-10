using BoulderTime.Application.Boulders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Each gym's own boulder tags (ADR-046). Reading is public; changing them needs the gym's staff.</summary>
[ApiController]
[Produces("application/json")]
public sealed class TagsController(TagService tags) : ControllerBase
{
    [HttpGet("api/gyms/{gymId:guid}/tags"), AllowAnonymous]
    public Task<IReadOnlyList<GymTagDto>> List(Guid gymId, CancellationToken ct) => tags.ListAsync(gymId, ct);

    [HttpPost("api/gyms/{gymId:guid}/tags"), Authorize]
    public Task<GymTagDto> Create(Guid gymId, [FromBody] SaveTagRequest request, CancellationToken ct) => tags.CreateAsync(gymId, request, ct);

    [HttpPatch("api/tags/{tagId:guid}"), Authorize]
    public Task<GymTagDto> Rename(Guid tagId, [FromBody] SaveTagRequest request, CancellationToken ct) => tags.RenameAsync(tagId, request, ct);

    [HttpDelete("api/tags/{tagId:guid}"), Authorize]
    public async Task<IActionResult> Delete(Guid tagId, CancellationToken ct)
    {
        await tags.DeleteAsync(tagId, ct);
        return NoContent();
    }
}
