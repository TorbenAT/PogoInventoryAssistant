using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.ImagePretest.Services;

public static class ImagePretestJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false) =>
        new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
