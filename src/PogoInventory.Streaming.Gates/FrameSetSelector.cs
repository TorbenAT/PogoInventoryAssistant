using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public enum FrameRole
{
    BestHeaderFrame = 0,
    BestModelFrame = 1,
    BestPanelFrame = 2,
    BestOverallStableFrame = 3,
    PreTransitionFrame = 4,
    TransitionFrame = 5,
    PostTransitionFrame = 6,
    ConfirmationFrame = 7
}

public enum FrameSetFailure
{
    None = 0,
    NoFrames = 1,
    RequiredRegionMissing = 2,
    StabilityNotEstablished = 3,
    TransitionNotDetected = 4,
    PostTransitionStabilityNotEstablished = 5,
    DiversityRequirementNotMet = 6,
    FrameLeaseFailed = 7
}

public sealed record FrameSetRequest
{
    public IReadOnlyList<FrameRole> Roles { get; init; } = Enum.GetValues<FrameRole>();
    public required StableRegionGateOptions StableOptions { get; init; }
    public required TransitionGateOptions TransitionOptions { get; init; }
    public FrameDiversityOptions Diversity { get; init; } = new();
}

public sealed record SelectedFrame
{
    public required FrameRole Role { get; init; }
    public required FrameId FrameId { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required double Score { get; init; }
    public required IFrameLease Lease { get; init; }
}

public sealed class SelectedFrameSet : IDisposable
{
    private IReadOnlyDictionary<FrameRole, SelectedFrame>? _frames;

    public SelectedFrameSet(
        IReadOnlyDictionary<FrameRole, SelectedFrame> frames,
        IReadOnlyDictionary<FrameRole, FrameSetFailure> failures)
    {
        _frames = frames;
        Failures = failures;
    }

    public IReadOnlyDictionary<FrameRole, SelectedFrame> Frames =>
        _frames ?? throw new ObjectDisposedException(nameof(SelectedFrameSet));

    public IReadOnlyDictionary<FrameRole, FrameSetFailure> Failures { get; }

