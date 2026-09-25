using BoulderTime.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Devices that receive notifications when the app is closed.</summary>
[ApiController]
[Authorize]
[Route("api/users/me/push")]
[Produces("application/json")]
public sealed class PushController(PushSubscriptionService service) : ControllerBase
{
    /// <summary>Whether push is available here, the key a device needs to subscribe, and whether this one already did.</summary>
    [HttpGet]
    public Task<PushStatusDto> Status([FromQuery] string? endpoint, CancellationToken ct) => service.StatusAsync(endpoint, ct);

    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeToPushRequest request, CancellationToken ct)
    {
        await service.SubscribeAsync(request, ct);
        return NoContent();
    }

    /// <summary>Stops notifications on one device, or on all of them when no endpoint is given.</summary>
    [HttpDelete]
    public async Task<IActionResult> Unsubscribe([FromQuery] string? endpoint, CancellationToken ct)
    {
        await service.UnsubscribeAsync(endpoint, ct);
        return NoContent();
    }
}
