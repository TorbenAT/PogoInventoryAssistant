namespace PogoInventory.Streaming.Gates;

public sealed class AllOfGate : ITemporalGate
{
    private readonly IReadOnlyList<ITemporalGate> _children;
    private readonly Dictionary<string, TemporalGateResult> _results = new(StringComparer.Ordinal);

    public AllOfGate(string name, IReadOnlyList<ITemporalGate> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (children is null || children.Count == 0)
        {
            throw new ArgumentException("At least one child gate is required.", nameof(children));
        }

        if (children.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != children.Count)
        {
            throw new ArgumentException("Child gate names must be unique within a composite gate.", nameof(children));
        }

        Name = name;
        _children = children;
    }

    public string Name { get; }

    public TemporalGateResult Observe(TemporalGateSession session, TemporalFrameObservation observation)
    {
        foreach (var child in _children)
        {
            if (_results.TryGetValue(child.Name, out var prior) && prior.IsTerminal)
            {
                continue;
            }

            var result = child.Observe(session, observation);
            _results[child.Name] = result;
            if (result.GateState is TemporalGateState.Rejected or TemporalGateState.TimedOut or TemporalGateState.Cancelled or TemporalGateState.Faulted)
            {
                return TemporalGateResult.Terminal(
                    Name,
                    session,
                    result.GateState,
                    GateReasonCode.CompositeRequirementFailed,
                    0,
                    observation.UtcTimestamp,
                    Diagnostics());
            }
        }

        if (_children.All(x => _results.TryGetValue(x.Name, out var result) && result.GateState == TemporalGateState.Passed))
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Passed,
                GateReasonCode.Passed,
                _results.Values.Min(x => x.Confidence),
                observation.UtcTimestamp,
                Diagnostics(),
                _results.Values.SelectMany(x => x.SelectedEvidenceFrameIds).Distinct().ToArray());
        }

        return TemporalGateResult.Pending(Name, session, GateReasonCode.Pending, AverageConfidence(), Diagnostics());
    }

    public TemporalGateResult Complete(TemporalGateSession session, GateTermination termination, DateTimeOffset timestamp, Exception? error = null)
    {
        foreach (var child in _children)
        {
            if (!_results.TryGetValue(child.Name, out var result) || !result.IsTerminal)
            {
                _results[child.Name] = child.Complete(session, termination, timestamp, error);
            }
        }

        return GateEvaluation.CompletePendingGate(
            Name,
            session,
            termination,
            timestamp,
            GateReasonCode.CompositeRequirementFailed,
            error,
            Diagnostics());
    }

    private double AverageConfidence() => _results.Count == 0 ? 0 : _results.Values.Average(x => x.Confidence);

    private IReadOnlyDictionary<string, object?> Diagnostics() =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Composition"] = "AllOf",
            ["Children"] = _children.Select(x => x.Name).ToArray(),
            ["ChildStates"] = _results.ToDictionary(x => x.Key, x => x.Value.GateState.ToString(), StringComparer.Ordinal),
            ["ChildReasons"] = _results.ToDictionary(x => x.Key, x => x.Value.ReasonCode.ToString(), StringComparer.Ordinal)
        };
}

public sealed class AnyOfGate : ITemporalGate
{
    private readonly IReadOnlyList<ITemporalGate> _children;
    private readonly Dictionary<string, TemporalGateResult> _results = new(StringComparer.Ordinal);

    public AnyOfGate(string name, IReadOnlyList<ITemporalGate> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (children is null || children.Count == 0)
        {
            throw new ArgumentException("At least one child gate is required.", nameof(children));
        }

        if (children.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != children.Count)
        {
            throw new ArgumentException("Child gate names must be unique within a composite gate.", nameof(children));
        }

        Name = name;
        _children = children;
    }

    public string Name { get; }

