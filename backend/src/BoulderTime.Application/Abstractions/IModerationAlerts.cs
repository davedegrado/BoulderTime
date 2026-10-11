namespace BoulderTime.Application.Abstractions;

/// <summary>An email to the BoulderTime team about something to moderate. <paramref name="Path"/> is the page in the
/// app where it is handled; the sender puts the site's address in front of it.</summary>
public sealed record ModerationAlert(string Subject, string Body, string Path);

/// <summary>
/// Tells the BoulderTime team when someone reports a comment, a video or a person, so reports are looked at promptly
/// rather than when someone happens to open the queue (ADR-050). Fire and forget: a report is filed whether or not
/// the email goes out, and sending never slows the request down.
/// </summary>
public interface IModerationAlerts
{
    void Send(ModerationAlert alert);
}
