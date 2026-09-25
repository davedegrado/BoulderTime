using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Notifications;

public sealed record PushKeysDto(string? P256dh, string? Auth);
public sealed record SubscribeToPushRequest(string? Endpoint, PushKeysDto? Keys);
public sealed record PushStatusDto(bool Available, string? PublicKey, bool SubscribedOnThisDevice);

/// <summary>
/// The devices a person agreed to be notified on. Each device registers itself; nobody can register another
/// person's device, and unsubscribing removes it immediately.
/// </summary>
public sealed class PushSubscriptionService(IAppDbContext db, ICurrentUser currentUser, IClock clock, IPushSender sender, IPushConfig config)
{
    public async Task<PushStatusDto> StatusAsync(string? endpoint, CancellationToken ct = default)
    {
        if (!sender.IsConfigured) return new PushStatusDto(false, null, false);
        var userId = currentUser.RequireUserId();
        var subscribed = endpoint is { Length: > 0 }
            && await db.PushSubscriptions.AsNoTracking().AnyAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
        return new PushStatusDto(true, config.PublicKey, subscribed);
    }

    public async Task SubscribeAsync(SubscribeToPushRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var endpoint = Input.Trimmed(r.Endpoint);
        var p256dh = Input.Trimmed(r.Keys?.P256dh);
        var auth = Input.Trimmed(r.Keys?.Auth);
        if (endpoint.Length == 0 || p256dh.Length == 0 || auth.Length == 0)
            throw new ValidationException("endpoint", "The device didn't provide a complete subscription.");
        if (endpoint.Length > PushSubscription.EndpointMaxLength || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationException("endpoint", "That isn't a valid push endpoint.");

        // The same device re-subscribing (keys rotate) updates its row instead of adding another.
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (existing is not null && existing.UserId != userId) db.PushSubscriptions.Remove(existing);
        else if (existing is not null)
        {
            existing.Refresh(p256dh, auth, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return;
        }
        db.PushSubscriptions.Add(PushSubscription.Create(userId, endpoint, p256dh, auth, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(string? endpoint, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var trimmed = Input.Trimmed(endpoint);
        var rows = await db.PushSubscriptions
            .Where(s => s.UserId == userId && (trimmed.Length == 0 || s.Endpoint == trimmed)).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.PushSubscriptions.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }
}
