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
    private readonly Queue<(FrameId Id, string Fingerprint)> _qualified = new();
    private readonly Dictionary<string, int> _reasons = new(StringComparer.Ordinal);
    private string? _changedFingerprint;
    private string? _currentFingerprint;

    public AppraisalHandoffEvaluator(StableRegionGateOptions options, IReadOnlyList<RegionDefinition> regions, string? previousFingerprint = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _regions = regions ?? throw new ArgumentNullException(nameof(regions));
        _previousFingerprint = previousFingerprint;
    }

    public AppraisalHandoffSnapshot Observe(TemporalFrameObservation observation, bool isAppraisalBars)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (!isAppraisalBars) { Count("WrongState"); return Snapshot(AppraisalHandoffStatus.Pending); }
        var evaluation = GateEvaluation.EvaluateStableRegions(observation, _options, _regions);
        if (!evaluation.IsStable) { Count(evaluation.ReasonCode.ToString()); return Snapshot(AppraisalHandoffStatus.Pending); }

        var fingerprint = StableFingerprint(observation, _options.RequiredRegions);
        _currentFingerprint = fingerprint;
        if (_qualified.All(x => x.Id != observation.FrameId))
        {
            _qualified.Enqueue((observation.FrameId, fingerprint));
            while (_qualified.Count > 5) _qualified.Dequeue();
        }
        if (_previousFingerprint is not null && !string.Equals(_previousFingerprint, fingerprint, StringComparison.Ordinal)) _changedFingerprint = fingerprint;
        return _qualified.Count >= 3 && (_previousFingerprint is null || _changedFingerprint is not null)
            ? Snapshot(AppraisalHandoffStatus.Ready) : Snapshot(AppraisalHandoffStatus.Pending);
    }

    public AppraisalHandoffSnapshot CompleteTimeout() =>
        _previousFingerprint is not null && _qualified.Count > 0 && _changedFingerprint is null
            ? Snapshot(AppraisalHandoffStatus.NoEffectOrFilterEnd) : Snapshot(AppraisalHandoffStatus.UnknownStop);

    private AppraisalHandoffSnapshot Snapshot(AppraisalHandoffStatus status) => new(status, _qualified.Select(x => x.Id).ToArray(), new Dictionary<string, int>(_reasons, StringComparer.Ordinal), _previousFingerprint, _currentFingerprint, _changedFingerprint);
    private void Count(string reason) => _reasons[reason] = _reasons.TryGetValue(reason, out var count) ? count + 1 : 1;

    public static string StableFingerprint(TemporalFrameObservation observation, IEnumerable<string> requiredRegions) =>
        string.Join("|", VisualFingerprint.CaptureRegions(observation, requiredRegions).OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}:{x.Value:x16}"));
}
