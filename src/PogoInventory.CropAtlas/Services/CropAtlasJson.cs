using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.CropAtlas.Services;

public static class CropAtlasJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented) =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
