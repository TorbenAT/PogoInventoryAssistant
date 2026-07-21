namespace PogoInventory.Application;

public sealed record CleanupProofComparativeSuggestion
{
    public required string LocalPokemonId { get; init; }
    public required string Species { get; init; }
    public required string Classification { get; init; }
    public string? ComparatorLocalPokemonId { get; init; }
    public int? Cp { get; init; }
    public int? TotalIv { get; init; }
    public int Ordinal { get; init; }
    public IReadOnlyList<string> MissingProtectionChecks { get; init; } = Array.Empty<string>();
    public string Label => Classification == "LIKELY_DELETE_SUGGESTION"
        ? "LIKELY_DELETE_SUGGESTION — REQUIRES HUMAN PROTECTION REVIEW"
        : Classification;
}
