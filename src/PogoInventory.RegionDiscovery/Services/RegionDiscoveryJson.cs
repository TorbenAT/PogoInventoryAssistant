using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.RegionDiscovery.Services;

public static class RegionDiscoveryJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented) =>
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
