using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed class StableRegionGate : ITemporalGate
{
    private readonly StableRegionGateOptions _options;
    private readonly IReadOnlyList<RegionDefinition> _regions;
    private readonly List<FrameId> _stableEvidence = new();
    private int _consecutiveStableFrames;
    private DateTimeOffset? _stableStartUtc;
    private TemporalFrameObservation? _lastEvidenceObservation;
    private GateReasonCode _lastReason = GateReasonCode.InsufficientStableFrames;

    public StableRegionGate(
        string name,
        StableRegionGateOptions options,
        IReadOnlyList<RegionDefinition> regions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _regions = regions ?? throw new ArgumentNullException(nameof(regions));
    }

    public string Name { get; }

    public TemporalGateResult Observe(TemporalGateSession session, TemporalFrameObservation observation)
    {
        var elapsed = observation.UtcTimestamp - session.StartUtc;
        if (elapsed > _options.MaximumObservationDuration)
        {
            return Complete(session, GateTermination.Timeout, observation.UtcTimestamp);
        }

        var evaluation = GateEvaluation.EvaluateStableRegions(observation, _options, _regions);
        _lastReason = evaluation.ReasonCode;
        if (!evaluation.IsStable)
        {
            ResetEvidence();
            return TemporalGateResult.Pending(Name, session, evaluation.ReasonCode, 0, evaluation.Diagnostics);
        }

        _stableStartUtc ??= observation.UtcTimestamp;
        _consecutiveStableFrames++;
        if (IsDiverseEvidence(observation))
        {
            _stableEvidence.Add(observation.FrameId);
            _lastEvidenceObservation = observation;
        }

        var stableDuration = observation.UtcTimestamp - _stableStartUtc.Value;
        var diagnostics = new Dictionary<string, object?>(evaluation.Diagnostics, StringComparer.Ordinal)
        {
            ["ConsecutiveStableFrames"] = _consecutiveStableFrames,
            ["StableDurationMs"] = stableDuration.TotalMilliseconds,
            ["MinimumStableFrames"] = _options.MinimumStableFrames,
            ["MinimumStableDurationMs"] = _options.MinimumStableDuration.TotalMilliseconds,
            ["DiverseEvidenceFrames"] = _stableEvidence.Select(x => x.Value).ToArray()
        };
        var countProgress = Math.Min(1d, _consecutiveStableFrames / (double)_options.MinimumStableFrames);
        var durationProgress = _options.MinimumStableDuration == TimeSpan.Zero
            ? 1d
            : Math.Min(1d, stableDuration.TotalMilliseconds / _options.MinimumStableDuration.TotalMilliseconds);
        var confidence = evaluation.Confidence * Math.Min(countProgress, durationProgress);
        var requiredEvidenceCount = Math.Min(2, _options.MinimumStableFrames);

        if (_consecutiveStableFrames >= _options.MinimumStableFrames &&
            stableDuration >= _options.MinimumStableDuration &&
            _stableEvidence.Count >= requiredEvidenceCount)
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Passed,
                GateReasonCode.Passed,
                confidence,
                observation.UtcTimestamp,
                diagnostics,
                _stableEvidence.ToArray());
        }

        _lastReason = GateReasonCode.InsufficientStableFrames;
        return TemporalGateResult.Pending(
            Name,
            session,
            GateReasonCode.InsufficientStableFrames,
            confidence,
            diagnostics,
            _stableEvidence.ToArray());
    }

    public TemporalGateResult Complete(
        TemporalGateSession session,
        GateTermination termination,
        DateTimeOffset timestamp,
        Exception? error = null)
    {
        return GateEvaluation.CompletePendingGate(
            Name,
            session,
            termination,
            timestamp,
            _lastReason == GateReasonCode.Pending ? GateReasonCode.StabilityNotEstablished : _lastReason,
            error,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ConsecutiveStableFrames"] = _consecutiveStableFrames,
                ["SelectedStableEvidence"] = _stableEvidence.Select(x => x.Value).ToArray()
            });
    }

    private bool IsDiverseEvidence(TemporalFrameObservation observation)
    {
        if (_lastEvidenceObservation is null)
        {
            return true;
        }

        if (observation.FrameId.Value - _lastEvidenceObservation.FrameId.Value < _options.MinimumEvidenceFrameIdDistance)
        {
            return false;
        }

        if (observation.UtcTimestamp - _lastEvidenceObservation.UtcTimestamp < _options.MinimumEvidenceTimeDistance)
        {
            return false;
        }

        if (_options.MaximumEvidenceVisualSimilarity >= 1)
        {
            return true;
        }

        var similarity = VisualFingerprint.RegionalSimilarity(
            _lastEvidenceObservation,
            observation,
            _options.RequiredRegions);
        return similarity <= _options.MaximumEvidenceVisualSimilarity;
    }

    private void ResetEvidence()
    {
        _consecutiveStableFrames = 0;
        _stableStartUtc = null;
        _stableEvidence.Clear();
        _lastEvidenceObservation = null;
    }
}
