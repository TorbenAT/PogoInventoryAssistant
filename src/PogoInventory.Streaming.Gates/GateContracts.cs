using System.Collections.ObjectModel;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public enum TemporalGateState
{
    Pending = 0,
    Passed = 1,
    Rejected = 2,
    TimedOut = 3,
    Cancelled = 4,
    Faulted = 5
}

public enum GateReasonCode
{
    Pending = 0,
    Passed = 1,
    InsufficientStableFrames = 2,
    MotionTooHigh = 3,
    DifferenceTooHigh = 4,
    SharpnessTooLow = 5,
    SimilarityTooLow = 6,
    RequiredRegionMissing = 7,
    RequiredRegionContaminatedByMotion = 8,
    ResolutionChanged = 9,
    ObservationTimedOut = 10,
    StreamFrozen = 11,
    TransitionNotDetected = 12,
    TransitionDidNotComplete = 13,
    PostTransitionStabilityNotEstablished = 14,
    NoMeaningfulVisualProgression = 15,
    NoFramesReceived = 16,
    InsufficientEvidence = 17,
    ObservationQueueOverflow = 18,
    StabilityNotEstablished = 19,
    FrameLeaseFailed = 20,
    OutOfOrderFrame = 21,
    Cancelled = 22,
    Faulted = 23,
    SequenceChildFailed = 24,
    CompositeRequirementFailed = 25
}

public enum GateTermination
{
    Timeout = 0,
    Cancelled = 1,
    StreamEnded = 2,
    Faulted = 3
}

public sealed record TemporalGateResult
{
    public required string GateName { get; init; }
    public required TemporalGateState GateState { get; init; }
    public required DateTimeOffset StartTimestamp { get; init; }
    public DateTimeOffset? EndTimestamp { get; init; }
    public required TimeSpan Duration { get; init; }
    public required IReadOnlyList<FrameId> ObservedFrameIds { get; init; }
    public required IReadOnlyList<FrameId> SelectedEvidenceFrameIds { get; init; }
    public required double Confidence { get; init; }
    public required GateReasonCode ReasonCode { get; init; }
    public required IReadOnlyDictionary<string, object?> Diagnostics { get; init; }

    public bool IsTerminal => GateState is not TemporalGateState.Pending;

    public static TemporalGateResult Pending(
        string gateName,
        TemporalGateSession session,
        GateReasonCode reasonCode,
        double confidence,
        IReadOnlyDictionary<string, object?>? diagnostics = null,
        IReadOnlyList<FrameId>? selectedEvidence = null) =>
        Create(gateName, session, TemporalGateState.Pending, reasonCode, confidence, null, diagnostics, selectedEvidence);

    public static TemporalGateResult Terminal(
        string gateName,
        TemporalGateSession session,
        TemporalGateState state,
        GateReasonCode reasonCode,
        double confidence,
        DateTimeOffset endTimestamp,
        IReadOnlyDictionary<string, object?>? diagnostics = null,
        IReadOnlyList<FrameId>? selectedEvidence = null)
    {
        if (state == TemporalGateState.Pending)
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        return Create(gateName, session, state, reasonCode, confidence, endTimestamp, diagnostics, selectedEvidence);
    }

    private static TemporalGateResult Create(
        string gateName,
        TemporalGateSession session,
        TemporalGateState state,
        GateReasonCode reasonCode,
        double confidence,
        DateTimeOffset? endTimestamp,
        IReadOnlyDictionary<string, object?>? diagnostics,
        IReadOnlyList<FrameId>? selectedEvidence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gateName);
        ArgumentNullException.ThrowIfNull(session);

        var end = endTimestamp ?? session.LastObservationUtc ?? session.StartUtc;
        var duration = end <= session.StartUtc ? TimeSpan.Zero : end - session.StartUtc;
        return new TemporalGateResult
        {
            GateName = gateName,
            GateState = state,
            StartTimestamp = session.StartUtc,
            EndTimestamp = endTimestamp,
            Duration = duration,
            ObservedFrameIds = session.ObservedFrameIds,
            SelectedEvidenceFrameIds = selectedEvidence ?? Array.Empty<FrameId>(),
            Confidence = Math.Clamp(confidence, 0, 1),
            ReasonCode = reasonCode,
            Diagnostics = diagnostics is null
                ? EmptyDiagnostics
                : new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(diagnostics, StringComparer.Ordinal))
        };
    }

    private static IReadOnlyDictionary<string, object?> EmptyDiagnostics { get; } =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(StringComparer.Ordinal));
}

public interface ITemporalGate<TObservation, TResult>
{
    string Name { get; }
    TResult Observe(TemporalGateSession session, TObservation observation);
    TResult Complete(TemporalGateSession session, GateTermination termination, DateTimeOffset timestamp, Exception? error = null);
}

public interface ITemporalGate : ITemporalGate<TemporalFrameObservation, TemporalGateResult>
{
}

public sealed record GateTransitionRecord(
    DateTimeOffset TimestampUtc,
    FrameId? FrameId,
    string GateName,
    TemporalGateState State,
    GateReasonCode ReasonCode,
    string? Phase,
    IReadOnlyDictionary<string, object?> Diagnostics);
