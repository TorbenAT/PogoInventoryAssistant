using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public enum CleanupProofObservationStatus
{
    Complete,
    Partial,
    Unresolved
}

public enum AppraisalCarouselAdvanceResult
{
    SUCCESS_CHANGED_POKEMON,
    NO_EFFECT_OR_FILTER_END,
    TRANSIENT_UNKNOWN_RECOVERED,
    UNKNOWN_STOP
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
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<string> EvidencePaths { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FailureReasons { get; init; } = Array.Empty<string>();
}
