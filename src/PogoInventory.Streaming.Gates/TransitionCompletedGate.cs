using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed class TransitionCompletedGate : ITemporalGate
{
    private enum Phase
    {
        PreStable = 0,
        Transition = 1,
        PostStable = 2,
        Completed = 3
    }

    private readonly TransitionGateOptions _options;
    private readonly IReadOnlyList<RegionDefinition> _regions;
    private readonly IReadOnlyList<string> _requiredStableRegions;
    private readonly StableRegionGateOptions _stableOptions;
    private readonly List<FrameId> _preEvidence = new();
    private readonly List<FrameId> _transitionEvidence = new();
    private readonly List<FrameId> _postEvidence = new();
    private Phase _phase;
    private int _preStableCount;
    private int _changedCount;
    private int _postStableCount;
    private DateTimeOffset? _preStableStart;
    private DateTimeOffset? _changeStart;
    private DateTimeOffset? _postStableStart;
    private IReadOnlyDictionary<string, ulong>? _preFingerprints;
    private double _maximumChangeMagnitude;
    private GateReasonCode _timeoutReason = GateReasonCode.StabilityNotEstablished;

    public TransitionCompletedGate(
        string name,
        TransitionGateOptions options,
        IReadOnlyList<RegionDefinition> regions,
        IReadOnlyList<string> requiredStableRegions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _regions = regions ?? throw new ArgumentNullException(nameof(regions));
        _requiredStableRegions = requiredStableRegions ?? throw new ArgumentNullException(nameof(requiredStableRegions));
        _stableOptions = GateEvaluation.StableOptionsFromTransition(_requiredStableRegions, _options);
    }

    public string Name { get; }

    public TemporalGateResult Observe(TemporalGateSession session, TemporalFrameObservation observation)
    {
        if (observation.UtcTimestamp - session.StartUtc > _options.MaximumObservationDuration)
        {
            return Complete(session, GateTermination.Timeout, observation.UtcTimestamp);
        }

        if ((observation.QualityFlags & TemporalQualityFlags.ResolutionChanged) != 0)
        {
            return TerminalFailure(session, observation.UtcTimestamp, GateReasonCode.ResolutionChanged, "Resolution changed during transition observation.");
        }

        if ((observation.QualityFlags & TemporalQualityFlags.StreamFrozen) != 0)
        {
            return TerminalFailure(session, observation.UtcTimestamp, GateReasonCode.StreamFrozen, "The stream froze during transition observation.");
        }

        return _phase switch
        {
            Phase.PreStable => ObservePreStable(session, observation),
            Phase.Transition => ObserveTransition(session, observation),
            Phase.PostStable => ObservePostStable(session, observation),
            Phase.Completed => throw new InvalidOperationException("TransitionCompletedGate received an observation after completion."),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public TemporalGateResult Complete(
        TemporalGateSession session,
        GateTermination termination,
        DateTimeOffset timestamp,
        Exception? error = null)
    {
        var reason = _phase switch
        {
            Phase.PreStable => GateReasonCode.StabilityNotEstablished,
            Phase.Transition => GateReasonCode.TransitionNotDetected,
            Phase.PostStable => _timeoutReason == GateReasonCode.NoMeaningfulVisualProgression
                ? GateReasonCode.NoMeaningfulVisualProgression
                : GateReasonCode.PostTransitionStabilityNotEstablished,
            _ => _timeoutReason
        };

        return GateEvaluation.CompletePendingGate(
            Name,
            session,
            termination,
            timestamp,
            reason,
            error,
            Diagnostics("Transition sequence did not complete."));
    }

    private TemporalGateResult ObservePreStable(TemporalGateSession session, TemporalFrameObservation observation)
    {
        var stable = GateEvaluation.EvaluateStableRegions(observation, _stableOptions, _regions);
        if (!stable.IsStable)
        {
            _preStableCount = 0;
            _preStableStart = null;
            _preEvidence.Clear();
            _timeoutReason = stable.ReasonCode;
            return Pending(session, observation, stable.ReasonCode, 0, "PreStable", stable.Diagnostics);
        }

        _preStableStart ??= observation.UtcTimestamp;
        _preStableCount++;
        AddBounded(_preEvidence, observation.FrameId, _options.MinimumPreStableFrames);
        var duration = observation.UtcTimestamp - _preStableStart.Value;
        var countReady = _preStableCount >= _options.MinimumPreStableFrames;
        var durationReady = duration >= _options.MinimumStableDuration;
        if (countReady && durationReady)
        {
            _preFingerprints = VisualFingerprint.CaptureRegions(observation, _options.TransitionRegions);
            _phase = Phase.Transition;
            _timeoutReason = GateReasonCode.TransitionNotDetected;
            return Pending(
                session,
                observation,
                GateReasonCode.TransitionNotDetected,
                0.33,
                "Transition",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Message"] = "Stable precondition established; awaiting sustained transition.",
                    ["PreStableFrames"] = _preStableCount,
                    ["PreStableDurationMs"] = duration.TotalMilliseconds,
                    ["IgnoredMotionRegions"] = stable.MovingVolatileRegions
                });
        }

        _timeoutReason = GateReasonCode.InsufficientStableFrames;
        return Pending(
            session,
            observation,
            GateReasonCode.InsufficientStableFrames,
            0.15,
            "PreStable",
            stable.Diagnostics);
    }

    private TemporalGateResult ObserveTransition(TemporalGateSession session, TemporalFrameObservation observation)
    {
        var missingRegions = GateEvaluation.MissingTransitionRegions(observation, _options);
        if (missingRegions.Count > 0)
        {
            return TerminalFailure(
                session,
                observation.UtcTimestamp,
                GateReasonCode.RequiredRegionMissing,
                $"Transition regions are missing: {string.Join(", ", missingRegions)}.");
        }

        var changed = GateEvaluation.IsTransitionFrame(observation, _options, out var magnitude, out var changedRegions);
        if (!changed)
        {
            _changedCount = 0;
            _changeStart = null;
            _transitionEvidence.Clear();
            return Pending(
                session,
                observation,
                GateReasonCode.TransitionNotDetected,
                0.33,
                "Transition",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Message"] = "Stable precondition exists; no sustained transition yet.",
                    ["TransitionRegions"] = _options.TransitionRegions
                });
        }

        _changeStart ??= observation.UtcTimestamp;
        _changedCount++;
        _maximumChangeMagnitude = Math.Max(_maximumChangeMagnitude, magnitude);
        AddBounded(_transitionEvidence, observation.FrameId, Math.Max(_options.MinimumChangedFrames, 8));
        var duration = observation.UtcTimestamp - _changeStart.Value;
        if (_changedCount >= _options.MinimumChangedFrames && duration >= _options.MinimumChangedDuration)
        {
            _phase = Phase.PostStable;
            _postStableCount = 0;
            _postStableStart = null;
            _timeoutReason = GateReasonCode.PostTransitionStabilityNotEstablished;
            return Pending(
                session,
                observation,
                GateReasonCode.PostTransitionStabilityNotEstablished,
                0.66,
                "PostStable",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Message"] = "Sustained transition established; awaiting stable postcondition.",
                    ["ChangedFrames"] = _changedCount,
                    ["ChangeDurationMs"] = duration.TotalMilliseconds,
                    ["ChangedRegions"] = changedRegions,
                    ["MaximumChangeMagnitude"] = _maximumChangeMagnitude
                });
        }

        return Pending(
            session,
            observation,
            GateReasonCode.TransitionNotDetected,
            0.45,
            "Transition",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Message"] = "Change is visible but sustained transition evidence is incomplete.",
                ["ChangedFrames"] = _changedCount,
                ["ChangeDurationMs"] = duration.TotalMilliseconds,
                ["ChangedRegions"] = changedRegions
            });
    }

    private TemporalGateResult ObservePostStable(TemporalGateSession session, TemporalFrameObservation observation)
    {
        var stable = GateEvaluation.EvaluateStableRegions(observation, _stableOptions, _regions);
        if (!stable.IsStable)
        {
            _postStableCount = 0;
            _postStableStart = null;
            _postEvidence.Clear();
            _timeoutReason = GateReasonCode.PostTransitionStabilityNotEstablished;
            return Pending(session, observation, stable.ReasonCode, 0.66, "PostStable", stable.Diagnostics);
        }

        _postStableStart ??= observation.UtcTimestamp;
        _postStableCount++;
        AddBounded(_postEvidence, observation.FrameId, _options.MinimumPostStableFrames);
        var duration = observation.UtcTimestamp - _postStableStart.Value;
        if (_postStableCount < _options.MinimumPostStableFrames || duration < _options.MinimumStableDuration)
        {
            return Pending(
                session,
                observation,
                GateReasonCode.PostTransitionStabilityNotEstablished,
                0.75,
                "PostStable",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Message"] = "Postcondition is becoming stable but evidence is incomplete.",
                    ["PostStableFrames"] = _postStableCount,
                    ["PostStableDurationMs"] = duration.TotalMilliseconds,
                    ["IgnoredMotionRegions"] = stable.MovingVolatileRegions
                });
        }

        if (_preFingerprints is null)
        {
            return TerminalFailure(
                session,
                observation.UtcTimestamp,
                GateReasonCode.InsufficientEvidence,
                "Precondition fingerprints were not captured.");
        }

        var similarity = VisualFingerprint.RegionalSimilarity(_preFingerprints, observation);
        var meaningfulChange = 1 - similarity;
        if (meaningfulChange < _options.MinimumMeaningfulChange)
        {
            _timeoutReason = GateReasonCode.NoMeaningfulVisualProgression;
            return Pending(
                session,
                observation,
                GateReasonCode.NoMeaningfulVisualProgression,
                0.80,
                "PostStable",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["Message"] = "A transition occurred, but pre- and postconditions remain too visually similar.",
                    ["VisualSimilarity"] = similarity,
                    ["ChangeMagnitude"] = meaningfulChange,
                    ["RequiredChangeMagnitude"] = _options.MinimumMeaningfulChange,
                    ["ComparedRegions"] = _options.TransitionRegions
                });
        }

        _phase = Phase.Completed;
        var evidence = _preEvidence
            .Concat(_transitionEvidence)
            .Concat(_postEvidence)
            .Distinct()
            .ToArray();
        return TemporalGateResult.Terminal(
            Name,
            session,
            TemporalGateState.Passed,
            GateReasonCode.Passed,
            Math.Clamp(0.75 + (meaningfulChange * 0.25), 0, 1),
            observation.UtcTimestamp,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Phase"] = "Completed",
                ["PreEvidenceFrames"] = _preEvidence.Select(x => x.Value).ToArray(),
                ["TransitionFrames"] = _transitionEvidence.Select(x => x.Value).ToArray(),
                ["PostEvidenceFrames"] = _postEvidence.Select(x => x.Value).ToArray(),
                ["ChangeMagnitude"] = meaningfulChange,
                ["MaximumTransitionMagnitude"] = _maximumChangeMagnitude,
                ["ComparedRegions"] = _options.TransitionRegions,
                ["CompletionReason"] = "Stable precondition, sustained transition, stable and meaningfully different postcondition."
            },
            evidence);
    }

    private TemporalGateResult Pending(
        TemporalGateSession session,
        TemporalFrameObservation observation,
        GateReasonCode reason,
        double confidence,
        string phase,
        IReadOnlyDictionary<string, object?> diagnostics)
    {
        var combined = new Dictionary<string, object?>(diagnostics, StringComparer.Ordinal)
        {
            ["Phase"] = phase,
            ["FrameId"] = observation.FrameId.Value
        };
        return TemporalGateResult.Pending(Name, session, reason, confidence, combined, CurrentEvidence());
    }

    private TemporalGateResult TerminalFailure(
        TemporalGateSession session,
        DateTimeOffset timestamp,
        GateReasonCode reason,
        string message)
    {
        return TemporalGateResult.Terminal(
            Name,
            session,
            TemporalGateState.Rejected,
            reason,
            0,
            timestamp,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["Phase"] = _phase.ToString(),
                ["Message"] = message
            },
            CurrentEvidence());
    }

    private IReadOnlyList<FrameId> CurrentEvidence() =>
        _preEvidence.Concat(_transitionEvidence).Concat(_postEvidence).Distinct().ToArray();

    private IReadOnlyDictionary<string, object?> Diagnostics(string message) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Phase"] = _phase.ToString(),
            ["Message"] = message,
            ["PreStableFrames"] = _preStableCount,
            ["ChangedFrames"] = _changedCount,
            ["PostStableFrames"] = _postStableCount,
            ["MaximumChangeMagnitude"] = _maximumChangeMagnitude
        };

    private static void AddBounded(List<FrameId> frames, FrameId frameId, int capacity)
    {
        frames.Add(frameId);
        while (frames.Count > Math.Max(2, capacity))
        {
            frames.RemoveAt(0);
        }
    }
}
