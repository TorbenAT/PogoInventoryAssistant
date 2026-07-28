namespace PogoInventory.Streaming.Semantics;

public sealed class FailClosedFieldConsensusGate<T> : IFieldConsensusGate<T>
{
    public FieldReading<T> Resolve(string fieldName, string identityKey, IReadOnlyList<FieldEvidence<T>> evidence, FieldConsensusOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(identityKey);
        ArgumentNullException.ThrowIfNull(evidence);
        options.Validate();
        var valid = evidence.Where(x => x.Status == FieldReadingStatus.Known && x.Value is not null && x.Confidence >= options.MinimumConfidence).ToArray();
        if (evidence.Any(x => x.Status == FieldReadingStatus.Occluded)) return Reading(FieldReadingStatus.Occluded, default, "OCCLUDED_EVIDENCE_PRESENT", evidence.ToArray());
        var hashes = valid.Select(x => x.Value!).GroupBy(x => x, EqualityComparer<T>.Default).OrderByDescending(g => g.Count()).ThenBy(g => Convert.ToString(g.Key, System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal).ToArray();
        if (valid.Length == 0) return Reading(FieldReadingStatus.Unknown, default, "NO_HIGH_CONFIDENCE_EVIDENCE", Array.Empty<FieldEvidence<T>>());
        if (hashes.Length > 1 && hashes[0].Count() == hashes[1].Count()) return Reading(FieldReadingStatus.Conflicting, default, "HIGH_CONFIDENCE_CONFLICT", valid);
        var winnerValue = hashes[0].Key;
        var winner = valid.Where(x => EqualityComparer<T>.Default.Equals(x.Value!, winnerValue)).ToArray();
        if (winner.Length < options.RequiredAgreement) return Reading(FieldReadingStatus.Unknown, default, "INSUFFICIENT_AGREEMENT", valid);
        return Reading(FieldReadingStatus.Known, winnerValue, "CONSENSUS_AGREEMENT", winner);

        FieldReading<T> Reading(FieldReadingStatus status, T? value, string reason, IReadOnlyList<FieldEvidence<T>> selected) => new(fieldName, status, value, selected.Count == 0 ? 0 : selected.Average(x => x.Confidence), selected.Select(x => x.FrameId).Order().ToArray(), selected.Select(x => x.EvidenceHash).Order(StringComparer.Ordinal).ToArray(), reason, new Dictionary<string, string> { ["identityKey"] = identityKey, ["evidenceCount"] = evidence.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) });
    }
}
