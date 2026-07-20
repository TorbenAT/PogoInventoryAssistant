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
    Stopped
}

public sealed record VerifiedSequenceRequest
{
    public required string Query { get; init; }
    public required int ItemLimit { get; init; }
    public string? ClassificationTag { get; init; }
    public bool ApplyTags { get; init; }
    public required string OutputDirectory { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Query)) throw new ArgumentException("Query is required.");
        if (ItemLimit is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(ItemLimit));
        if (ApplyTags && string.IsNullOrWhiteSpace(ClassificationTag))
            throw new ArgumentException("A classification tag is required when ApplyTags is true.");
        if (ApplyTags && ClassificationTag == "AI-Delete")
            throw new ArgumentException("AI-Delete is classification evidence only; it is never auto-applied.");
        if (ClassificationTag is not null && ClassificationTag is not ("AI-Keep" or "AI-Review" or "AI-Delete"))
            throw new ArgumentException("ClassificationTag is not allow-listed.");
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputDirectory);
    }
}

public sealed record VerifiedSequenceItem
{
    public required int Ordinal { get; init; }
    public required string InstanceId { get; init; }
    public required string StableFingerprintSha256 { get; init; }
    public required IReadOnlyList<string> EvidenceHashes { get; init; }
    public required VerifiedSequenceState State { get; init; }
    public required string Query { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? AppraisalStatus { get; init; }
    public string? Detail { get; init; }
}

public sealed record VerifiedSequenceCheckpoint
{
    public const string CurrentSchemaVersion = "1.0";
    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ScanRunId { get; init; }
    public required string Query { get; init; }
    public required int ItemLimit { get; init; }
    public required bool ApplyTags { get; init; }
    public string? ClassificationTag { get; init; }
    public VerifiedSequenceState State { get; init; } = VerifiedSequenceState.Inventory;
    public IReadOnlyList<VerifiedSequenceItem> Items { get; init; } = Array.Empty<VerifiedSequenceItem>();
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? NextAction { get; init; }
}

public sealed record VerifiedSequenceResult
{
    public required VerifiedSequenceCheckpoint Checkpoint { get; init; }
    public required string CheckpointPath { get; init; }
}
