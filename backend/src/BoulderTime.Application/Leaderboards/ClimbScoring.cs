using BoulderTime.Domain.Grading;

namespace BoulderTime.Application.Leaderboards;

/// <summary>A graded send as seen by the scoring rule: where the grade sits on its scale.</summary>
public readonly record struct GradedSend(int Rank, int ScaleSize);

/// <summary>
/// The rule that turns a completed boulder into leaderboard points. Isolated behind an interface so the formula can
/// change (or become per-gym) without touching queries, API or UI.
/// </summary>
public interface IClimbScoring
{
    int PointsFor(GradedSend send);

    /// <summary>Human-readable explanation shown next to the leaderboard.</summary>
    string Describe(string systemName);
}

/// <summary>
/// Default rule: the easiest grade of the scale is worth 10 points, the hardest 100, evenly spaced in between.
/// Normalising by scale size keeps a 6-colour scale and a 23-step Fontainebleau scale on the same 10–100 range.
/// Attempts don't affect points (flash/redpoint aren't tracked yet).
/// </summary>
public sealed class RankBasedScoring : IClimbScoring
{
    public const int MinPoints = 10;
    public const int MaxPoints = 100;

    public int PointsFor(GradedSend send)
    {
        if (send.ScaleSize <= 1) return MaxPoints;
        var position = Math.Clamp(send.Rank, 0, send.ScaleSize - 1);
        return (int)Math.Round(MinPoints + (MaxPoints - MinPoints) * position / (double)(send.ScaleSize - 1), MidpointRounding.AwayFromZero);
    }

    public string Describe(string systemName) =>
        $"Each completed boulder scores by its {systemName} grade: the easiest grade is worth {MinPoints} points and the hardest {MaxPoints}, evenly spaced in between.";
}
