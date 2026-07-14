using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.CropAtlas.Semantic.Services;

public static class SemanticEvidenceJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented) =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
