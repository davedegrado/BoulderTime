namespace BoulderTime.Application.Abstractions;

/// <summary>
/// Whether video files can be uploaded at all, and how large they may be (ADR-049). Set on the server, never in the
/// app, so that turning uploads on with a bigger storage plan is a change of settings, not a new release:
/// <c>Videos__UploadsEnabled</c> and <c>Videos__MaxUploadMegabytes</c>.
/// </summary>
public interface IVideoSettings
{
    /// <summary>Off: no video file is accepted — climbers' or official beta; linking a video still works.</summary>
    bool UploadsEnabled { get; }

    /// <summary>Must not exceed what storage itself accepts (Supabase Free refuses any file over 50 MB).</summary>
    int MaxUploadMegabytes { get; }

    long MaxUploadBytes => MaxUploadMegabytes * 1024L * 1024L;
}

/// <summary>What the app is told, so it shows only what the server will accept.</summary>
public sealed record VideoSettingsDto(bool UploadsEnabled, int MaxUploadMegabytes);
