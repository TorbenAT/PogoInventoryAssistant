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

    /// <summary>
    /// Raw bytes of the stable frame this identity capture confirmed, set only
    /// for the appraisal-carousel identity capture (<c>CaptureCleanupAppraisalIdentityAsync</c>,
    /// ordinal&gt;1) when it actually confirmed a stable AppraisalBars frame.
    /// Lets a caller that just established this stable frame (e.g. the
    /// cleanup-proof per-item loop) hand it into
    /// <c>CaptureCurrentCleanupAppraisalAsync</c> as the already-confirmed IV
    /// source, skipping a redundant re-capture of the identical on-screen
    /// state one step later. Null whenever no stable frame was confirmed (the
    /// consumer must then fail closed to its own full capture window).
    /// </summary>
    public byte[]? StableScreenshot { get; init; }
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

    /// <summary>
    /// Per-evidence-frame IV bar measurements, one entry per analyzed appraisal
    /// evidence screenshot. Populated independently of <see cref="AttackIv"/> /
    /// <see cref="DefenseIv"/> / <see cref="HpIv"/> (which come from a single
    /// "stable" frame) so that a caller (e.g. the cleanup-proof runner) can
    /// require multi-frame agreement before trusting IVs as Complete, without
    /// depending on <c>AppraisalAnalyzer</c>'s own Calcy-verified-profile gate.
    /// </summary>
    public IReadOnlyList<AppraisalFrameIv> Frames { get; init; } = Array.Empty<AppraisalFrameIv>();

    /// <summary>
    /// SHA-256 fingerprint of the stable AppraisalBars frame this capture was
    /// analyzed from, set only when a stable frame was actually confirmed
    /// (never for an "AppraisalBarsNotStable"-unavailable result). Lets a
    /// caller that just established this stable frame (e.g. the cleanup-proof
    /// per-item loop) hand it back into <c>AdvanceToNextPokemonInAppraisalAsync</c>
    /// as the pre-swipe reference, skipping a redundant re-capture of the same
    /// state one step later.
    /// </summary>
    public string? StableFingerprintSha256 { get; init; }
}

/// <summary>
/// IV bar measurement for a single analyzed appraisal evidence frame.
/// <see cref="BarsConfident"/> mirrors the "all three bars measured with
/// track detected, an estimated IV and confidence at or above the visual
/// profile's <c>CompleteBarConfidenceMinimum</c>" check that
/// <c>AppraisalAnalyzer</c> itself uses to gate a Complete result, so a
/// consuming caller can require that per-frame quality bar without needing
/// the visual profile itself.
/// </summary>
public sealed record AppraisalFrameIv
{
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public required bool BarsConfident { get; init; }
}
