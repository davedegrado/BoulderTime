namespace BoulderTime.Application.Abstractions;

public sealed record PushTarget(string Endpoint, string P256dh, string Auth);

/// <summary>What the device shows. Kept small: push services limit the payload size.</summary>
public sealed record PushMessage(string Title, string Body, string Url, string? Tag = null);

public enum PushResult { Delivered, Failed, Gone, Skipped }

/// <summary>Delivers a notification to one device. Implemented over Web Push.</summary>
public interface IPushSender
{
    bool IsConfigured { get; }
    Task<PushResult> SendAsync(PushTarget target, PushMessage message, CancellationToken ct = default);
}

/// <summary>The public half of the VAPID key pair, which devices need in order to subscribe.</summary>
public interface IPushConfig
{
    string? PublicKey { get; }
}
