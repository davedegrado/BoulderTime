using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Notifications;

public sealed record NotificationDto(
    Guid Id, NotificationType Type, NotificationCategory Category, string Title, string? Body, string Link,
    RelatedEntityType RelatedEntityType, Guid RelatedEntityId, int Count, bool IsRead, DateTimeOffset CreatedAt);

public sealed record UnreadCountDto(int Unread);
/// <param name="FriendRequests">Added later (ADR-063): older apps neither read nor send it, and it stays as it is.</param>
public sealed record NotificationSettingsDto(bool GymUpdates, bool SectorUpdates, bool BoulderUpdates, bool MyContent, bool FriendRequests = true);
public sealed record UpdateNotificationSettingsRequest(bool? GymUpdates, bool? SectorUpdates, bool? BoulderUpdates, bool? MyContent, bool? FriendRequests = null);

/// <summary>The signed-in user's own inbox and settings. Nobody can read or change another user's notifications.</summary>
public sealed class NotificationService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<PagedResult<NotificationDto>> ListAsync(bool? unreadOnly, int? page, int? pageSize, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var (p, size) = Paging.Normalize(page, pageSize, 30);
        var q = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        if (unreadOnly == true) q = q.Where(n => n.ReadAt == null);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(n => n.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<NotificationDto>(rows.Select(ToDto).ToList(), p, size, total);
    }

    public async Task<UnreadCountDto> UnreadCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        return new UnreadCountDto(await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct));
    }

    public async Task<NotificationDto> MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct) ?? throw new NotFoundException("Notification", id);
        n.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return ToDto(n);
    }

    public async Task<UnreadCountDto> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var unread = await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null).ToListAsync(ct);
        foreach (var n in unread) n.MarkRead(now);
        await db.SaveChangesAsync(ct);
        return new UnreadCountDto(0);
    }

    public async Task<NotificationSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var s = await db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct) ?? Domain.Notifications.NotificationSettings.For(userId);
        return ToDto(s);
    }

    public async Task<NotificationSettingsDto> UpdateSettingsAsync(UpdateNotificationSettingsRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var s = await db.NotificationSettings.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (s is null) db.NotificationSettings.Add(s = Domain.Notifications.NotificationSettings.For(userId));
        s.Update(r.GymUpdates, r.SectorUpdates, r.BoulderUpdates, r.MyContent, r.FriendRequests);
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { throw new ConflictException("Settings changed on another device. Refresh and try again.", "concurrent_update"); }
        return ToDto(s);
    }

    private static NotificationSettingsDto ToDto(Domain.Notifications.NotificationSettings s) =>
        new(s.GymUpdates, s.SectorUpdates, s.BoulderUpdates, s.MyContent, s.FriendRequests);

    private static NotificationDto ToDto(Notification n) =>
        new(n.Id, n.Type, Notification.CategoryOf(n.Type), n.Title, n.Body, n.Link, n.RelatedEntityType, n.RelatedEntityId, n.Count, n.ReadAt is not null, n.CreatedAt);
}
