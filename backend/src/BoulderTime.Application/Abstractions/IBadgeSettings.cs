namespace BoulderTime.Application.Abstractions;

/// <summary>
/// Server switches for badges (ADR-069). <c>Badges__PushEnabled</c>: a phone notification for each new badge, off by
/// default. Turn it on once the 1.1 apps and the web app are out: native 1.0 apps never get it anyway, but a browser
/// does not say which version it runs.
/// </summary>
public interface IBadgeSettings
{
    bool PushEnabled { get; }
}
