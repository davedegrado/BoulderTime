using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Localization;
using BoulderTime.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Notifications;

public sealed record PushKeysDto(string? P256dh, string? Auth);

/// <summary>
/// A device registering itself. A browser sends its push endpoint and keys; a store app sends its Firebase token.
/// </summary>
public sealed record SubscribeToPushRequest(string? Endpoint, PushKeysDto? Keys, string? Token, PushPlatform? Platform);

public sealed record PushStatusDto(bool Available, string? PublicKey, bool SubscribedOnThisDevice);

/// <summary>
/// How a test notification went: delivered (handed to Firebase or the browser's push service), failed, gone (the
/// device is no longer registered with the push service) or skipped (the server can't send to this kind of device).
/// `Detail` is the push service's own reason.
/// </summary>
public sealed record PushTestDto(string Outcome, string? Detail);

/// <summary>
/// The devices a person agreed to be notified on. Each device registers itself; nobody can register another
/// person's device, and unregistering removes it immediately.
/// </summary>
public sealed class PushSubscriptionService(IAppDbContext db, ICurrentUser currentUser, IClock clock, IEnumerable<IPushSender> senders, IPushConfig config)
{
    private bool CanSendTo(PushPlatform platform) => senders.Any(s => s.Handles(platform) && s.IsConfigured);

    public async Task<PushStatusDto> StatusAsync(string? address, PushPlatform? platform, CancellationToken ct = default)
    {
        var kind = platform ?? PushPlatform.Web;
        if (!CanSendTo(kind)) return new PushStatusDto(false, null, false);

        var userId = currentUser.RequireUserId();
        var subscribed = address is { Length: > 0 }
            && await db.PushSubscriptions.AsNoTracking().AnyAsync(s => s.UserId == userId && s.Address == address, ct);
        return new PushStatusDto(true, kind == PushPlatform.Web ? config.PublicKey : null, subscribed);
    }

    public async Task SubscribeAsync(SubscribeToPushRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var platform = r.Platform ?? PushPlatform.Web;
        if (!Enum.IsDefined(platform)) throw new ValidationException("platform", "Unknown kind of device.");

        var (address, p256dh, auth) = platform == PushPlatform.Native ? NativeDevice(r) : WebDevice(r);

        // The same device registering again (keys rotate, tokens are reissued) updates its row instead of adding one.
        // An address can only belong to one account: a shared phone follows whoever signed in on it.
        var existing = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Address == address, ct);
        if (existing is not null && existing.UserId == userId)
        {
            existing.Refresh(platform, p256dh, auth, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return;
        }
        if (existing is not null) db.PushSubscriptions.Remove(existing);

        db.PushSubscriptions.Add(platform == PushPlatform.Native
            ? PushSubscription.CreateNative(userId, address, clock.UtcNow)
            : PushSubscription.CreateWeb(userId, address, p256dh, auth, clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private static (string Address, string P256dh, string Auth) WebDevice(SubscribeToPushRequest r)
    {
        var endpoint = Input.Trimmed(r.Endpoint);
        var p256dh = Input.Trimmed(r.Keys?.P256dh);
        var auth = Input.Trimmed(r.Keys?.Auth);
        if (endpoint.Length == 0 || p256dh.Length == 0 || auth.Length == 0)
            throw new ValidationException("endpoint", "The device didn't provide a complete subscription.");
        if (endpoint.Length > PushSubscription.AddressMaxLength
            || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationException("endpoint", "That isn't a valid push endpoint.");
        return (endpoint, p256dh, auth);
    }

    private static (string Address, string P256dh, string Auth) NativeDevice(SubscribeToPushRequest r)
    {
        var token = Input.Trimmed(r.Token);
        // A Firebase token is an opaque string; all we can check is that it is one, and short enough to store.
        if (token.Length == 0 || token.Length > PushSubscription.AddressMaxLength || token.Any(char.IsWhiteSpace))
            throw new ValidationException("token", "The app didn't provide a valid notification token.");
        return (token, "", "");
    }

    public async Task UnsubscribeAsync(string? address, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var trimmed = Input.Trimmed(address);
        var rows = await db.PushSubscriptions
            .Where(s => s.UserId == userId && (trimmed.Length == 0 || s.Address == trimmed)).ToListAsync(ct);
        if (rows.Count == 0) return;
        db.PushSubscriptions.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Sends a notification to one of the person's own devices, right now and outside the queue, and says how it
    /// went. It is how someone finds out whether their phone can be reached without waiting for a new boulder.
    /// </summary>
    public async Task<PushTestDto> TestAsync(string? address, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var trimmed = Input.Trimmed(address);
        var device = trimmed.Length == 0 ? null
            : await db.PushSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId && s.Address == trimmed, ct);
        if (device is null) throw new NotFoundException("Device", trimmed);

        var sender = senders.FirstOrDefault(s => s.Handles(device.Platform) && s.IsConfigured);
        if (sender is null) return new PushTestDto("skipped", "NOT_CONFIGURED");

        var language = Language.Normalize(await db.Users.Where(u => u.Id == userId).Select(u => u.Language).FirstOrDefaultAsync(ct));
        var message = language == Language.English
            ? new PushMessage("BoulderTime", "Test notification: this phone receives notifications.", "/notifications/settings", "push-test")
            : new PushMessage("BoulderTime", "Notifica di prova: questo telefono riceve le notifiche.", "/notifications/settings", "push-test");

        var attempt = await sender.SendWithDetailAsync(new PushTarget(device.Platform, device.Address, device.P256dh, device.Auth), message, ct);
        switch (attempt.Result)
        {
            case PushResult.Delivered: device.Succeeded(clock.UtcNow); break;
            case PushResult.Gone: db.PushSubscriptions.Remove(device); break;
        }
        await db.SaveChangesAsync(ct);
        return new PushTestDto(attempt.Result.ToString().ToLowerInvariant(), attempt.Detail);
    }
}
