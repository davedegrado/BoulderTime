using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Boulders;

namespace BoulderTime.Api.Auth;

/// <summary>
/// Hold colours in the API, as SCREAMING_SNAKE names like every other enum ("BLUE", "LIGHT_BLUE"). An app that doesn't
/// say it knows the colours added in 1.1 is sent the closest one it knows instead (light blue as blue, wood as brown):
/// the store apps already installed would otherwise show an unknown colour as their first one, red (ADR-066). Reading
/// takes every colour from every app; numbers are refused, as for the other enums.
/// </summary>
public sealed class HoldColorJsonConverter(IHttpContextAccessor accessor) : JsonConverter<HoldColor>
{
    public override HoldColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && HoldColors.TryParse(reader.GetString(), out var color)) return color;
        throw new JsonException("Unknown hold colour.");
    }

    public override void Write(Utf8JsonWriter writer, HoldColor value, JsonSerializerOptions options)
    {
        var shown = HttpClientCapabilities.Knows(accessor.HttpContext, ClientCapabilities.HoldColors2) ? value : HoldColors.Legacy(value);
        writer.WriteStringValue(HoldColors.ApiName(shown));
    }
}
