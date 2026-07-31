using PogoInventory.Streaming;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.Streaming.Gates;

internal sealed record RegionGateEvaluation(
    bool IsStable,
    GateReasonCode ReasonCode,
    double Confidence,
    IReadOnlyList<string> MovingVolatileRegions,
    IReadOnlyDictionary<string, object?> Diagnostics);

internal static class GateEvaluation
{
    public static RegionGateEvaluation EvaluateStableRegions(
        TemporalFrameObservation observation,
        StableRegionGateOptions options,
        IReadOnlyList<RegionDefinition> definitions)
    {
        if ((observation.QualityFlags & TemporalQualityFlags.ResolutionChanged) != 0)
        {
            return Failure(GateReasonCode.ResolutionChanged, observation, Array.Empty<string>(), "Resolution changed during observation.");
        }

        if ((observation.QualityFlags & TemporalQualityFlags.StreamFrozen) != 0)
        {
            return Failure(GateReasonCode.StreamFrozen, observation, Array.Empty<string>(), "Stream freeze evidence is present.");
        }

        var definitionsByName = definitions.ToDictionary(x => x.Name, x => x, StringComparer.Ordinal);
        var volatileDefinitions = definitions
            .Where(x => x.StabilityRole == RegionStabilityRole.Volatile)
            .ToArray();
        var movingVolatile = observation.Regions.Values
            .Where(x => x.StabilityRole == RegionStabilityRole.Volatile && !x.IsLikelyStable)
            .Select(x => x.RegionName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        var stableScores = new List<double>();
        foreach (var requiredName in options.RequiredRegions)
        {
            if (!observation.TryGetRegion(requiredName, out var region) || !definitionsByName.TryGetValue(requiredName, out var requiredDefinition))
            {
                return Failure(
                    GateReasonCode.RequiredRegionMissing,
                    observation,
                    movingVolatile,
                    $"Required region '{requiredName}' is missing.");
            }

            if (region.StabilityRole == RegionStabilityRole.Volatile)
            {
                return Failure(
                    GateReasonCode.RequiredRegionContaminatedByMotion,
                    observation,
                    movingVolatile,
                    $"Required region '{requiredName}' is configured as volatile.");
            }

            var overlappingMovingVolatile = volatileDefinitions
                .Where(x => movingVolatile.Contains(x.Name, StringComparer.Ordinal))
                .Where(x => Overlaps(requiredDefinition.Region, x.Region))
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (overlappingMovingVolatile.Length > 0)
            {
                return Failure(
                    GateReasonCode.RequiredRegionContaminatedByMotion,
                    observation,
                    movingVolatile,
                    $"Required region '{requiredName}' overlaps moving volatile content: {string.Join(", ", overlappingMovingVolatile)}.",
                    requiredName,
                    region,
                    overlappingMovingVolatile);
            }

            if (region.MotionScore > options.MaximumMotionScore)
            {
                return Failure(
                    GateReasonCode.MotionTooHigh,
                    observation,
                    movingVolatile,
                    $"Motion in required region '{requiredName}' exceeded the threshold.",
                    requiredName,
                    region);
            }

            if (region.DifferenceScore > options.MaximumDifferenceScore)
            {
                return Failure(
                    GateReasonCode.DifferenceTooHigh,
                    observation,
                    movingVolatile,
                    $"Difference in required region '{requiredName}' exceeded the threshold.",
                    requiredName,
                    region);
            }

            if (region.SimilarityScore < options.MinimumSimilarityScore)
            {
                return Failure(
                    GateReasonCode.SimilarityTooLow,
                    observation,
                    movingVolatile,
                    $"Structural similarity in required region '{requiredName}' was below the threshold.",
                    requiredName,
                    region);
            }

            var minimumSharpness = options.MinimumSharpnessScoreByRegion.TryGetValue(
                requiredName,
                out var regionalMinimumSharpness)
                ? regionalMinimumSharpness
                : options.MinimumSharpnessScore;
            if (region.SharpnessScore < minimumSharpness)
            {
                return Failure(
                    GateReasonCode.SharpnessTooLow,
                    observation,
                    movingVolatile,
                    $"Sharpness in required region '{requiredName}' was below the threshold.",
                    requiredName,
                    region);
            }

            stableScores.Add(
                Math.Clamp(
                    ((1 - region.MotionScore) * 0.30) +
                    ((1 - region.DifferenceScore) * 0.25) +
                    (region.SimilarityScore * 0.30) +
                    (region.SharpnessScore * 0.15),
                    0,
                    1));
        }

        var confidence = stableScores.Count == 0 ? 0 : stableScores.Average();
        return new RegionGateEvaluation(
            true,
            GateReasonCode.Pending,
            confidence,
            movingVolatile,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["RequiredRegionsStable"] = true,
                ["VolatileRegionsMoving"] = movingVolatile.Length > 0,
                ["IgnoredMotionRegions"] = movingVolatile,
                ["RequiredRegionCount"] = options.RequiredRegions.Count
            });
    }

