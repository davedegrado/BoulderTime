using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Climbing;

/// <summary>A climber's 1–5 star rating of a boulder. One per (user, boulder), changeable. Requires an attempt.</summary>
public class BoulderRating : IAuditable
{
    public const int Min = 1;
    public const int Max = 5;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BoulderId { get; private set; }
    public int Rating { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private BoulderRating() { }

    public static BoulderRating Create(Guid userId, Guid boulderId, int rating)
    {
        var r = new BoulderRating { Id = Guid.NewGuid(), UserId = userId, BoulderId = boulderId };
        r.Change(rating);
        return r;
    }

    public void Change(int rating)
    {
        if (rating is < Min or > Max) throw new ArgumentOutOfRangeException(nameof(rating));
        Rating = rating;
    }
}
