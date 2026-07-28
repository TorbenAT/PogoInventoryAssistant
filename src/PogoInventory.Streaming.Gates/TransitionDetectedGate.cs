using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed class TransitionDetectedGate : ITemporalGate
{
    private readonly TransitionGateOptions _options;
    private int _changedFrames;
    private DateTimeOffset? _changeStartUtc;
    private readonly List<FrameId> _evidence = new();
    private double _maximumMagnitude;
    private string[] _lastChangedRegions = Array.Empty<string>();

    public TransitionDetectedGate(string name, TransitionGateOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Rejected,
                GateReasonCode.ResolutionChanged,
                0,
                observation.UtcTimestamp);
        }

        if ((observation.QualityFlags & TemporalQualityFlags.StreamFrozen) != 0)
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Rejected,
                GateReasonCode.StreamFrozen,
                0,
                observation.UtcTimestamp);
        }

        var missingRegions = GateEvaluation.MissingTransitionRegions(observation, _options);
        if (missingRegions.Count > 0)
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Rejected,
                GateReasonCode.RequiredRegionMissing,
                0,
                observation.UtcTimestamp,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["MissingTransitionRegions"] = missingRegions
                });
        }

        var changed = GateEvaluation.IsTransitionFrame(observation, _options, out var magnitude, out var changedRegions);
        if (!changed)
        {
            _changedFrames = 0;
            _changeStartUtc = null;
            _evidence.Clear();
            return TemporalGateResult.Pending(
                Name,
                session,
                GateReasonCode.TransitionNotDetected,
                0,
                Diagnostics("Awaiting sustained change.", Array.Empty<string>(), 0));
        }

        _changeStartUtc ??= observation.UtcTimestamp;
        _changedFrames++;
        _evidence.Add(observation.FrameId);
        while (_evidence.Count > Math.Max(8, _options.MinimumChangedFrames))
        {
            _evidence.RemoveAt(0);
        }

        _maximumMagnitude = Math.Max(_maximumMagnitude, magnitude);
        _lastChangedRegions = changedRegions;
        var duration = observation.UtcTimestamp - _changeStartUtc.Value;
        var progress = Math.Min(
            _changedFrames / (double)_options.MinimumChangedFrames,
            _options.MinimumChangedDuration == TimeSpan.Zero
                ? 1
                : duration.TotalMilliseconds / _options.MinimumChangedDuration.TotalMilliseconds);

        if (_changedFrames >= _options.MinimumChangedFrames && duration >= _options.MinimumChangedDuration)
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Passed,
                GateReasonCode.Passed,
                Math.Clamp(progress, 0, 1),
                observation.UtcTimestamp,
                Diagnostics("Sustained visual transition detected.", changedRegions, duration.TotalMilliseconds),
                _evidence.ToArray());
        }

        return TemporalGateResult.Pending(
            Name,
            session,
            GateReasonCode.TransitionNotDetected,
            Math.Clamp(progress, 0, 1),
            Diagnostics("Change seen, but sustained transition evidence is incomplete.", changedRegions, duration.TotalMilliseconds),
            _evidence.ToArray());
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
            GateReasonCode.TransitionNotDetected,
            error,
            Diagnostics("Transition detection did not complete.", _lastChangedRegions, 0));
    }

    private IReadOnlyDictionary<string, object?> Diagnostics(string message, IReadOnlyList<string> changedRegions, double durationMs) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Message"] = message,
            ["ChangedFrames"] = _changedFrames,
            ["ChangeDurationMs"] = durationMs,
            ["MaximumChangeMagnitude"] = _maximumMagnitude,
            ["ChangedRegions"] = changedRegions
        };
}