    public static bool IsTransitionFrame(
        TemporalFrameObservation observation,
        TransitionGateOptions options,
        out double magnitude,
        out string[] changedRegions)
    {
        var changed = new List<(string Name, double Magnitude)>();
        foreach (var name in options.TransitionRegions)
        {
            if (!observation.TryGetRegion(name, out var region) || !region.ObserveTransition)
            {
                continue;
            }

            var regionMagnitude = Math.Max(region.MotionScore, region.DifferenceScore);
            if (region.MotionScore >= options.MinimumMotionScore ||
                region.DifferenceScore >= options.MinimumDifferenceScore)
            {
                changed.Add((name, regionMagnitude));
            }
        }

        magnitude = changed.Count == 0 ? 0 : changed.Max(x => x.Magnitude);
        changedRegions = changed.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return changed.Count > 0;
    }

    public static IReadOnlyList<string> MissingTransitionRegions(
        TemporalFrameObservation observation,
        TransitionGateOptions options) =>
        options.TransitionRegions
            .Where(name => !observation.TryGetRegion(name, out var region) || !region.ObserveTransition)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

    public static StableRegionGateOptions StableOptionsFromTransition(
        IReadOnlyList<string> requiredRegions,
        TransitionGateOptions options) => new()
    {
        RequiredRegions = requiredRegions,
        MinimumStableFrames = 1,
        MinimumStableDuration = TimeSpan.Zero,
        MaximumMotionScore = options.MaximumStableMotionScore,
        MaximumDifferenceScore = options.MaximumStableDifferenceScore,
        MinimumSimilarityScore = options.MinimumStableSimilarityScore,
        MinimumSharpnessScore = options.MinimumSharpnessScore,
        MaximumObservationDuration = options.MaximumObservationDuration,
        MinimumEvidenceFrameIdDistance = 1,
        MinimumEvidenceTimeDistance = TimeSpan.Zero,
        MaximumEvidenceVisualSimilarity = 1.0
    };

    public static TemporalGateResult CompletePendingGate(
        string gateName,
        TemporalGateSession session,
        GateTermination termination,
        DateTimeOffset timestamp,
        GateReasonCode timeoutReason,
        Exception? error,
        IReadOnlyDictionary<string, object?>? diagnostics = null)
    {
        return termination switch
        {
            GateTermination.Timeout => TemporalGateResult.Terminal(
                gateName,
                session,
                TemporalGateState.TimedOut,
                session.FramesObserved == 0 ? GateReasonCode.NoFramesReceived : timeoutReason,
                0,
                timestamp,
                diagnostics),
            GateTermination.Cancelled => TemporalGateResult.Terminal(
                gateName,
                session,
                TemporalGateState.Cancelled,
                GateReasonCode.Cancelled,
                0,
                timestamp,
                diagnostics),
            GateTermination.StreamEnded => TemporalGateResult.Terminal(
                gateName,
                session,
                TemporalGateState.Rejected,
                session.FramesObserved == 0 ? GateReasonCode.NoFramesReceived : GateReasonCode.InsufficientEvidence,
                0,
                timestamp,
                diagnostics),
            GateTermination.Faulted => TemporalGateResult.Terminal(
                gateName,
                session,
                TemporalGateState.Faulted,
                GateReasonCode.Faulted,
                0,
                timestamp,
                MergeDiagnostics(diagnostics, error)),
            _ => throw new ArgumentOutOfRangeException(nameof(termination))
        };
    }

    private static RegionGateEvaluation Failure(
        GateReasonCode reasonCode,
        TemporalFrameObservation observation,
        IReadOnlyList<string> movingVolatile,
        string message,
        string? regionName = null,
        RegionalFrameObservation? region = null,
        IReadOnlyList<string>? overlappingVolatileRegions = null)
    {
        var diagnostics = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Message"] = message,
            ["FrameId"] = observation.FrameId.Value,
            ["RequiredRegionsStable"] = false,
            ["VolatileRegionsMoving"] = movingVolatile.Count > 0,
            ["IgnoredMotionRegions"] = movingVolatile
        };
        if (overlappingVolatileRegions is not null)
        {
            diagnostics["OverlappingVolatileRegions"] = overlappingVolatileRegions;
        }

        if (regionName is not null && region is not null)
        {
            diagnostics["FailedRegion"] = regionName;
            diagnostics["MotionScore"] = region.MotionScore;
            diagnostics["DifferenceScore"] = region.DifferenceScore;
            diagnostics["SimilarityScore"] = region.SimilarityScore;
            diagnostics["SharpnessScore"] = region.SharpnessScore;
        }

        return new RegionGateEvaluation(false, reasonCode, 0, movingVolatile, diagnostics);
    }

    private static bool Overlaps(NormalizedRegion left, NormalizedRegion right)
    {
        var intersectionWidth = Math.Min(left.X + left.Width, right.X + right.Width) - Math.Max(left.X, right.X);
        var intersectionHeight = Math.Min(left.Y + left.Height, right.Y + right.Height) - Math.Max(left.Y, right.Y);
        return intersectionWidth > 0.000001 && intersectionHeight > 0.000001;
    }

    private static IReadOnlyDictionary<string, object?> MergeDiagnostics(
        IReadOnlyDictionary<string, object?>? diagnostics,
        Exception? error)
    {
        var result = diagnostics is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(diagnostics, StringComparer.Ordinal);
        if (error is not null)
        {
            result["ExceptionType"] = error.GetType().FullName;
            result["ExceptionMessage"] = error.Message;
        }

        return result;
    }
}
