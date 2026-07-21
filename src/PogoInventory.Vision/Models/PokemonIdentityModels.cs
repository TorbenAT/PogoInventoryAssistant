namespace PogoInventory.Vision.Models;

public enum PokemonIdentityObservationStatus
{
    Complete,
    Partial,
    Unavailable
}

public sealed record PokemonIdentityFingerprintProfile
{
    public NormalizedRegion HeaderRegion { get; init; } = new()
    {
        X = 0.24, Y = 0.42, Width = 0.52, Height = 0.045
    };
    public NormalizedRegion TagSearchRegion { get; init; } = new()
    {
        X = 0.08, Y = 0.44, Width = 0.84, Height = 0.21
    };
    public NormalizedRegion LowerAnchorSearchRegion { get; init; } = new()
    {
        X = 0.08, Y = 0.40, Width = 0.84, Height = 0.52
    };
    public NormalizedRegion LowerContentRegion { get; init; } = new()
    {
        X = 0.10, Y = 0.02, Width = 0.80, Height = 0.16
    };
    public int FingerprintWidth { get; init; } = 16;
    public int FingerprintHeight { get; init; } = 12;
    public double SameIdentitySimilarityThreshold { get; init; } = 0.965;

    public void Validate()
    {
        HeaderRegion.Validate(nameof(HeaderRegion));
        TagSearchRegion.Validate(nameof(TagSearchRegion));
        LowerAnchorSearchRegion.Validate(nameof(LowerAnchorSearchRegion));
        LowerContentRegion.Validate(nameof(LowerContentRegion));
        if (FingerprintWidth is < 4 or > 64 || FingerprintHeight is < 4 or > 64)
            throw new ArgumentOutOfRangeException(nameof(FingerprintWidth));
        if (!double.IsFinite(SameIdentitySimilarityThreshold) ||
            SameIdentitySimilarityThreshold is <= 0.8 or > 1)
            throw new ArgumentOutOfRangeException(nameof(SameIdentitySimilarityThreshold));
    }
}

public sealed record PokemonIdentityTagObservation
{
    public required int TagCount { get; init; }
    public required NormalizedRegion? Section { get; init; }
    public required bool IsSeparateFromIdentity { get; init; }
    public IReadOnlyList<string> TagNames { get; init; } = Array.Empty<string>();
}

public sealed record PokemonIdentityFingerprintObservation
{
    public required PokemonIdentityObservationStatus Status { get; init; }
    public required string EvidenceSha256 { get; init; }
    public required string StableFingerprintSha256 { get; init; }
    public required string StableFingerprintBase64 { get; init; }
    public required double Confidence { get; init; }
    public required PokemonIdentityTagObservation Tags { get; init; }
    public IReadOnlyList<NormalizedRegion> IgnoredDynamicRegions { get; init; } = Array.Empty<NormalizedRegion>();
    public IReadOnlyList<string> AnchorEvidence { get; init; } = Array.Empty<string>();
    public double? LowerAnchorY { get; init; }
}

public sealed record PokemonIdentityFrame
{
    public required byte[] ScreenshotPng { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record PokemonIdentityConsensus
{
    public required PokemonIdentityObservationStatus Status { get; init; }
    public required string StableFingerprintSha256 { get; init; }
    public required string StableFingerprintBase64 { get; init; }
    public required double Confidence { get; init; }
    public required IReadOnlyList<PokemonIdentityFingerprintObservation> Frames { get; init; }
    public required IReadOnlyList<string> EvidenceHashes { get; init; }
    public required PokemonIdentityTagObservation Tags { get; init; }
    public required int IgnoredFrameCount { get; init; }
}

public sealed record PokemonIdentityInstance
{
    public required string ScanRunId { get; init; }
    public required int Ordinal { get; init; }
    public required string InstanceId { get; init; }
    public required PokemonIdentityConsensus Consensus { get; init; }

    public static PokemonIdentityInstance Create(string scanRunId, int ordinal, PokemonIdentityConsensus consensus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scanRunId);
        if (ordinal <= 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
        ArgumentNullException.ThrowIfNull(consensus);
        return new PokemonIdentityInstance
        {
            ScanRunId = scanRunId,
            Ordinal = ordinal,
            InstanceId = $"{scanRunId}:{ordinal:D6}",
            Consensus = consensus
        };
    }
}
