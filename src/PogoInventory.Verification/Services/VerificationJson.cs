using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.Verification.Services;

public static class VerificationJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false) =>
        new()
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
}
