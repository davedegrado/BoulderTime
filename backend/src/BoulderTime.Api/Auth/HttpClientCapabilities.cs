using BoulderTime.Application.Abstractions;

namespace BoulderTime.Api.Auth;

/// <summary>Reads what the app understands from the request's capabilities header (ADR-066).</summary>
public sealed class HttpClientCapabilities(IHttpContextAccessor accessor) : IClientCapabilities
{
    public bool KnowsAllHoldColors => Knows(accessor.HttpContext, ClientCapabilities.HoldColors2);

    /// <summary>Whether the request lists this capability. No request at all: everything is understood.</summary>
    public static bool Knows(HttpContext? http, string capability)
    {
        if (http is null) return true;
        foreach (var value in http.Request.Headers[ClientCapabilities.Header])
            foreach (var item in (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                if (string.Equals(item, capability, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
