namespace BoulderTime.Domain.Boulders;

/// <summary>
/// The PHYSICAL colour of the holds that make up a boulder. Completely separate from any grade,
/// including colour-based grades: a boulder can be graded "Yellow" and set on blue holds.
/// Stored by name, so values are only ever added at the end and never renamed.
/// </summary>
public enum HoldColor
{
    Red, Orange, Yellow, Green, Blue, Purple, Pink, White, Black, Grey, Brown, Mixed,
    /// <summary>Light blue ("azzurro"), told apart from Blue on the wall. Apps before 1.1 see it as Blue.</summary>
    LightBlue,
    /// <summary>Wooden holds and volumes. Apps before 1.1 see it as White.</summary>
    Wood,
}

/// <summary>
/// What the hold colours mean to apps that came before some of them existed (ADR-066). Apps from 1.1 say they know
/// every colour; older ones are shown, and may filter by, the closest colour they know, and saving a boulder from one
/// never turns a colour they can't see into the one they were shown.
/// </summary>
public static class HoldColors
{
    /// <summary>The colour an app that knows only the first twelve shows instead.</summary>
    public static HoldColor Legacy(HoldColor color) => color switch
    {
        HoldColor.LightBlue => HoldColor.Blue,
        HoldColor.Wood => HoldColor.White,
        _ => color,
    };

    /// <summary>The colours an older app means when it filters by one: what it sees as that colour.</summary>
    public static IReadOnlyList<HoldColor> LegacyMatches(HoldColor color) => color switch
    {
        HoldColor.Blue => [HoldColor.Blue, HoldColor.LightBlue],
        HoldColor.White => [HoldColor.White, HoldColor.Wood],
        _ => [color],
    };

    /// <summary>
    /// The colour to keep when an older app saves a boulder: the one it sent, unless that is only how it saw the stored
    /// one (a light blue boulder it showed as blue stays light blue, a wood one it showed as white stays wood).
    /// </summary>
    public static HoldColor KeepOnLegacySave(HoldColor stored, HoldColor sent) =>
        sent != stored && Legacy(stored) == sent ? stored : sent;

    /// <summary>The colour's name in the API: "LIGHT_BLUE", "WOOD", "BLUE".</summary>
    public static string ApiName(HoldColor color) => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(color.ToString());

    /// <summary>Reads a colour as the API writes it ("LIGHT_BLUE") or by its own name ("LightBlue"), whatever the case.</summary>
    public static bool TryParse(string? value, out HoldColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        foreach (var c in Enum.GetValues<HoldColor>())
        {
            if (string.Equals(ApiName(c), text, StringComparison.OrdinalIgnoreCase) || string.Equals(c.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                color = c;
                return true;
            }
        }
        return false;
    }

    /// <summary>A plain English name for texts written once for everyone ("Light blue holds").</summary>
    public static string EnglishName(HoldColor color) => color switch
    {
        HoldColor.LightBlue => "Light blue",
        _ => color.ToString(),
    };
}
