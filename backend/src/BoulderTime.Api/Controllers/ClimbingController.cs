using BoulderTime.Application.Boulders;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Follows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Climbing progress, ratings, follows, history, profiles and Home.</summary>
[ApiController]
[Produces("application/json")]
public sealed class ClimbingController(ProgressService progress, FollowService follows, ActivityService activity) : ControllerBase
{
    // ---- Attempts & ratings (always the caller's own) ----

    [HttpPut("api/boulders/{boulderId:guid}/attempt"), Authorize]
    public Task<ViewerProgressDto?> SetAttempt(Guid boulderId, [FromBody] SetAttemptRequest request, CancellationToken ct) =>
        progress.SetAttemptAsync(boulderId, request, ct);

    [HttpPut("api/boulders/{boulderId:guid}/rating"), Authorize]
    public Task<ViewerProgressDto?> SetRating(Guid boulderId, [FromBody] SetRatingRequest request, CancellationToken ct) =>
        progress.SetRatingAsync(boulderId, request, ct);

    [HttpDelete("api/boulders/{boulderId:guid}/rating"), Authorize]
    public Task<ViewerProgressDto?> ClearRating(Guid boulderId, CancellationToken ct) => progress.ClearRatingAsync(boulderId, ct);

    // ---- Follows ----

    [HttpPut("api/gyms/{gymId:guid}/follow"), Authorize]
    public Task<GymFollowState> FollowGym(Guid gymId, [FromBody] GymFollowRequest request, CancellationToken ct) => follows.FollowGymAsync(gymId, request, ct);

    [HttpDelete("api/gyms/{gymId:guid}/follow"), Authorize]
    public async Task<IActionResult> UnfollowGym(Guid gymId, CancellationToken ct) { await follows.UnfollowGymAsync(gymId, ct); return NoContent(); }

    [HttpPut("api/sectors/{sectorId:guid}/follow"), Authorize]
    public Task<FollowState> FollowSector(Guid sectorId, [FromBody] FollowRequest request, CancellationToken ct) => follows.FollowSectorAsync(sectorId, request, ct);

    [HttpDelete("api/sectors/{sectorId:guid}/follow"), Authorize]
    public async Task<IActionResult> UnfollowSector(Guid sectorId, CancellationToken ct) { await follows.UnfollowSectorAsync(sectorId, ct); return NoContent(); }

    [HttpPut("api/boulders/{boulderId:guid}/follow"), Authorize]
    public Task<FollowState> FollowBoulder(Guid boulderId, [FromBody] FollowRequest request, CancellationToken ct) => follows.FollowBoulderAsync(boulderId, request, ct);

    [HttpDelete("api/boulders/{boulderId:guid}/follow"), Authorize]
    public async Task<IActionResult> UnfollowBoulder(Guid boulderId, CancellationToken ct) { await follows.UnfollowBoulderAsync(boulderId, ct); return NoContent(); }

    [HttpGet("api/users/me/follows"), Authorize]
    public Task<MyFollowsDto> MyFollows(CancellationToken ct) => follows.ListMineAsync(ct);

    // ---- History, profiles, home ----

    [HttpGet("api/users/me/home"), Authorize]
    public Task<HomeDto> Home(CancellationToken ct) => activity.HomeAsync(ct);

    [HttpGet("api/users/{userId:guid}/profile"), AllowAnonymous]
    public Task<ProfileDto> Profile(Guid userId, CancellationToken ct) => activity.ProfileAsync(userId, ct);

    [HttpGet("api/users/{userId:guid}/history"), AllowAnonymous]
    public Task<PagedResult<ClimbHistoryItemDto>> History(Guid userId, [FromQuery] HistoryFilter? filter, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        activity.HistoryAsync(userId, filter, page, pageSize, ct);
}
