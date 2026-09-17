using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Community;
using BoulderTime.Domain.Community;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Comments, community grades, official beta, community videos, moderation and reports.</summary>
[ApiController]
[Produces("application/json")]
public sealed class CommunityController(CommentService comments, GradeSuggestionService grades, VideoService videos, ReportService reports) : ControllerBase
{
    // ---- Comments ----

    [HttpGet("api/boulders/{boulderId:guid}/comments"), AllowAnonymous]
    public Task<PagedResult<CommentDto>> ListComments(Guid boulderId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        comments.ListAsync(boulderId, page, pageSize, ct);

    [HttpPost("api/boulders/{boulderId:guid}/comments"), Authorize]
    public async Task<ActionResult<CommentDto>> CreateComment(Guid boulderId, [FromBody] SaveCommentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await comments.CreateAsync(boulderId, request, ct));

    [HttpPatch("api/comments/{commentId:guid}"), Authorize]
    public Task<CommentDto> EditComment(Guid commentId, [FromBody] SaveCommentRequest request, CancellationToken ct) => comments.EditAsync(commentId, request, ct);

    [HttpDelete("api/comments/{commentId:guid}"), Authorize]
    public async Task<IActionResult> DeleteComment(Guid commentId, CancellationToken ct) { await comments.DeleteAsync(commentId, ct); return NoContent(); }

    [HttpPut("api/comments/{commentId:guid}/like"), Authorize]
    public Task<CommentDto> Like(Guid commentId, CancellationToken ct) => comments.SetLikeAsync(commentId, true, ct);

    [HttpDelete("api/comments/{commentId:guid}/like"), Authorize]
    public Task<CommentDto> Unlike(Guid commentId, CancellationToken ct) => comments.SetLikeAsync(commentId, false, ct);

    [HttpPost("api/comments/{commentId:guid}/hide"), Authorize]
    public Task<CommentDto> Hide(Guid commentId, CancellationToken ct) => comments.SetHiddenAsync(commentId, true, ct);

    [HttpPost("api/comments/{commentId:guid}/unhide"), Authorize]
    public Task<CommentDto> Unhide(Guid commentId, CancellationToken ct) => comments.SetHiddenAsync(commentId, false, ct);

    // ---- Community grade ----

    [HttpGet("api/boulders/{boulderId:guid}/grade-consensus"), AllowAnonymous]
    public Task<GradeConsensusDto> Consensus(Guid boulderId, CancellationToken ct) => grades.GetAsync(boulderId, ct);

    [HttpPut("api/boulders/{boulderId:guid}/grade-suggestions"), Authorize]
    public Task<GradeConsensusDto> Suggest(Guid boulderId, [FromBody] SuggestGradeRequest request, CancellationToken ct) => grades.SuggestAsync(boulderId, request, ct);

    [HttpDelete("api/boulders/{boulderId:guid}/grade-suggestions/{gradeSystemId:guid}"), Authorize]
    public Task<GradeConsensusDto> Withdraw(Guid boulderId, Guid gradeSystemId, CancellationToken ct) => grades.WithdrawAsync(boulderId, gradeSystemId, ct);

    // ---- Videos ----

    [HttpPost("api/boulders/{boulderId:guid}/video-uploads"), Authorize]
    public Task<UploadTicket> VideoUpload(Guid boulderId, [FromBody] VideoUploadRequest request, CancellationToken ct) => videos.CreateUploadAsync(boulderId, request, ct);

    [HttpGet("api/boulders/{boulderId:guid}/beta"), AllowAnonymous]
    public async Task<ActionResult<BetaDto>> GetBeta(Guid boulderId, CancellationToken ct) =>
        await videos.GetBetaAsync(boulderId, ct) is { } beta ? Ok(beta) : NoContent();

    [HttpPut("api/boulders/{boulderId:guid}/beta"), Authorize]
    public Task<BetaDto> SaveBeta(Guid boulderId, [FromBody] SaveBetaRequest request, CancellationToken ct) => videos.SaveBetaAsync(boulderId, request, ct);

    [HttpDelete("api/boulders/{boulderId:guid}/beta"), Authorize]
    public async Task<IActionResult> DeleteBeta(Guid boulderId, CancellationToken ct) { await videos.DeleteBetaAsync(boulderId, ct); return NoContent(); }

    [HttpGet("api/boulders/{boulderId:guid}/videos"), AllowAnonymous]
    public Task<BoulderVideosDto> ListVideos(Guid boulderId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        videos.ListAsync(boulderId, page, pageSize, ct);

    [HttpPost("api/boulders/{boulderId:guid}/videos"), Authorize]
    public async Task<ActionResult<VideoDto>> SubmitVideo(Guid boulderId, [FromBody] SubmitVideoRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await videos.SubmitAsync(boulderId, request, ct));

    [HttpPatch("api/videos/{videoId:guid}"), Authorize]
    public Task<VideoDto> ModifyVideo(Guid videoId, [FromBody] ModifyVideoRequest request, CancellationToken ct) => videos.ModifyAsync(videoId, request, ct);

    [HttpDelete("api/videos/{videoId:guid}"), Authorize]
    public async Task<IActionResult> DeleteVideo(Guid videoId, CancellationToken ct) { await videos.DeleteAsync(videoId, ct); return NoContent(); }

    // ---- Moderation ----

    [HttpGet("api/gyms/{gymId:guid}/moderation/summary"), Authorize]
    public Task<ModerationSummaryDto> Summary(Guid gymId, CancellationToken ct) => reports.SummaryAsync(gymId, ct);

    [HttpGet("api/gyms/{gymId:guid}/moderation/videos"), Authorize]
    public Task<IReadOnlyList<ModerationVideoDto>> PendingVideos(Guid gymId, CancellationToken ct) => videos.PendingForGymAsync(gymId, ct);

    [HttpPost("api/videos/{videoId:guid}/approve"), Authorize]
    public Task<VideoDto> Approve(Guid videoId, CancellationToken ct) => videos.ApproveAsync(videoId, ct);

    [HttpPost("api/videos/{videoId:guid}/reject"), Authorize]
    public Task<VideoDto> Reject(Guid videoId, [FromBody] RejectVideoRequest request, CancellationToken ct) => videos.RejectAsync(videoId, request, ct);

    [HttpGet("api/gyms/{gymId:guid}/moderation/reports"), Authorize]
    public Task<PagedResult<ReportDto>> GymReports(Guid gymId, [FromQuery] ReportStatus? status, [FromQuery] int? page, CancellationToken ct) =>
        reports.ListForGymAsync(gymId, status, page, ct);

    [HttpGet("api/admin/reports"), Authorize]
    public Task<PagedResult<ReportDto>> AllReports([FromQuery] ReportStatus? status, [FromQuery] int? page, CancellationToken ct) => reports.ListAllAsync(status, page, ct);

    [HttpPost("api/reports"), Authorize]
    public async Task<ActionResult<ReportDto>> CreateReport([FromBody] CreateReportRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reports.CreateAsync(request, ct));

    [HttpPost("api/reports/{reportId:guid}/resolve"), Authorize]
    public Task<ReportDto> Resolve(Guid reportId, [FromBody] CloseReportRequest request, CancellationToken ct) => reports.ResolveAsync(reportId, request, ct);

    [HttpPost("api/reports/{reportId:guid}/dismiss"), Authorize]
    public Task<ReportDto> Dismiss(Guid reportId, [FromBody] CloseReportRequest request, CancellationToken ct) => reports.DismissAsync(reportId, request, ct);
}
