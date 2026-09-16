using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Grading;
using BoulderTime.Domain.Boulders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class BouldersController(BoulderService boulders, GradingService grading) : ControllerBase
{
    // ---- Grading systems ----

    [HttpGet("api/gyms/{gymId:guid}/grade-systems"), AllowAnonymous]
    public Task<IReadOnlyList<GradeSystemDto>> ListGradeSystems(Guid gymId, CancellationToken ct) => grading.ListAsync(gymId, ct);

    [HttpPost("api/gyms/{gymId:guid}/grade-systems"), Authorize]
    public async Task<ActionResult<GradeSystemDto>> CreateGradeSystem(Guid gymId, [FromBody] CreateGradeSystemRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await grading.CreateAsync(gymId, request, ct));

    [HttpPut("api/gyms/{gymId:guid}/grade-systems/order"), Authorize]
    public Task<IReadOnlyList<GradeSystemDto>> ReorderGradeSystems(Guid gymId, [FromBody] ReorderGradeSystemsRequest request, CancellationToken ct) =>
        grading.ReorderAsync(gymId, request, ct);

    [HttpPatch("api/grade-systems/{systemId:guid}"), Authorize]
    public Task<GradeSystemDto> UpdateGradeSystem(Guid systemId, [FromBody] UpdateGradeSystemRequest request, CancellationToken ct) =>
        grading.UpdateAsync(systemId, request, ct);

    [HttpPut("api/grade-systems/{systemId:guid}/values"), Authorize]
    public Task<GradeSystemDto> SetGradeValues(Guid systemId, [FromBody] SetGradeValuesRequest request, CancellationToken ct) =>
        grading.SetValuesAsync(systemId, request, ct);

    // ---- Boulders ----

    [HttpGet("api/gyms/{gymId:guid}/boulders"), AllowAnonymous]
    public Task<PagedResult<BoulderSummaryDto>> List(Guid gymId, [FromQuery] BoulderStatus? status, [FromQuery] Guid? sectorId,
        [FromQuery] HoldColor? holdColor, [FromQuery] Guid? gradeValueId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        boulders.ListAsync(gymId, new BoulderQuery(status, sectorId, holdColor, gradeValueId, page, pageSize), ct);

    [HttpGet("api/boulders/{boulderId:guid}"), AllowAnonymous]
    public Task<BoulderDetailDto> Get(Guid boulderId, CancellationToken ct) => boulders.GetAsync(boulderId, ct);

    [HttpPost("api/gyms/{gymId:guid}/boulder-photos"), Authorize]
    public Task<UploadTicket> CreatePhotoUpload(Guid gymId, [FromBody] PhotoUploadRequest request, CancellationToken ct) =>
        boulders.CreatePhotoUploadAsync(gymId, request, ct);

    [HttpPost("api/gyms/{gymId:guid}/boulders"), Authorize]
    public async Task<ActionResult<BoulderDetailDto>> Create(Guid gymId, [FromBody] SaveBoulderRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await boulders.CreateAsync(gymId, request, ct));

    [HttpPut("api/boulders/{boulderId:guid}"), Authorize]
    public Task<BoulderDetailDto> Update(Guid boulderId, [FromBody] SaveBoulderRequest request, CancellationToken ct) =>
        boulders.UpdateAsync(boulderId, request, ct);

    [HttpPost("api/gyms/{gymId:guid}/boulders/remove"), Authorize]
    public Task<RemoveBouldersResult> Remove(Guid gymId, [FromBody] RemoveBouldersRequest request, CancellationToken ct) =>
        boulders.RemoveAsync(gymId, request, ct);

    [HttpPost("api/boulders/{boulderId:guid}/restore"), Authorize]
    public Task<BoulderDetailDto> Restore(Guid boulderId, CancellationToken ct) => boulders.RestoreAsync(boulderId, ct);
}
