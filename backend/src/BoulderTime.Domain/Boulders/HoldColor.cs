namespace BoulderTime.Domain.Boulders;

/// <summary>
/// The PHYSICAL colour of the holds that make up a boulder. Completely separate from any grade,
/// including colour-based grades: a boulder can be graded "Yellow" and set on blue holds.
/// </summary>
public enum HoldColor
{
    Red, Orange, Yellow, Green, Blue, Purple, Pink, White, Black, Grey, Brown, Mixed,
}
