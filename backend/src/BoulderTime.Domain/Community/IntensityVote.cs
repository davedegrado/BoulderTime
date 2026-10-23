using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Community;

/// <summary>
/// A climber's opinion of how intense a boulder is, beside the grade. At most one per (boulder, user). Like the
/// community grade, it never changes the intensity staff set.
/// </summary>
public class IntensityVote : IAuditable
{
    public Guid Id { get; private set; }
    public Guid BoulderId { get; private set; }
    public Guid UserId { get; private set; }
    public BoulderIntensity Intensity { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private IntensityVote() { }

    public static IntensityVote Create(Guid boulderId, Guid userId, BoulderIntensity intensity) =>
        new() { Id = Guid.NewGuid(), BoulderId = boulderId, UserId = userId, Intensity = intensity };

    public void Change(BoulderIntensity intensity) => Intensity = intensity;
}
