using PogoInventory.Streaming;

namespace PogoInventory.Streaming.Gates;

public enum AppraisalHandoffStatus { Pending, Ready, NoEffectOrFilterEnd, UnknownStop }

public sealed record AppraisalHandoffSnapshot(AppraisalHandoffStatus Status, IReadOnlyList<FrameId> QualifiedFrameIds, IReadOnlyDictionary<string, int> ReasonCounts, string? PreviousFingerprint, string? CurrentFingerprint, string? NewFingerprint);

/// <summary>Pure bounded decision state for stream-only AppraisalBars settling.</summary>
public sealed class AppraisalHandoffEvaluator
{
    private readonly StableRegionGateOptions _options;
    private readonly IReadOnlyList<RegionDefinition> _regions;
    private readonly string? _previousFingerprint;
    private readonly IReadOnlyDictionary<string, ulong>? _previousRegions;
    private readonly Queue<(
        FrameId Id,
        TimeSpan Timestamp,
        IReadOnlyDictionary<string, ulong> Regions)> _qualified = new();
    private readonly Dictionary<string, int> _reasons = new(StringComparer.Ordinal);
    private string? _changedFingerprint;
    private IReadOnlyDictionary<string, ulong>? _candidateRegions;
    private string? _currentFingerprint;
    private bool _sawPreviousFingerprint;
    private bool _sawPostActionTransition;

