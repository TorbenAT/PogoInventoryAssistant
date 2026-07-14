using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.Automation.Services;

public static class AutomationJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = true) =>
        new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
