using PogoInventory.Streaming;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.Streaming.Gates;

public enum RegionStabilityRole
{
    Required = 0,
    Volatile = 1,
    DiagnosticOnly = 2
}

[Flags]
public enum TemporalQualityFlags
{
    None = 0,
    MissingPreviousFrame = 1 << 0,
    LowSharpness = 1 << 1,
    LowBrightness = 1 << 2,
    HighBrightness = 1 << 3,
    LowContrast = 1 << 4,
    ResolutionChanged = 1 << 5,
    StreamFrozen = 1 << 6,
    SourceTimestampStalled = 1 << 7,
    OutOfOrderFrame = 1 << 8
}

public sealed record RegionDefinition
{
    public required string Name { get; init; }
    public required NormalizedRegion Region { get; init; }
    public RegionStabilityRole StabilityRole { get; init; } = RegionStabilityRole.DiagnosticOnly;
    public bool ObserveTransition { get; init; } = true;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        Region.Validate();
    }
}

public readonly record struct FrameResolution(int Width, int Height)
{
    public static FrameResolution From(FrameDescriptor descriptor) => new(descriptor.Width, descriptor.Height);
}

public sealed record RegionalFrameObservation
{
    public required string RegionName { get; init; }
    public required RegionStabilityRole StabilityRole { get; init; }
    public required bool ObserveTransition { get; init; }
    public required double DifferenceScore { get; init; }
    public required double MotionScore { get; init; }
    public required double SimilarityScore { get; init; }
    public required double SharpnessScore { get; init; }
    public required double BrightnessScore { get; init; }
    public required double ContrastScore { get; init; }
    public required double ChangeVelocity { get; init; }
    public required ulong VisualFingerprint { get; init; }
    public required bool IsLikelyStable { get; init; }
    public required bool IsLikelyTransitioning { get; init; }
}

public sealed record TemporalFrameObservation
{
    public required FrameId FrameId { get; init; }
    public required long SourceTicks { get; init; }
    public required TimeSpan MonotonicTimestamp { get; init; }
    public required DateTimeOffset UtcTimestamp { get; init; }
    public TimeSpan? FrameInterval { get; init; }
    public required double GlobalDifferenceScore { get; init; }
    public required IReadOnlyDictionary<string, double> RegionalDifferenceScores { get; init; }
    public required double MotionScore { get; init; }
    public required double SharpnessScore { get; init; }
    public required double FreezeScore { get; init; }
    public required double BrightnessScore { get; init; }
    public required double ContrastScore { get; init; }
    public required FrameResolution Resolution { get; init; }
    public required bool IsLikelyStable { get; init; }
    public required bool IsLikelyTransitioning { get; init; }
    public required TemporalQualityFlags QualityFlags { get; init; }
    public required IReadOnlyDictionary<string, RegionalFrameObservation> Regions { get; init; }
    public required ulong VisualFingerprint { get; init; }
    public required TimeSpan ObservationDuration { get; init; }

    public bool TryGetRegion(string name, out RegionalFrameObservation observation) =>
        Regions.TryGetValue(name, out observation!);
}

public static class VisualFingerprint
{
    public static double Similarity(ulong left, ulong right)
    {
        var differingBits = System.Numerics.BitOperations.PopCount(left ^ right);
        return 1d - (differingBits / 64d);
    }

    public static double RegionalSimilarity(
        TemporalFrameObservation left,
        TemporalFrameObservation right,
        IEnumerable<string> regionNames)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(regionNames);

        var similarities = new List<double>();
        foreach (var name in regionNames.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!left.TryGetRegion(name, out var leftRegion) || !right.TryGetRegion(name, out var rightRegion))
            {
                return 0;
            }

            similarities.Add(Similarity(leftRegion.VisualFingerprint, rightRegion.VisualFingerprint));
        }

        return similarities.Count == 0 ? 0 : similarities.Average();
    }

    public static IReadOnlyDictionary<string, ulong> CaptureRegions(
        TemporalFrameObservation observation,
        IEnumerable<string> regionNames)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(regionNames);

        var result = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var name in regionNames.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
        {
            if (!observation.TryGetRegion(name, out var region))
            {
                throw new InvalidOperationException($"Region '{name}' is missing from frame {observation.FrameId}.");
            }

            result.Add(name, region.VisualFingerprint);
        }

        return result;
    }

    public static double RegionalSimilarity(
        IReadOnlyDictionary<string, ulong> baseline,
        TemporalFrameObservation candidate)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);

        var similarities = new List<double>();
        foreach (var pair in baseline.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (!candidate.TryGetRegion(pair.Key, out var candidateRegion))
            {
                return 0;
            }

            similarities.Add(Similarity(pair.Value, candidateRegion.VisualFingerprint));
        }

        return similarities.Count == 0 ? 0 : similarities.Average();
    }
}
