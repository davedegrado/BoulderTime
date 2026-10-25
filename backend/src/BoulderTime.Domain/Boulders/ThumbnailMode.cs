namespace BoulderTime.Domain.Boulders;

/// <summary>
/// How staff made a boulder's card picture (ADR-068). The app makes the picture itself and uploads it; this only says
/// which choice it came from, so the editor can show it again. Stored by name; values are only ever added.
/// </summary>
public enum ThumbnailMode
{
    /// <summary>The photo covers the card, its centre kept.</summary>
    FillCenter,
    /// <summary>The photo covers the card, its top kept.</summary>
    FillTop,
    /// <summary>The photo covers the card, its bottom kept.</summary>
    FillBottom,
    /// <summary>The whole photo, the room left over filled with the same photo blurred.</summary>
    Fit,
    /// <summary>A part chosen by hand.</summary>
    Custom,
}