    public AppraisalHandoffEvaluator(
        StableRegionGateOptions options,
        IReadOnlyList<RegionDefinition> regions,
        string? previousFingerprint = null,
        bool postActionTransitionObserved = false)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _regions = regions ?? throw new ArgumentNullException(nameof(regions));
        _previousFingerprint = previousFingerprint;
        _previousRegions = previousFingerprint is null ? null : ParseFingerprint(previousFingerprint);
        _sawPostActionTransition = postActionTransitionObserved;
        if (postActionTransitionObserved)
        {
            Count("ActionTransitionObserved");
        }
    }

    public AppraisalHandoffSnapshot Observe(TemporalFrameObservation observation, bool isAppraisalBars)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!isAppraisalBars) { Count("WrongState"); return Snapshot(AppraisalHandoffStatus.Pending); }
        var evaluation = GateEvaluation.EvaluateStableRegions(observation, _options, _regions);
        if (!evaluation.IsStable)
        {
            Count(evaluation.ReasonCode.ToString());
            if (evaluation.Diagnostics.TryGetValue("FailedRegion", out var failedRegion) &&
                failedRegion is string regionName)
            {
                Count($"{evaluation.ReasonCode}:{regionName}");
            }
            if (_previousRegions is not null &&
                (observation.QualityFlags &
                    TemporalQualityFlags.MissingPreviousFrame) == 0 &&
                (observation.IsLikelyTransitioning ||
                 evaluation.ReasonCode is GateReasonCode.MotionTooHigh or
                     GateReasonCode.DifferenceTooHigh or
                     GateReasonCode.SimilarityTooLow))
            {
                _sawPostActionTransition = true;
                _candidateRegions = null;
                _changedFingerprint = null;
                _qualified.Clear();
            }
            return Snapshot(AppraisalHandoffStatus.Pending);
        }

        var regions = VisualFingerprint.CaptureRegions(observation, _options.RequiredRegions);
        var fingerprint = FormatFingerprint(regions);
        _currentFingerprint = fingerprint;
        if (_previousRegions is not null && !_sawPostActionTransition)
        {
            if (VisualFingerprint.RegionalSimilarity(_previousRegions, observation) >=
                _options.MinimumSimilarityScore)
            {
                _sawPreviousFingerprint = true;
                Count("PreviousFingerprint");
            }
            else
            {
                Count("CandidateBeforeTransition");
            }
            return Snapshot(AppraisalHandoffStatus.Pending);
        }

        if (_candidateRegions is null ||
            VisualFingerprint.RegionalSimilarity(_candidateRegions, observation) <
                _options.MinimumSimilarityScore)
        {
            if (_candidateRegions is not null)
            {
                Count("CandidateFingerprintChanged");
            }
            _candidateRegions = regions;
            _changedFingerprint = null;
            _qualified.Clear();
        }

        if (_qualified.All(x => x.Id != observation.FrameId))
        {
            if (_qualified.TryPeek(out var first) &&
                _qualified.Last() is var last)
            {
                if (observation.FrameId.Value - last.Id.Value <
                    _options.MinimumEvidenceFrameIdDistance)
                {
                    Count("EvidenceFrameIdTooClose");
                    return Snapshot(AppraisalHandoffStatus.Pending);
                }

                if (observation.MonotonicTimestamp - last.Timestamp <
                    _options.MinimumEvidenceTimeDistance)
                {
                    Count("EvidenceTimeTooClose");
                    return Snapshot(AppraisalHandoffStatus.Pending);
                }

                if (_options.MaximumEvidenceVisualSimilarity < 1 &&
                    RegionalSimilarity(last.Regions, regions) >
                    _options.MaximumEvidenceVisualSimilarity)
                {
                    Count("EvidenceVisualSimilarityTooHigh");
                    return Snapshot(AppraisalHandoffStatus.Pending);
                }

                _qualified.Enqueue((
                    observation.FrameId,
                    observation.MonotonicTimestamp,
                    regions));

                if (_qualified.Count >= _options.MinimumStableFrames &&
                    observation.MonotonicTimestamp - first.Timestamp <
                    _options.MinimumStableDuration)
                {
                    Count("StableDurationTooShort");
                    return Snapshot(AppraisalHandoffStatus.Pending);
                }
            }
            else
            {
                _qualified.Enqueue((
                    observation.FrameId,
                    observation.MonotonicTimestamp,
                    regions));
            }
        }

        if (_qualified.Count < _options.MinimumStableFrames)
        {
            return Snapshot(AppraisalHandoffStatus.Pending);
        }

        var canonical = CanonicalFingerprint(_qualified.Select(x => x.Regions));
        _currentFingerprint = canonical;
        _changedFingerprint = _previousFingerprint is null ? null : canonical;
        if (_previousRegions is not null &&
            RegionalSimilarity(_previousRegions, ParseFingerprint(canonical)) >=
                _options.MinimumSimilarityScore)
        {
            Count("SemanticProgressionProofRequired");
        }
        return Snapshot(AppraisalHandoffStatus.Ready);
    }

    public AppraisalHandoffSnapshot CompleteTimeout() =>
        _previousFingerprint is not null && _sawPreviousFingerprint &&
        _qualified.Count == 0 && _changedFingerprint is null
            ? Snapshot(AppraisalHandoffStatus.NoEffectOrFilterEnd) : Snapshot(AppraisalHandoffStatus.UnknownStop);

    private AppraisalHandoffSnapshot Snapshot(AppraisalHandoffStatus status) => new(status, _qualified.Select(x => x.Id).ToArray(), new Dictionary<string, int>(_reasons, StringComparer.Ordinal), _previousFingerprint, _currentFingerprint, _changedFingerprint);
    private void Count(string reason) => _reasons[reason] = _reasons.TryGetValue(reason, out var count) ? count + 1 : 1;

    public static string StableFingerprint(TemporalFrameObservation observation, IEnumerable<string> requiredRegions) =>
        FormatFingerprint(VisualFingerprint.CaptureRegions(observation, requiredRegions));

    private static string CanonicalFingerprint(
        IEnumerable<IReadOnlyDictionary<string, ulong>> frames)
    {
        var all = frames.ToArray();
        var canonical = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var name in all[0].Keys.OrderBy(x => x, StringComparer.Ordinal))
        {
            ulong value = 0;
            for (var bit = 0; bit < 64; bit++)
            {
                var mask = 1UL << bit;
                if (all.Count(x => (x[name] & mask) != 0) * 2 >= all.Length)
                {
                    value |= mask;
                }
            }
            canonical.Add(name, value);
        }
        return FormatFingerprint(canonical);
    }

    private static string FormatFingerprint(IReadOnlyDictionary<string, ulong> regions) =>
        string.Join("|", regions.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}:{x.Value:x16}"));

    private static double RegionalSimilarity(
        IReadOnlyDictionary<string, ulong> baseline,
        IReadOnlyDictionary<string, ulong> candidate)
    {
        var similarities = baseline
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => candidate.TryGetValue(x.Key, out var value)
                ? VisualFingerprint.Similarity(x.Value, value)
                : 0)
            .ToArray();
        return similarities.Length == 0 ? 0 : similarities.Average();
    }

    private static IReadOnlyDictionary<string, ulong> ParseFingerprint(string fingerprint)
    {
        var regions = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var part in fingerprint.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf(':');
            if (separator <= 0 ||
                !ulong.TryParse(part[(separator + 1)..],
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                throw new ArgumentException("Previous fingerprint has an invalid format.", nameof(fingerprint));
            }
            regions.Add(part[..separator], value);
        }
        return regions;
    }
}