    public void Dispose()
    {
        var frames = Interlocked.Exchange(ref _frames, null);
        if (frames is null)
        {
            return;
        }

        foreach (var frame in frames.Values)
        {
            frame.Lease.Dispose();
        }
    }
}

public interface IFrameSetSelector
{
    ValueTask<SelectedFrameSet> SelectAsync(
        TemporalGateSession session,
        FrameSetRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FrameSetSelector : IFrameSetSelector
{
    public ValueTask<SelectedFrameSet> SelectAsync(
        TemporalGateSession session,
        FrameSetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var records = session.SnapshotRecords().Where(x => x.Frame is not null).ToArray();
        var selected = new Dictionary<FrameRole, SelectedFrame>();
        var failures = new Dictionary<FrameRole, FrameSetFailure>();

        foreach (var role in request.Roles.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = SelectCandidate(role, records, request, selected);
            if (candidate.Record is null)
            {
                failures[role] = candidate.Failure;
                continue;
            }

            try
            {
                selected[role] = new SelectedFrame
                {
                    Role = role,
                    FrameId = candidate.Record.Observation.FrameId,
                    TimestampUtc = candidate.Record.Observation.UtcTimestamp,
                    Score = candidate.Score,
                    Lease = candidate.Record.Frame!.Acquire()
                };
                failures[role] = FrameSetFailure.None;
            }
            catch (ObjectDisposedException)
            {
                failures[role] = FrameSetFailure.FrameLeaseFailed;
            }
        }

        return ValueTask.FromResult(
            new SelectedFrameSet(
                new Dictionary<FrameRole, SelectedFrame>(selected),
                new Dictionary<FrameRole, FrameSetFailure>(failures)));
    }

    private static CandidateSelection SelectCandidate(
        FrameRole role,
        IReadOnlyList<GateFrameRecord> records,
        FrameSetRequest request,
        IReadOnlyDictionary<FrameRole, SelectedFrame> alreadySelected)
    {
        if (records.Count == 0)
        {
            return new CandidateSelection(null, 0, FrameSetFailure.NoFrames);
        }

        return role switch
        {
            FrameRole.BestHeaderFrame => BestRegion(records, "Header", false, alreadySelected),
            FrameRole.BestModelFrame => BestRegion(records, "Model", true),
            FrameRole.BestPanelFrame => BestRegion(records, "AppraisalPanel", false, alreadySelected),
            FrameRole.BestOverallStableFrame => BestStable(records, request.StableOptions, alreadySelected),
            FrameRole.PreTransitionFrame => PreTransition(records, request),
            FrameRole.TransitionFrame => Transition(records, request),
            FrameRole.PostTransitionFrame => PostTransition(records, request),
            FrameRole.ConfirmationFrame => Confirmation(records, request, alreadySelected),
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }

    private static CandidateSelection BestRegion(
        IReadOnlyList<GateFrameRecord> records,
        string regionName,
        bool allowVolatileMotion,
        IReadOnlyDictionary<FrameRole, SelectedFrame>? alreadySelected = null)
    {
        var selectedIds = alreadySelected?.Values.Select(x => x.FrameId).ToHashSet() ?? [];
        var candidates = records
            .Where(x => !selectedIds.Contains(x.Observation.FrameId))
            .Where(x => x.Observation.TryGetRegion(regionName, out _))
            .Select(x =>
            {
                var region = x.Observation.Regions[regionName];
                var motionWeight = allowVolatileMotion ? 0.10 : 0.30;
                var score = (region.SharpnessScore * 0.40) +
                            (region.SimilarityScore * 0.20) +
                            ((1 - region.DifferenceScore) * 0.20) +
                            ((1 - region.MotionScore) * motionWeight) +
                            (region.ContrastScore * (0.20 - motionWeight));
                return new CandidateSelection(x, score, FrameSetFailure.None);
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Record!.Observation.FrameId.Value)
            .ToArray();
        return candidates.Length == 0
            ? new CandidateSelection(null, 0, FrameSetFailure.RequiredRegionMissing)
            : candidates[0];
    }

    private static CandidateSelection BestStable(
        IReadOnlyList<GateFrameRecord> records,
        StableRegionGateOptions options,
        IReadOnlyDictionary<FrameRole, SelectedFrame>? alreadySelected = null)
    {
        var selectedIds = alreadySelected?.Values.Select(x => x.FrameId).ToHashSet() ?? [];
        var candidates = records
            .Where(x => !selectedIds.Contains(x.Observation.FrameId))
            .Where(x => IsStable(x.Observation, options))
            .Select(x => new CandidateSelection(
                x,
                StableScore(x.Observation, options.RequiredRegions),
                FrameSetFailure.None))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Record!.Observation.FrameId.Value)
            .ToArray();
        return candidates.Length == 0
            ? new CandidateSelection(null, 0, FrameSetFailure.StabilityNotEstablished)
            : candidates[0];
    }

    private static CandidateSelection PreTransition(
        IReadOnlyList<GateFrameRecord> records,
        FrameSetRequest request)
    {
        var transitionIndex = FindTransitionIndex(records, request.TransitionOptions);
        if (transitionIndex < 0)
        {
            return new CandidateSelection(null, 0, FrameSetFailure.TransitionNotDetected);
        }

        for (var index = transitionIndex - 1; index >= 0; index--)
        {
            if (IsStable(records[index].Observation, request.StableOptions))
            {
                return new CandidateSelection(records[index], StableScore(records[index].Observation, request.StableOptions.RequiredRegions), FrameSetFailure.None);
            }
        }

        return new CandidateSelection(null, 0, FrameSetFailure.StabilityNotEstablished);
    }

    private static CandidateSelection Transition(
        IReadOnlyList<GateFrameRecord> records,
        FrameSetRequest request)
    {
        var candidates = records
            .Where(x => GateEvaluation.IsTransitionFrame(x.Observation, request.TransitionOptions, out _, out _))
            .Select(x =>
            {
                GateEvaluation.IsTransitionFrame(x.Observation, request.TransitionOptions, out var magnitude, out _);
                return new CandidateSelection(x, magnitude, FrameSetFailure.None);
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Record!.Observation.FrameId.Value)
            .ToArray();
        return candidates.Length == 0
            ? new CandidateSelection(null, 0, FrameSetFailure.TransitionNotDetected)
            : candidates[0];
    }

    private static CandidateSelection PostTransition(
        IReadOnlyList<GateFrameRecord> records,
        FrameSetRequest request)
    {
        var transitionIndex = FindTransitionIndex(records, request.TransitionOptions);
        if (transitionIndex < 0)
        {
            return new CandidateSelection(null, 0, FrameSetFailure.TransitionNotDetected);
        }

        for (var index = transitionIndex + 1; index < records.Count; index++)
        {
            if (IsStable(records[index].Observation, request.StableOptions))
            {
                return new CandidateSelection(records[index], StableScore(records[index].Observation, request.StableOptions.RequiredRegions), FrameSetFailure.None);
            }
        }

        return new CandidateSelection(null, 0, FrameSetFailure.PostTransitionStabilityNotEstablished);
    }

    private static CandidateSelection Confirmation(
        IReadOnlyList<GateFrameRecord> records,
        FrameSetRequest request,
        IReadOnlyDictionary<FrameRole, SelectedFrame> alreadySelected)
    {
        var anchor = alreadySelected.TryGetValue(FrameRole.PostTransitionFrame, out var post)
            ? post
            : alreadySelected.TryGetValue(FrameRole.BestOverallStableFrame, out var stable)
                ? stable
                : null;
        if (anchor is null)
        {
            return new CandidateSelection(null, 0, FrameSetFailure.StabilityNotEstablished);
        }

        var anchorRecord = records.FirstOrDefault(x => x.Observation.FrameId == anchor.FrameId);
        if (anchorRecord is null)
        {
            return new CandidateSelection(null, 0, FrameSetFailure.NoFrames);
        }

        var candidates = records
            .Where(x => x.Observation.FrameId.Value > anchor.FrameId.Value)
            .Where(x => IsStable(x.Observation, request.StableOptions))
            .Where(x => IsDiverse(
                anchorRecord.Observation,
                x.Observation,
                request.Diversity,
                request.StableOptions.RequiredRegions))
            .Select(x => new CandidateSelection(x, StableScore(x.Observation, request.StableOptions.RequiredRegions), FrameSetFailure.None))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Record!.Observation.FrameId.Value)
            .ToArray();
        return candidates.Length == 0
            ? new CandidateSelection(null, 0, FrameSetFailure.DiversityRequirementNotMet)
            : candidates[0];
    }

    private static int FindTransitionIndex(IReadOnlyList<GateFrameRecord> records, TransitionGateOptions options)
    {
        for (var index = 0; index < records.Count; index++)
        {
            if (GateEvaluation.IsTransitionFrame(records[index].Observation, options, out _, out _))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsStable(TemporalFrameObservation observation, StableRegionGateOptions options)
    {
        foreach (var name in options.RequiredRegions)
        {
            if (!observation.TryGetRegion(name, out var region) ||
                region.MotionScore > options.MaximumMotionScore ||
                region.DifferenceScore > options.MaximumDifferenceScore ||
                region.SimilarityScore < options.MinimumSimilarityScore ||
                region.SharpnessScore < options.MinimumSharpnessScore)
            {
                return false;
            }
        }

        return (observation.QualityFlags & (TemporalQualityFlags.StreamFrozen | TemporalQualityFlags.ResolutionChanged)) == 0;
    }

    private static double StableScore(TemporalFrameObservation observation, IReadOnlyList<string> regions)
    {
        var selected = regions
            .Where(observation.Regions.ContainsKey)
            .Select(name => observation.Regions[name])
            .ToArray();
        if (selected.Length == 0)
        {
            return 0;
        }

        return selected.Average(x =>
            (x.SharpnessScore * 0.35) +
            (x.SimilarityScore * 0.25) +
            ((1 - x.MotionScore) * 0.20) +
            ((1 - x.DifferenceScore) * 0.15) +
            (x.ContrastScore * 0.05));
    }

    private static bool IsDiverse(
        TemporalFrameObservation anchor,
        TemporalFrameObservation candidate,
        FrameDiversityOptions diversity,
        IReadOnlyList<string> fingerprintRegions)
    {
        if (candidate.FrameId.Value - anchor.FrameId.Value < diversity.MinimumFrameIdDistance)
        {
            return false;
        }

        if (candidate.UtcTimestamp - anchor.UtcTimestamp < diversity.MinimumTimeDistance)
        {
            return false;
        }

        if (diversity.MaximumVisualSimilarity >= 1)
        {
            return true;
        }

        return VisualFingerprint.RegionalSimilarity(anchor, candidate, fingerprintRegions) <=
               diversity.MaximumVisualSimilarity;
    }

    private sealed record CandidateSelection(GateFrameRecord? Record, double Score, FrameSetFailure Failure);
}
