namespace PogoInventory.Core.Models;

public sealed record PokemonVariantIdentity
{
    public const string CurrentSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public int? SpeciesId { get; init; }
    public string? SpeciesName { get; init; }
    public string? FormId { get; init; }
    public string? FormName { get; init; }
    public string? CostumeId { get; init; }
    public string? CostumeName { get; init; }
    public string? BackgroundId { get; init; }
    public string? BackgroundName { get; init; }
    public string? GenderVariant { get; init; }
    public bool? IsShiny { get; init; }
    public string? ShadowState { get; init; }
    public string? LuckyState { get; init; }
    public string? DynamaxState { get; init; }
    public string? SpecialVariantId { get; init; }
    public string? SpecialVariantName { get; init; }
    public IdentityConfidence VariantIdentityConfidence { get; init; } =
        IdentityConfidence.Unknown;
    public IReadOnlyCollection<string> EvidenceReferences { get; init; } =
        Array.Empty<string>();

    public IReadOnlyList<string> MissingVariantFields
    {
        get
        {
            var missing = new List<string>();
            AddMissing(missing, SpeciesId, nameof(SpeciesId));
            AddMissing(missing, SpeciesName, nameof(SpeciesName));
            AddMissing(missing, FormId, nameof(FormId));
            AddMissing(missing, FormName, nameof(FormName));
            AddMissing(missing, CostumeId, nameof(CostumeId));
            AddMissing(missing, CostumeName, nameof(CostumeName));
            AddMissing(missing, BackgroundId, nameof(BackgroundId));
            AddMissing(missing, BackgroundName, nameof(BackgroundName));
            AddMissing(missing, GenderVariant, nameof(GenderVariant));
            AddMissing(missing, IsShiny, nameof(IsShiny));
            AddMissing(missing, ShadowState, nameof(ShadowState));
            AddMissing(missing, LuckyState, nameof(LuckyState));
            AddMissing(missing, DynamaxState, nameof(DynamaxState));
            AddMissing(missing, SpecialVariantId, nameof(SpecialVariantId));
            AddMissing(missing, SpecialVariantName, nameof(SpecialVariantName));
            return missing;
        }
    }

    public string? VariantKey =>
        VariantIdentityConfidence == IdentityConfidence.Exact &&
        MissingVariantFields.Count == 0
            ? string.Join('|',
                SpeciesId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Normalize(FormId),
                Normalize(CostumeId),
                Normalize(BackgroundId),
                Normalize(GenderVariant),
                IsShiny!.Value ? "shiny" : "not-shiny",
                Normalize(ShadowState),
                Normalize(DynamaxState),
                Normalize(SpecialVariantId))
            : null;

    private static void AddMissing<T>(ICollection<string> missing, T? value, string name)
        where T : struct
    {
        if (value is null)
        {
            missing.Add(name);
        }
    }

    private static void AddMissing(ICollection<string> missing, string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(name);
        }
    }

    private static string Normalize(string? value) =>
        value!.Trim().ToLowerInvariant();
}
