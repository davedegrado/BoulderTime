namespace BoulderTime.Application.Abstractions;

/// <summary>
/// Hands a person's climbing activity to the background that records their active days and awards their badges
/// (ADR-069). Queueing never fails and never waits: saving a send must not depend on badges, and a badge found late is
/// far less bad than a send not saved.
/// </summary>
public interface IBadgeQueue
{
    void Enqueue(Guid userId, DateTimeOffset at);
}
