using BoulderTime.Domain.Notifications;

namespace BoulderTime.Application.Abstractions;

/// <summary>One device to deliver to. The keys are empty for a store app, which needs no payload encryption.</summary>
public sealed record PushTarget(PushPlatform Platform, string Address, string P256dh, string Auth);

/// <summary>What the device shows. Kept small: push services limit the payload size.</summary>
public sealed record PushMessage(string Title, string Body, string Url, string? Tag = null);

public enum PushResult { Delivered, Failed, Gone, Skipped }

/// <summary>A delivery and, when it didn't go through, what the push service said (e.g. Firebase's error code).</summary>
public sealed record PushAttempt(PushResult Result, string? Detail = null);

/// <summary>
/// Delivers a notification to one device. There is one sender per platform — Web Push for browsers, Firebase for the
/// store apps — and the dispatcher picks the one that handles the device.
/// </summary>
public interface IPushSender
{
    bool Handles(PushPlatform platform);
    bool IsConfigured { get; }
    Task<PushResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default);

    /// <summary>
    /// The same delivery, keeping the push service's reason when it fails: the test notification shows it, so a
    /// setup mistake (a missing Apple key in Firebase, say) can be read off the phone instead of the server logs.
    /// </summary>
    async Task<PushAttempt> SendWithDetailAsync(PushTarget target, PushMessage message, CancellationToken ct = default) =>
        new(await SendAsync(target, message, ct));
}

/// <summary>The public half of the VAPID key pair, which browsers need in order to subscribe.</summary>
public interface IPushConfig
{
    string? PublicKey { get; }
}
