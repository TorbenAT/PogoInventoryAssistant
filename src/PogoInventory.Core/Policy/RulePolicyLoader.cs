using System.Text.Json;

namespace PogoInventory.Core.Policy;

/// <summary>
/// Loads a <see cref="RulePolicy"/> from a JSON file on disk, and can write the current
/// built-in defaults back out as JSON. The JSON schema mirrors the <see cref="RulePolicy"/>
/// and <see cref="PvpHeuristicPolicy"/> record properties exactly, plus a required
/// top-level <c>schemaVersion</c> field. Loading fails closed: any unrecognised field at
/// either the top level or inside <c>pvpHeuristic</c> is rejected rather than silently
/// ignored, so a typo or a future field never gets treated as "not configured" (which
/// would otherwise be misread as an intentional false/default value).
/// </summary>
public static class RulePolicyLoader
{
    public const string CurrentSchemaVersion = "1";

    private static readonly HashSet<string> TopLevelFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "schemaVersion",
        "version",
        "oldPokemonCutoff",
        "minimumOrdinaryCopiesPerSpeciesForm",
        "deleteRequiresExactIdentity",
        "deleteRequiresStrictlyBetterDuplicate",
        "tradeTagNames",
        "tradeNicknameFragments",
        "pvpHeuristic"
    };

    private static readonly HashSet<string> PvpHeuristicFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "enabled",
        "maximumAttackIv",
        "minimumDefenseIv",
        "minimumHpIv",
        "preserveBestCandidatePerGroup"
    };

    public static RulePolicy LoadFromFile(string jsonPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);

        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException($"Rule policy file not found: {jsonPath}", jsonPath);
        }

        var json = File.ReadAllText(jsonPath);
        return LoadFromJson(json, jsonPath);
    }

    public static RulePolicy LoadFromJson(string json, string? sourceDescription = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var label = sourceDescription ?? "<in-memory>";

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' must contain a JSON object.");
        }

        RejectUnknownFields(root, TopLevelFields, label, "top level");

        if (!TryGetString(root, "schemaVersion", out var schemaVersion) || string.IsNullOrWhiteSpace(schemaVersion))
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing a non-empty 'schemaVersion' field.");
        }

        if (!string.Equals(schemaVersion, CurrentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' declares unsupported schemaVersion '{schemaVersion}'. Expected '{CurrentSchemaVersion}'.");
        }

        if (!TryGetString(root, "version", out var version) || string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing a non-empty 'version' field.");
        }

        if (!root.TryGetProperty("oldPokemonCutoff", out var cutoffElement) ||
            !cutoffElement.TryGetDateOnly(out var oldPokemonCutoff))
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing or has an invalid 'oldPokemonCutoff' date.");
        }

        if (!root.TryGetProperty("minimumOrdinaryCopiesPerSpeciesForm", out var minCopiesElement) ||
            minCopiesElement.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing or has an invalid 'minimumOrdinaryCopiesPerSpeciesForm'.");
        }

        var minimumOrdinaryCopies = minCopiesElement.GetInt32();

        var deleteRequiresExactIdentity = RequireBool(root, "deleteRequiresExactIdentity", label);
        var deleteRequiresStrictlyBetterDuplicate = RequireBool(root, "deleteRequiresStrictlyBetterDuplicate", label);
        var tradeTagNames = RequireStringArray(root, "tradeTagNames", label);
        var tradeNicknameFragments = RequireStringArray(root, "tradeNicknameFragments", label);

        if (!root.TryGetProperty("pvpHeuristic", out var pvpElement) ||
            pvpElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing a 'pvpHeuristic' object.");
        }

        RejectUnknownFields(pvpElement, PvpHeuristicFields, label, "pvpHeuristic");

        var pvpHeuristic = new PvpHeuristicPolicy
        {
            Enabled = RequireBool(pvpElement, "enabled", label),
            MaximumAttackIv = RequireInt(pvpElement, "maximumAttackIv", label),
            MinimumDefenseIv = RequireInt(pvpElement, "minimumDefenseIv", label),
            MinimumHpIv = RequireInt(pvpElement, "minimumHpIv", label),
            PreserveBestCandidatePerGroup = RequireBool(pvpElement, "preserveBestCandidatePerGroup", label)
        };

        return new RulePolicy
        {
            Version = version,
            OldPokemonCutoff = oldPokemonCutoff,
            MinimumOrdinaryCopiesPerSpeciesForm = minimumOrdinaryCopies,
            DeleteRequiresExactIdentity = deleteRequiresExactIdentity,
            DeleteRequiresStrictlyBetterDuplicate = deleteRequiresStrictlyBetterDuplicate,
            TradeTagNames = tradeTagNames,
            TradeNicknameFragments = tradeNicknameFragments,
            PvpHeuristic = pvpHeuristic
        };
    }

    /// <summary>
    /// Writes the built-in default <see cref="RulePolicy"/> (<c>new RulePolicy()</c>) to
    /// <paramref name="path"/> as JSON matching the schema this loader accepts.
    /// </summary>
    public static void WriteDefault(string path) => WritePolicy(path, new RulePolicy());

    public static void WritePolicy(string path, RulePolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(policy);

        var document = new
        {
            schemaVersion = CurrentSchemaVersion,
            version = policy.Version,
            oldPokemonCutoff = policy.OldPokemonCutoff.ToString("yyyy-MM-dd"),
            minimumOrdinaryCopiesPerSpeciesForm = policy.MinimumOrdinaryCopiesPerSpeciesForm,
            deleteRequiresExactIdentity = policy.DeleteRequiresExactIdentity,
            deleteRequiresStrictlyBetterDuplicate = policy.DeleteRequiresStrictlyBetterDuplicate,
            tradeTagNames = policy.TradeTagNames,
            tradeNicknameFragments = policy.TradeNicknameFragments,
            pvpHeuristic = new
            {
                enabled = policy.PvpHeuristic.Enabled,
                maximumAttackIv = policy.PvpHeuristic.MaximumAttackIv,
                minimumDefenseIv = policy.PvpHeuristic.MinimumDefenseIv,
                minimumHpIv = policy.PvpHeuristic.MinimumHpIv,
                preserveBestCandidatePerGroup = policy.PvpHeuristic.PreserveBestCandidatePerGroup
            }
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void RejectUnknownFields(
        JsonElement element,
        HashSet<string> allowedFields,
        string label,
        string sectionDescription)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowedFields.Contains(property.Name))
            {
                throw new InvalidOperationException(
                    $"Rule policy file '{label}' has an unrecognised field '{property.Name}' in {sectionDescription}.");
            }
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool RequireBool(JsonElement element, string propertyName, string label)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing or has an invalid boolean '{propertyName}'.");
        }

        return property.GetBoolean();
    }

    private static int RequireInt(JsonElement element, string propertyName, string label)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing or has an invalid integer '{propertyName}'.");
        }

        return property.GetInt32();
    }

    private static string[] RequireStringArray(JsonElement element, string propertyName, string label)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                $"Rule policy file '{label}' is missing or has an invalid array '{propertyName}'.");
        }

        var items = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } text)
            {
                throw new InvalidOperationException(
                    $"Rule policy file '{label}' has a non-string entry in '{propertyName}'.");
            }

            items.Add(text);
        }

        return items.ToArray();
    }
}

internal static class JsonElementDateExtensions
{
    public static bool TryGetDateOnly(this JsonElement element, out DateOnly value)
    {
        if (element.ValueKind == JsonValueKind.String &&
            DateOnly.TryParse(
                element.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out value))
        {
            return true;
        }

        value = default;
        return false;
    }
}
