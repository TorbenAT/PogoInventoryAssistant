using PogoInventory.Observations.Models;

namespace PogoInventory.Verification.Models;

public sealed record CalcyVerificationCaseResult
{
    public required string Id { get; init; }
    public required ExpectedPokemonObservation Expected { get; init; }
    public CalcyObservation? Observed { get; init; }
    public required CalcyVerificationOutcome Outcome { get; init; }
    public IReadOnlyList<string> Mismatches { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string> EvidenceSha256 { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
    public string? ErrorDetail { get; init; }
}
