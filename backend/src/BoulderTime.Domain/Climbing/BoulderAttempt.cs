using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Climbing;

/// <summary>
/// A climber's current tracking of one boulder: how many attempts, and whether it's completed.
/// Exactly one row per (user, boulder). Flash/redpoint distinctions are intentionally not modelled yet.
/// The row outlives the boulder's removal, so climbing history is permanent.
/// </summary>
public class BoulderAttempt : IAuditable
{
    public const int MaxAttempts = 999;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BoulderId { get; private set; }
    public int Attempts { get; private set; }
    public bool Completed { get; private set; }
    /// <summary>When the boulder was first marked completed; the date shown in history.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private BoulderAttempt() { }

    public static BoulderAttempt Start(Guid userId, Guid boulderId) => new() { Id = Guid.NewGuid(), UserId = userId, BoulderId = boulderId };

    /// <summary>
    /// Applies the climber's latest state. Completing implies at least one attempt.
    /// Un-completing clears the completion date; re-completing sets a new one.
    /// </summary>
    public void Record(int attempts, bool completed, DateTimeOffset now)
    {
        if (attempts is < 0 or > MaxAttempts) throw new ArgumentOutOfRangeException(nameof(attempts));
        Attempts = completed ? Math.Max(1, attempts) : attempts;
        if (completed && !Completed) CompletedAt = now;
        if (!completed) CompletedAt = null;
        Completed = completed;
    }

    /// <summary>A row with no attempts and no completion carries no information and should be deleted.</summary>
    public bool IsEmpty => Attempts == 0 && !Completed;
}
