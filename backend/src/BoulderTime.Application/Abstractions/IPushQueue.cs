namespace BoulderTime.Application.Abstractions;

/// <summary>
/// Hands a notification to the background sender. Queueing never fails and never waits: setting boulders must not
/// slow down because a push service is slow, and a lost buzz is far less bad than a stuck request.
/// </summary>
public interface IPushQueue
{
    void Enqueue(Guid userId, PushMessage message);
}
