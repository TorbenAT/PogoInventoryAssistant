using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public enum CleanupProofObservationStatus
{
    Complete,
    Partial,
    Unresolved
}

public sealed record CleanupProofIdentityCapture
{
    public required PokemonIdentityConsensus Consensus { get; init; }
    public required CleanupProofObservationStatus Status { get; init; }
    public required IReadOnlyList<string> ScreenshotPaths { get; init; }
    public required IReadOnlyList<string> ScreenshotHashes { get; init; }
    public IReadOnlyList<string> FailureReasons { get; init; } = Array.Empty<string>();
}

public sealed record CleanupProofAppraisalCapture
{
    public required string Status { get; init; }
    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();
}
