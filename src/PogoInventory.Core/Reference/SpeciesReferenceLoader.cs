using System.Text.Json;
using System.Text.Json.Serialization;

namespace PogoInventory.Core.Reference;

/// <summary>
/// Loads and validates a versioned species reference JSON file
/// (see <c>data/reference/species-reference.json</c>).
/// </summary>
public static class SpeciesReferenceLoader
{
    public static SpeciesReferenceData LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Species reference file not found: {path}",
                path);
        }

        var json = File.ReadAllText(path);
        return LoadFromJson(json, path);
    }

    public static SpeciesReferenceData LoadFromJson(string json, string? sourceDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        SpeciesReferenceDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<SpeciesReferenceDocument>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Species reference file '{sourceDescription ?? "<in-memory>"}' is not valid JSON: {exception.Message}",
                exception);
        }

        if (document is null)
        {
            throw new InvalidOperationException(
                $"Species reference file '{sourceDescription ?? "<in-memory>"}' deserialised to null.");
        }

        return Validate(document, sourceDescription);
    }

    private static SpeciesReferenceData Validate(SpeciesReferenceDocument document, string? sourceDescription)
    {
        var label = sourceDescription ?? "<in-memory>";

        if (string.IsNullOrWhiteSpace(document.Version))
        {
            throw new InvalidOperationException(
                $"Species reference file '{label}' is missing a non-empty 'version' field.");
        }

        if (string.IsNullOrWhiteSpace(document.Source))
        {
            throw new InvalidOperationException(
                $"Species reference file '{label}' is missing a non-empty 'source' field.");
        }

        if (document.Species is null || document.Species.Count == 0)
        {
            throw new InvalidOperationException(
                $"Species reference file '{label}' must contain at least one species entry.");
        }

        if (document.CpRange is null)
        {
            throw new InvalidOperationException(
                $"Species reference file '{label}' is missing a 'cpRange' field.");
        }

        if (document.CpRange.Min < 0 || document.CpRange.Max <= document.CpRange.Min)
        {
            throw new InvalidOperationException(
                $"Species reference file '{label}' has an invalid cpRange ({document.CpRange.Min}..{document.CpRange.Max}).");
        }

        var entries = new List<SpeciesReferenceEntry>(document.Species.Count);
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in document.Species)
        {
            if (string.IsNullOrWhiteSpace(raw.Name))
            {
                throw new InvalidOperationException(
                    $"Species reference file '{label}' contains an entry with an empty name.");
            }

            if (raw.DexNumber <= 0)
            {
                throw new InvalidOperationException(
                    $"Species reference file '{label}' entry '{raw.Name}' has an invalid dexNumber ({raw.DexNumber}).");
            }

            var normalizedName = SpeciesReferenceData.Normalize(raw.Name);
            if (!seenNames.Add(normalizedName))
            {
                throw new InvalidOperationException(
                    $"Species reference file '{label}' contains a duplicate species name: '{raw.Name}'.");
            }

            entries.Add(new SpeciesReferenceEntry
            {
                Name = raw.Name,
                DexNumber = raw.DexNumber,
                Classification = raw.Classification
            });
        }

        return new SpeciesReferenceData(
            document.Version,
            document.Source,
            new SpeciesCpRange { Min = document.CpRange.Min, Max = document.CpRange.Max },
            entries);
    }

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class SpeciesReferenceDocument
    {
        public string? Version { get; set; }
        public string? Source { get; set; }
        public List<SpeciesReferenceEntryDocument>? Species { get; set; }
        public SpeciesCpRangeDocument? CpRange { get; set; }
    }

    private sealed class SpeciesReferenceEntryDocument
    {
        public string? Name { get; set; }
        public int DexNumber { get; set; }
        public SpeciesClassification Classification { get; set; }
    }

    private sealed class SpeciesCpRangeDocument
    {
        public int Min { get; set; }
        public int Max { get; set; }
    }
}
