using System.Diagnostics;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed record TemporalGateEngineOptions
{
    public TimeSpan MaximumDuration { get; init; } = TimeSpan.FromSeconds(30);
}

public sealed class TemporalGateRun : IAsyncDisposable
{
    public TemporalGateRun(
        TemporalGateSession session,
        TemporalGateResult result,
        IReadOnlyList<GateTransitionRecord> timeline,
        IReadOnlyList<double> observationDurationsMs,
        long droppedObservations,
        long droppedTimelineEntries,
        int maximumConcurrentAnalysis,
        long stableFrames,
        long transitionFrames,
        long freezeEvents,
        long resolutionChanges,
        IReadOnlyDictionary<string, RegionalMetricSamples> regionalMetricSamples)
    {
        Session = session;
        Result = result;
        Timeline = timeline;
        ObservationDurationsMs = observationDurationsMs;
        DroppedObservations = droppedObservations;
        DroppedTimelineEntries = droppedTimelineEntries;
        MaximumConcurrentAnalysis = maximumConcurrentAnalysis;
        StableFrames = stableFrames;
        TransitionFrames = transitionFrames;
        FreezeEvents = freezeEvents;
        ResolutionChanges = resolutionChanges;
        RegionalMetricSamples = regionalMetricSamples;
    }

    public TemporalGateSession Session { get; }
    public TemporalGateResult Result { get; }
    public IReadOnlyList<GateTransitionRecord> Timeline { get; }
    public IReadOnlyList<double> ObservationDurationsMs { get; }
    public long DroppedObservations { get; }
    public long DroppedTimelineEntries { get; }
    public int MaximumConcurrentAnalysis { get; }
    public long StableFrames { get; }
    public long TransitionFrames { get; }
    public long FreezeEvents { get; }
    public long ResolutionChanges { get; }
    public IReadOnlyDictionary<string, RegionalMetricSamples> RegionalMetricSamples { get; }

    public ValueTask DisposeAsync() => Session.DisposeAsync();
}

public sealed class TemporalGateEngine
{
    private readonly GateProfile _profile;
    private readonly TemporalGateEngineOptions _options;

