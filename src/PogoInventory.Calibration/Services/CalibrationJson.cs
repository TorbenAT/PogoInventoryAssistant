using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.Calibration.Services;

public static class CalibrationJson
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false) =>
        new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented,
            Converters = { new JsonStringEnumConverter() }
        };
}
