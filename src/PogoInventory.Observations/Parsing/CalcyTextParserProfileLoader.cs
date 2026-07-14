using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Parsing;

public static class CalcyTextParserProfileLoader
{
    public static JsonSerializerOptions CreateJsonOptions(bool writeIndented = false) =>
        new()
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

    public static async Task<CalcyTextParserProfile> LoadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var profile = JsonSerializer.Deserialize<CalcyTextParserProfile>(
            await File.ReadAllTextAsync(path, cancellationToken),
            CreateJsonOptions()) ?? throw new InvalidOperationException(
                $"Calcy parser profile '{path}' contained no data.");
        profile.Validate();
        return profile;
    }
}
