using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class NotificationsController(NotificationService notifications, AnnouncementService announcements) : ControllerBase
{
    // ---- Inbox ----

    [HttpGet("api/notifications"), Authorize]
    public Task<PagedResult<NotificationDto>> List([FromQuery] bool? unreadOnly, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        notifications.ListAsync(unreadOnly, page, pageSize, ct);

    [HttpGet("api/notifications/unread-count"), Authorize]
    public Task<UnreadCountDto> UnreadCount(CancellationToken ct) => notifications.UnreadCountAsync(ct);

    [HttpPost("api/notifications/{id:guid}/read"), Authorize]
    public Task<NotificationDto> MarkRead(Guid id, CancellationToken ct) => notifications.MarkReadAsync(id, ct);

    [HttpPost("api/notifications/read-all"), Authorize]
    public Task<UnreadCountDto> MarkAllRead(CancellationToken ct) => notifications.MarkAllReadAsync(ct);

    [HttpGet("api/users/me/notification-settings"), Authorize]
    public Task<NotificationSettingsDto> GetSettings(CancellationToken ct) => notifications.GetSettingsAsync(ct);

    [HttpPut("api/users/me/notification-settings"), Authorize]
    public Task<NotificationSettingsDto> UpdateSettings([FromBody] UpdateNotificationSettingsRequest request, CancellationToken ct) =>
        notifications.UpdateSettingsAsync(request, ct);

    // ---- Gym announcements & events ----

    [HttpGet("api/gyms/{gymId:guid}/announcements"), AllowAnonymous]
    public Task<PagedResult<AnnouncementDto>> ListAnnouncements(Guid gymId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        announcements.ListAsync(gymId, page, pageSize, ct);

    [HttpPost("api/gyms/{gymId:guid}/announcement-images"), Authorize]
    public Task<UploadTicket> AnnouncementImage(Guid gymId, [FromBody] PhotoUploadRequest request, CancellationToken ct) =>
        announcements.CreateImageUploadAsync(gymId, request, ct);

    [HttpPost("api/gyms/{gymId:guid}/announcements"), Authorize]
    public async Task<ActionResult<AnnouncementDto>> CreateAnnouncement(Guid gymId, [FromBody] SaveAnnouncementRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await announcements.CreateAsync(gymId, request, ct));

    [HttpPut("api/announcements/{id:guid}"), Authorize]
    public Task<AnnouncementDto> UpdateAnnouncement(Guid id, [FromBody] SaveAnnouncementRequest request, CancellationToken ct) =>
        announcements.UpdateAsync(id, request, ct);

    [HttpDelete("api/announcements/{id:guid}"), Authorize]
    public async Task<IActionResult> DeleteAnnouncement(Guid id, CancellationToken ct) { await announcements.DeleteAsync(id, ct); return NoContent(); }
}
