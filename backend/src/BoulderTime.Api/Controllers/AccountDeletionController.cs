using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Deleting your own account, with a week to change your mind.</summary>
[ApiController]
[Authorize]
[Route("api/users/me/deletion")]
[Produces("application/json")]
public sealed class AccountDeletionController(AccountDeletionService service) : ControllerBase
{
    [HttpGet]
    public Task<DeletionStatusDto> Status(CancellationToken ct) => service.StatusAsync(ct);

    [HttpPost]
    public Task<DeletionStatusDto> Request([FromBody] DeleteAccountRequest request, CancellationToken ct) =>
        service.RequestAsync(request, ct);

    /// <summary>Brings the account back, within the grace period.</summary>
    [HttpDelete]
    public Task<DeletionStatusDto> Cancel(CancellationToken ct) => service.CancelAsync(ct);
}
