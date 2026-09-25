namespace BoulderTime.Domain.Notifications;

/// <summary>
/// One browser (or installed app) that agreed to receive notifications. A person can have several: phone, tablet,
/// desktop. The endpoint identifies it and belongs to the browser vendor's push service, not to us.
/// </summary>
public class PushSubscription
{
    public const int EndpointMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>The push service URL to deliver to. Unique: re-subscribing the same device updates instead of duplicating.</summary>
    public string Endpoint { get; private set; } = "";
    /// <summary>The device's public key (base64url), used to encrypt the payload so the push service can't read it.</summary>
    public string P256dh { get; private set; } = "";
    /// <summary>The device's authentication secret (base64url).</summary>
    public string Auth { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSucceededAt { get; private set; }
    /// <summary>Consecutive delivery failures; a device that keeps failing is dropped rather than retried forever.</summary>
    public int Failures { get; private set; }

    private PushSubscription() { }

    public static PushSubscription Create(Guid userId, string endpoint, string p256dh, string auth, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Endpoint = endpoint.Trim(), P256dh = p256dh.Trim(), Auth = auth.Trim(), CreatedAt = now,
    };

    public void Refresh(string p256dh, string auth, DateTimeOffset now)
    {
        P256dh = p256dh.Trim();
        Auth = auth.Trim();
        Failures = 0;
        LastSucceededAt = null;
        CreatedAt = now;
    }

    public void Succeeded(DateTimeOffset now)
    {
        LastSucceededAt = now;
        Failures = 0;
    }

    public void Failed() => Failures++;
}