    public TemporalGateEngine(GateProfile profile, TemporalGateEngineOptions? options = null)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profile.Validate();
        _options = options ?? new TemporalGateEngineOptions();
        if (_options.MaximumDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    public Task<TemporalGateRun> RunAsync(
        IFrameLeaseSource frameSource,
        CancellationToken cancellationToken = default) =>
        RunAsync(frameSource, GateFactory.Create(_profile), cancellationToken);

    public async Task<TemporalGateRun> RunAsync(
        IFrameLeaseSource frameSource,
        ITemporalGate gate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frameSource);
        ArgumentNullException.ThrowIfNull(gate);
        var timeout = TimeSpan.FromMilliseconds(Math.Min(
            _options.MaximumDuration.TotalMilliseconds,
            MaximumProfileDuration().TotalMilliseconds));
        var session = new TemporalGateSession(
            gate.Name,
            timeout,
            _profile.SessionHistoryCapacity,
            _profile.MaximumObservedFrameIds);
        var timeline = new List<GateTransitionRecord>();
        var durations = new List<double>();
        var observer = new MultiRegionTemporalObserver(_profile.Regions, _profile.Observer);
        TemporalGateResult? finalResult = null;
        TemporalGateState? previousState = null;
        GateReasonCode? previousReason = null;
        string? previousPhase = null;
        FrameId? lastSourceFrameId = null;
        long rejectedBySession = 0;
        long droppedTimelineEntries = 0;
        long stableFrames = 0;
        long transitionFrames = 0;
        long freezeEvents = 0;
        long resolutionChanges = 0;
        var regionalMetrics = new Dictionary<string, RegionMetricAccumulator>(StringComparer.Ordinal);

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            await foreach (var sourceLease in frameSource.ReadAsync(linkedCts.Token).ConfigureAwait(false))
            {
                if (lastSourceFrameId.HasValue && sourceLease.Metadata.Id.CompareTo(lastSourceFrameId.Value) <= 0)
                {
                    sourceLease.Dispose();
                    rejectedBySession++;
                    continue;
                }

                lastSourceFrameId = sourceLease.Metadata.Id;
                var retained = new RetainedFrame(sourceLease);
                TemporalFrameObservation observation;
                try
                {
                    observation = await observer.AnalyzeAsync(retained, linkedCts.Token).ConfigureAwait(false);
                }
                catch
                {
                    retained.Dispose();
                    throw;
                }

                durations.Add(observation.ObservationDuration.TotalMilliseconds);
                foreach (var region in observation.Regions.Values)
                {
                    if (!regionalMetrics.TryGetValue(region.RegionName, out var accumulator))
                    {
                        accumulator = new RegionMetricAccumulator();
                        regionalMetrics.Add(region.RegionName, accumulator);
                    }

                    accumulator.Add(region);
                }
                if (observation.IsLikelyStable)
                {
                    stableFrames++;
                }

                if (observation.IsLikelyTransitioning)
                {
                    transitionFrames++;
                }

                if ((observation.QualityFlags & TemporalQualityFlags.StreamFrozen) != 0)
                {
                    freezeEvents++;
                }

                if ((observation.QualityFlags & TemporalQualityFlags.ResolutionChanged) != 0)
                {
                    resolutionChanges++;
                }

                if (!session.TryAdd(observation, retained, out var rejectionReason))
                {
                    rejectedBySession++;
                    if (rejectionReason == GateReasonCode.OutOfOrderFrame)
                    {
                        continue;
                    }

                    break;
                }

                var result = gate.Observe(session, observation);
                var phase = result.Diagnostics.TryGetValue("Phase", out var phaseValue)
                    ? Convert.ToString(phaseValue, System.Globalization.CultureInfo.InvariantCulture)
                    : null;
                if (result.GateState != previousState || result.ReasonCode != previousReason || !string.Equals(phase, previousPhase, StringComparison.Ordinal))
                {
                    if (timeline.Count >= _profile.MaximumObservedFrameIds)
                    {
                        timeline.RemoveAt(0);
                        droppedTimelineEntries++;
                    }

                    timeline.Add(new GateTransitionRecord(
                        observation.UtcTimestamp,
                        observation.FrameId,
                        gate.Name,
                        result.GateState,
                        result.ReasonCode,
                        phase,
                        result.Diagnostics));
                    previousState = result.GateState;
                    previousReason = result.ReasonCode;
                    previousPhase = phase;
                }

                if (result.IsTerminal)
                {
                    finalResult = result;
                    session.TryComplete(result);
                    break;
                }
            }

            if (finalResult is null)
            {
                finalResult = gate.Complete(session, GateTermination.StreamEnded, DateTimeOffset.UtcNow);
                session.TryComplete(finalResult);
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            finalResult = gate.Complete(session, GateTermination.Timeout, DateTimeOffset.UtcNow);
            session.TryComplete(finalResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            finalResult = gate.Complete(session, GateTermination.Cancelled, DateTimeOffset.UtcNow);
            session.TryComplete(finalResult);
        }
        catch (Exception error)
        {
            finalResult = gate.Complete(session, GateTermination.Faulted, DateTimeOffset.UtcNow, error);
            session.TryComplete(finalResult);
        }
        finally
        {
            await observer.DisposeAsync().ConfigureAwait(false);
        }

        Debug.Assert(finalResult is not null, "A gate run must always produce a result.");
        return new TemporalGateRun(
            session,
            finalResult!,
            timeline.ToArray(),
            durations.ToArray(),
            frameSource.DroppedFrames + rejectedBySession,
            droppedTimelineEntries,
            observer.MaximumConcurrentAnalysis,
            stableFrames,
            transitionFrames,
            freezeEvents,
            resolutionChanges,
            regionalMetrics.ToDictionary(x => x.Key, x => x.Value.ToSamples(), StringComparer.Ordinal));
    }

    private TimeSpan MaximumProfileDuration() => _profile.Kind switch
    {
        GateProfileKind.StableRegion => _profile.Stable.MaximumObservationDuration,
        GateProfileKind.TransitionDetected or GateProfileKind.TransitionCompleted => _profile.Transition.MaximumObservationDuration,
        _ => _options.MaximumDuration
    };
}

internal sealed class RegionMetricAccumulator
{
    private readonly List<double> _motion = new();
    private readonly List<double> _difference = new();
    private readonly List<double> _similarity = new();
    private readonly List<double> _sharpness = new();

    public void Add(RegionalFrameObservation observation)
    {
        _motion.Add(observation.MotionScore);
        _difference.Add(observation.DifferenceScore);
        _similarity.Add(observation.SimilarityScore);
        _sharpness.Add(observation.SharpnessScore);
    }

    public RegionalMetricSamples ToSamples() => new()
    {
        Motion = _motion.ToArray(),
        Difference = _difference.ToArray(),
        Similarity = _similarity.ToArray(),
        Sharpness = _sharpness.ToArray()
    };
}
