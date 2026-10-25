namespace BoulderTime.Domain.Climbing;

/// <summary>
/// A boulder a climber chose to work on (ADR-067). Only the climber marks or unmarks it: trying a boulder never makes it
/// a project. Kept apart from the attempts, so a project can be marked before the first try and survives an attempt
/// count set back to zero. Once the boulder is completed the project is closed and counts among the sends; the mark
/// stays, so undoing the send makes it a project again. It outlives the boulder's removal like the rest of the history.
/// </summary>
public class BoulderProject
{
    public Guid UserId { get; private set; }
    public Guid BoulderId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private BoulderProject() { }

    public static BoulderProject Mark(Guid userId, Guid boulderId, DateTimeOffset now) =>
        new() { UserId = userId, BoulderId = boulderId, CreatedAt = now };
}
