using System.Collections.ObjectModel;
using System.Diagnostics;
using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public sealed class MultiRegionTemporalObserver : IAsyncDisposable
{
    private readonly IReadOnlyList<RegionDefinition> _regions;
    private readonly TemporalObserverOptions _options;
    private readonly IRegionalObservationAnalyzer _analyzer;
    private readonly SemaphoreSlim _analysisSlots;
    private IFrameLease? _previousLease;
    private TemporalFrameObservation? _previousObservation;
    private int _sameSourceTickFrames;
    private int _activeAnalysis;
    private int _maximumConcurrentAnalysis;

    public MultiRegionTemporalObserver(
        IReadOnlyList<RegionDefinition> regions,
        TemporalObserverOptions? options = null,
        IRegionalObservationAnalyzer? analyzer = null)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (regions.Count == 0)
        {
            throw new ArgumentException("At least one region is required.", nameof(regions));
        }

        foreach (var region in regions)
        {
            region.Validate();
        }

        _regions = regions.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        _options = options ?? new TemporalObserverOptions();
        if (_options.MaxConcurrentAnalysis < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        _analyzer = analyzer ?? new CpuRegionalObservationAnalyzer();
        _analysisSlots = new SemaphoreSlim(_options.MaxConcurrentAnalysis, _options.MaxConcurrentAnalysis);
    }

    public int MaximumConcurrentAnalysis => Volatile.Read(ref _maximumConcurrentAnalysis);

    public async ValueTask<TemporalFrameObservation> AnalyzeAsync(
        RetainedFrame currentFrame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentFrame);
        var stopwatch = Stopwatch.StartNew();
        using var current = currentFrame.Acquire();
        var previous = _previousLease;
        var descriptor = current.Metadata.Descriptor;
        var resolutionChanged = previous is not null && previous.Metadata.Descriptor != descriptor;

        var tasks = _regions
            .Select(definition => AnalyzeRegionAsync(definition, current, previous, cancellationToken))
            .ToArray();
        var regionalResults = await Task.WhenAll(tasks).ConfigureAwait(false);
        var regional = new ReadOnlyDictionary<string, RegionalFrameObservation>(
            regionalResults.ToDictionary(x => x.RegionName, x => x, StringComparer.Ordinal));

        var fullFrame = regional.TryGetValue("FullFrame", out var configuredFullFrame)
            ? configuredFullFrame
            : regionalResults.First();
        var required = regionalResults.Where(x => x.StabilityRole == RegionStabilityRole.Required).ToArray();
        if (required.Length == 0)
        {
            required = new[] { fullFrame };
        }

        var requiredStable = required.All(x => x.IsLikelyStable);
        var requiredTransitioning = required.Any(x => x.IsLikelyTransitioning);
        TimeSpan? interval = _previousObservation is null
            ? null
            : current.Metadata.Timestamp.MonotonicSinceStart - _previousObservation.MonotonicTimestamp;

        if (_previousObservation is not null &&
            current.Metadata.Timestamp.SourceTicks == _previousObservation.SourceTicks &&
            fullFrame.VisualFingerprint == _previousObservation.VisualFingerprint)
        {
            _sameSourceTickFrames++;
        }
        else
        {
            _sameSourceTickFrames = 0;
        }

        var freezeByInterval = interval.HasValue && interval.Value > _options.FreezeIntervalThreshold;
        var freezeBySource = _sameSourceTickFrames >= _options.FrozenSourceTimestampFrames;
        var freezeScore = freezeByInterval || freezeBySource ? 1d : 0d;
        var flags = TemporalQualityFlags.None;

        if (previous is null)
        {
            flags |= TemporalQualityFlags.MissingPreviousFrame;
        }

        if (fullFrame.SharpnessScore < _options.MinimumSharpness)
        {
            flags |= TemporalQualityFlags.LowSharpness;
        }

        if (fullFrame.BrightnessScore < 0.05)
        {
            flags |= TemporalQualityFlags.LowBrightness;
        }
        else if (fullFrame.BrightnessScore > 0.95)
        {
            flags |= TemporalQualityFlags.HighBrightness;
        }

        if (fullFrame.ContrastScore < 0.04)
        {
            flags |= TemporalQualityFlags.LowContrast;
        }

        if (resolutionChanged)
        {
            flags |= TemporalQualityFlags.ResolutionChanged;
        }

        if (freezeByInterval || freezeBySource)
        {
            flags |= TemporalQualityFlags.StreamFrozen;
        }

        if (freezeBySource)
        {
            flags |= TemporalQualityFlags.SourceTimestampStalled;
        }

        stopwatch.Stop();
        var observation = new TemporalFrameObservation
        {
            FrameId = current.Metadata.Id,
            SourceTicks = current.Metadata.Timestamp.SourceTicks,
            MonotonicTimestamp = current.Metadata.Timestamp.MonotonicSinceStart,
            UtcTimestamp = current.Metadata.Timestamp.CapturedAtUtc,
            FrameInterval = interval,
            GlobalDifferenceScore = fullFrame.DifferenceScore,
            RegionalDifferenceScores = new ReadOnlyDictionary<string, double>(
                regional.ToDictionary(x => x.Key, x => x.Value.DifferenceScore, StringComparer.Ordinal)),
            MotionScore = fullFrame.MotionScore,
            SharpnessScore = fullFrame.SharpnessScore,
            FreezeScore = freezeScore,
            BrightnessScore = fullFrame.BrightnessScore,
            ContrastScore = fullFrame.ContrastScore,
            Resolution = FrameResolution.From(descriptor),
            IsLikelyStable = requiredStable && !freezeByInterval && !freezeBySource && !resolutionChanged,
            IsLikelyTransitioning = requiredTransitioning,
            QualityFlags = flags,
            Regions = regional,
            VisualFingerprint = fullFrame.VisualFingerprint,
            ObservationDuration = stopwatch.Elapsed
        };

        var nextPrevious = currentFrame.Acquire();
        Interlocked.Exchange(ref _previousLease, nextPrevious)?.Dispose();
        _previousObservation = observation;
        return observation;
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Exchange(ref _previousLease, null)?.Dispose();
        _analysisSlots.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task<RegionalFrameObservation> AnalyzeRegionAsync(
        RegionDefinition definition,
        IFrameLease current,
        IFrameLease? previous,
        CancellationToken cancellationToken)
    {
        await _analysisSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        var active = Interlocked.Increment(ref _activeAnalysis);
        UpdateMaximum(active);
        try
        {
            RegionalFrameObservation? priorRegion = null;
            if (_previousObservation is not null &&
                _previousObservation.Regions.TryGetValue(definition.Name, out var foundPriorRegion))
            {
                priorRegion = foundPriorRegion;
            }

            return await Task.Run(
                () => _analyzer.Analyze(definition, current, previous, priorRegion, _options),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _activeAnalysis);
            _analysisSlots.Release();
        }
    }

    private void UpdateMaximum(int candidate)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maximumConcurrentAnalysis);
            if (candidate <= current)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _maximumConcurrentAnalysis, candidate, current) == current)
            {
                return;
            }
        }
    }
}
