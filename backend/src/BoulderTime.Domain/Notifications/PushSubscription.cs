namespace BoulderTime.Domain.Notifications;

/// <summary>How a device is reached. Each one needs a different sender, so the device says which it is.</summary>
public enum PushPlatform
{
    /// <summary>A browser or installed PWA, reached through Web Push (RFC 8030).</summary>
    Web = 0,
    /// <summary>A store app, reached through Firebase Cloud Messaging.</summary>
    Native = 1,
}

/// <summary>
/// One device that agreed to receive notifications: a browser, an installed PWA, or a store app. A person can have
/// several — phone, tablet, desktop.
///
/// <see cref="Address"/> is what identifies it, and it is unique: the push service URL for a browser, the Firebase
/// registration token for a store app. Re-registering the same device updates its row instead of adding another.
/// The two keys are the browser's own, used to encrypt the payload; a store app has none, because Firebase handles
/// the transport.
/// </summary>
public class PushSubscription
{
    public const int AddressMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public PushPlatform Platform { get; private set; }
    public string Address { get; private set; } = "";
    public string P256dh { get; private set; } = "";
    public string Auth { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastSucceededAt { get; private set; }
    /// <summary>Consecutive delivery failures; a device that keeps failing is dropped rather than retried forever.</summary>
    public int Failures { get; private set; }

    private PushSubscription() { }

    /// <summary>A browser or installed PWA: the push service URL plus the keys that encrypt the payload for it.</summary>
    public static PushSubscription CreateWeb(Guid userId, string endpoint, string p256dh, string auth, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Platform = PushPlatform.Web,
        Address = endpoint.Trim(), P256dh = p256dh.Trim(), Auth = auth.Trim(), CreatedAt = now,
    };

    /// <summary>A store app: the Firebase registration token, which is all Firebase needs to reach the device.</summary>
    public static PushSubscription CreateNative(Guid userId, string token, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Platform = PushPlatform.Native, Address = token.Trim(), CreatedAt = now,
    };

    /// <summary>The same device registering again: its keys may have rotated, and its failures start over.</summary>
    public void Refresh(PushPlatform platform, string p256dh, string auth, DateTimeOffset now)
    {
        Platform = platform;
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
