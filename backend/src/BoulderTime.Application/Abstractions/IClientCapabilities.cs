namespace BoulderTime.Application.Abstractions;

/// <summary>
/// What the app making the request says it understands. Apps already in the stores send nothing, and get answers
/// they know how to show (ADR-066). Outside a request (a background job) everything is understood.
/// </summary>
public interface IClientCapabilities
{
    /// <summary>The app knows the hold colours added in 1.1 (light blue, wood).</summary>
    bool KnowsAllHoldColors { get; }
}

public static class ClientCapabilities
{
    /// <summary>The request header in which an app lists what it understands, separated by commas.</summary>
    public const string Header = "X-BoulderTime-Capabilities";

    /// <summary>Every hold colour, light blue and wood included.</summary>
    public const string HoldColors2 = "hold-colors-2";
}