    public TemporalGateResult Observe(TemporalGateSession session, TemporalFrameObservation observation)
    {
        foreach (var child in _children)
        {
            if (_results.TryGetValue(child.Name, out var prior) && prior.IsTerminal)
            {
                continue;
            }

            var result = child.Observe(session, observation);
            _results[child.Name] = result;
            if (result.GateState == TemporalGateState.Passed)
            {
                return TemporalGateResult.Terminal(
                    Name,
                    session,
                    TemporalGateState.Passed,
                    GateReasonCode.Passed,
                    result.Confidence,
                    observation.UtcTimestamp,
                    Diagnostics(),
                    result.SelectedEvidenceFrameIds);
            }
        }

        var allTerminal = _children.All(x => _results.TryGetValue(x.Name, out var result) && result.IsTerminal);
        if (allTerminal)
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                TemporalGateState.Rejected,
                GateReasonCode.CompositeRequirementFailed,
                0,
                observation.UtcTimestamp,
                Diagnostics());
        }

        return TemporalGateResult.Pending(Name, session, GateReasonCode.Pending, BestConfidence(), Diagnostics());
    }

    public TemporalGateResult Complete(TemporalGateSession session, GateTermination termination, DateTimeOffset timestamp, Exception? error = null)
    {
        foreach (var child in _children)
        {
            if (!_results.TryGetValue(child.Name, out var result) || !result.IsTerminal)
            {
                _results[child.Name] = child.Complete(session, termination, timestamp, error);
            }
        }

        return GateEvaluation.CompletePendingGate(
            Name,
            session,
            termination,
            timestamp,
            GateReasonCode.CompositeRequirementFailed,
            error,
            Diagnostics());
    }

    private double BestConfidence() => _results.Count == 0 ? 0 : _results.Values.Max(x => x.Confidence);

    private IReadOnlyDictionary<string, object?> Diagnostics() =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Composition"] = "AnyOf",
            ["Children"] = _children.Select(x => x.Name).ToArray(),
            ["ChildStates"] = _results.ToDictionary(x => x.Key, x => x.Value.GateState.ToString(), StringComparer.Ordinal),
            ["ChildReasons"] = _results.ToDictionary(x => x.Key, x => x.Value.ReasonCode.ToString(), StringComparer.Ordinal)
        };
}

public sealed class SequenceGate : ITemporalGate
{
    private readonly IReadOnlyList<ITemporalGate> _children;
    private readonly List<TemporalGateResult> _completed = new();
    private int _index;

    public SequenceGate(string name, IReadOnlyList<ITemporalGate> children)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (children is null || children.Count == 0)
        {
            throw new ArgumentException("At least one child gate is required.", nameof(children));
        }

        if (children.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != children.Count)
        {
            throw new ArgumentException("Child gate names must be unique within a composite gate.", nameof(children));
        }

        Name = name;
        _children = children;
    }

    public string Name { get; }

    public TemporalGateResult Observe(TemporalGateSession session, TemporalFrameObservation observation)
    {
        var current = _children[_index];
        var result = current.Observe(session, observation);
        if (result.GateState == TemporalGateState.Passed)
        {
            _completed.Add(result);
            _index++;
            if (_index == _children.Count)
            {
                return TemporalGateResult.Terminal(
                    Name,
                    session,
                    TemporalGateState.Passed,
                    GateReasonCode.Passed,
                    _completed.Min(x => x.Confidence),
                    observation.UtcTimestamp,
                    Diagnostics(),
                    _completed.SelectMany(x => x.SelectedEvidenceFrameIds).Distinct().ToArray());
            }

            return TemporalGateResult.Pending(
                Name,
                session,
                GateReasonCode.Pending,
                _completed.Average(x => x.Confidence),
                Diagnostics());
        }

        if (result.GateState is TemporalGateState.Rejected or TemporalGateState.TimedOut or TemporalGateState.Cancelled or TemporalGateState.Faulted)
        {
            return TemporalGateResult.Terminal(
                Name,
                session,
                result.GateState,
                GateReasonCode.SequenceChildFailed,
                0,
                observation.UtcTimestamp,
                Diagnostics());
        }

        return TemporalGateResult.Pending(Name, session, result.ReasonCode, result.Confidence, Diagnostics());
    }

    public TemporalGateResult Complete(TemporalGateSession session, GateTermination termination, DateTimeOffset timestamp, Exception? error = null)
    {
        var currentResult = _children[_index].Complete(session, termination, timestamp, error);
        return GateEvaluation.CompletePendingGate(
            Name,
            session,
            termination,
            timestamp,
            GateReasonCode.SequenceChildFailed,
            error,
            new Dictionary<string, object?>(Diagnostics(), StringComparer.Ordinal)
            {
                ["CurrentChildState"] = currentResult.GateState.ToString(),
                ["CurrentChildReason"] = currentResult.ReasonCode.ToString()
            });
    }

    private IReadOnlyDictionary<string, object?> Diagnostics() =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Composition"] = "Sequence",
            ["CurrentIndex"] = _index,
            ["CurrentChild"] = _index < _children.Count ? _children[_index].Name : null,
            ["CompletedChildren"] = _completed.Select(x => x.GateName).ToArray(),
            ["Children"] = _children.Select(x => x.Name).ToArray()
        };
}
