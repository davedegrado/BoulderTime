using BoulderTime.Application.Notifications;
using BoulderTime.Domain.Notifications;
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
    /// <summary>
    /// Whether notifications can be delivered to this kind of device, the key a browser needs to subscribe, and
    /// whether this device is already registered. `address` is the push endpoint, or the app's Firebase token.
    /// </summary>
    [HttpGet]
    public Task<PushStatusDto> Status([FromQuery] string? address, [FromQuery] PushPlatform? platform, CancellationToken ct) =>
        service.StatusAsync(address, platform, ct);

    [HttpPost]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeToPushRequest request, CancellationToken ct)
    {
        await service.SubscribeAsync(request, ct);
        return NoContent();
    }

    /// <summary>Stops notifications on one device, or on all of them when no address is given.</summary>
    [HttpDelete]
    public async Task<IActionResult> Unsubscribe([FromQuery] string? address, CancellationToken ct)
    {
        await service.UnsubscribeAsync(address, ct);
        return NoContent();
    }
}
