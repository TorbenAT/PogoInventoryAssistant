using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public enum VerifiedSequenceState
{
    Inventory,
    PokemonDetails,
    PokemonMenu,
    Appraisal,
    Partial,
    Unknown,
    Stopped,
    ControlledStopped,
    TerminalUnknown,
    TerminalFailure,
    NoEffectOrEndOfFilter,
    Completed
}

public sealed record VerifiedSequenceRequest
{
    public required string Query { get; init; }
    public required int ItemLimit { get; init; }
    public string? ClassificationTag { get; init; }
    public string IndexTag { get; init; } = "AI-Indexed";
    public bool ApplyIndexTag { get; init; }
    public bool ApplyClassificationTag { get; init; }
    public bool Resume { get; init; } = true;
    public int? ControlledStopAfter { get; init; }
    public required string OutputDirectory { get; init; }

    // Kept as a source-compatible alias for older callers. New code must use
    // the separate index/classification switches above.
    public bool ApplyTags
    {
        get => ApplyClassificationTag;
        init => ApplyClassificationTag = value;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Query)) throw new ArgumentException("Query is required.");
        if (ItemLimit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(ItemLimit));
        if (ApplyIndexTag && IndexTag != "AI-Indexed")
            throw new ArgumentException("Only AI-Indexed may be applied as an index tag.");
        if (ApplyClassificationTag && string.IsNullOrWhiteSpace(ClassificationTag))
            throw new ArgumentException("A classification tag is required when ApplyClassificationTag is true.");
        if (ApplyClassificationTag && ClassificationTag == "AI-Delete")
            throw new ArgumentException("AI-Delete is classification evidence only; it is never auto-applied.");
        if (ClassificationTag is not null && ClassificationTag is not ("AI-Keep" or "AI-Review" or "AI-Delete"))
            throw new ArgumentException("ClassificationTag is not allow-listed.");
        if (ControlledStopAfter is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(ControlledStopAfter));
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputDirectory);
    }
}

public sealed record VerifiedTagObservation
{
    public required int TagCount { get; init; }
    public IReadOnlyList<string> KnownTagNames { get; init; } = Array.Empty<string>();
    public required bool NamesComplete { get; init; }
    public required NormalizedRegion? Section { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}

public sealed record VerifiedSequenceItem
{
    public required int Ordinal { get; init; }
    public required string InstanceId { get; init; }
    public required string StableFingerprintSha256 { get; init; }
    public required IReadOnlyList<string> EvidenceHashes { get; init; }
    public required VerifiedSequenceState State { get; init; }
    public required string Query { get; init; }
    public required PokemonIdentityObservationStatus IdentityStatus { get; init; }
    public VerifiedTagObservation TagObservation { get; init; } = new()
    {
        TagCount = 0, NamesComplete = false, Section = null
    };
    public string? AppraisalStatus { get; init; }
    public string? Detail { get; init; }
}

public sealed record VerifiedSequenceCheckpoint
{
    public const string CurrentSchemaVersion = "2.0";
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ScanRunId { get; init; }
    public required string Query { get; init; }
    public required int ItemLimit { get; init; }
    public required bool ApplyIndexTag { get; init; }
    public required string IndexTag { get; init; }
    public required bool ApplyClassificationTag { get; init; }
    public string? ClassificationTag { get; init; }
    public int CurrentOrdinal { get; init; }
    public int LastCompletedOrdinal { get; init; }
    public string? PreviousStableFingerprint { get; init; }
    public string? CurrentStableFingerprint { get; init; }
    public VerifiedSequenceState LastVerifiedState { get; init; } = VerifiedSequenceState.Inventory;
    public PokemonIdentityObservationStatus? IdentityStatus { get; init; }
    public IReadOnlyList<string> EvidenceHashes { get; init; } = Array.Empty<string>();
    public VerifiedTagObservation? TagObservation { get; init; }
    public VerifiedSequenceState State { get; init; } = VerifiedSequenceState.Inventory;
    public IReadOnlyList<VerifiedSequenceItem> Items { get; init; } = Array.Empty<VerifiedSequenceItem>();
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? NextAction { get; init; }

    // Compatibility for checkpoints written by the pre-cursor implementation.
    public bool ApplyTags => ApplyClassificationTag;
}

public sealed record VerifiedSequenceResult
{
    public required VerifiedSequenceCheckpoint Checkpoint { get; init; }
    public required string CheckpointPath { get; init; }
}
