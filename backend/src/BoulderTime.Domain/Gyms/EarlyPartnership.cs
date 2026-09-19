namespace BoulderTime.Domain.Gyms;

/// <summary>
/// A gym in the early-adopter programme. Unlike the founding gym this is a commercial relationship that several gyms
/// can hold, so it is a period with a start, an optional end and a note — not a flag. Ended partnerships are kept:
/// knowing a gym was an early partner last season is worth more than a boolean that silently flips back.
/// Billing is deliberately out of scope here.
/// </summary>
public class EarlyPartnership
{
    public const int NoteMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    /// <summary>Null while the partnership is running.</summary>
    public DateTimeOffset? EndedAt { get; private set; }
    public string? Note { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private EarlyPartnership() { }

    public static EarlyPartnership Start(Guid gymId, Guid grantedBy, DateTimeOffset startedAt, string? note, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), GymId = gymId, GrantedByUserId = grantedBy, StartedAt = startedAt,
        Note = Clean(note), CreatedAt = now,
    };

    public bool IsActiveAt(DateTimeOffset moment) => StartedAt <= moment && (EndedAt is null || EndedAt > moment);

    public void End(DateTimeOffset endedAt)
    {
        if (EndedAt is not null) throw new InvalidOperationException("This partnership already ended.");
        EndedAt = endedAt < StartedAt ? StartedAt : endedAt;
    }

    public void Edit(DateTimeOffset startedAt, DateTimeOffset? endedAt, string? note)
    {
        if (endedAt is { } end && end < startedAt) throw new ArgumentException("A partnership can't end before it starts.", nameof(endedAt));
        StartedAt = startedAt;
        EndedAt = endedAt;
        Note = Clean(note);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
