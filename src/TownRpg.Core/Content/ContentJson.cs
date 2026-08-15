using System.Text.Json;
using System.Text.Json.Serialization;

namespace TownRpg.Core.Content;

public static class ContentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T Deserialize<T>(string json) where T : class =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidDataException($"Content document for {typeof(T).Name} is empty or malformed.");
}
