using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.Appraisal.Services;

public static class AppraisalJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented) =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
