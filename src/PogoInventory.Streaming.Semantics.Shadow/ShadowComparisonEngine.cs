using PogoInventory.Streaming.Semantics;

namespace PogoInventory.Streaming.Semantics.Shadow;

public sealed class ShadowComparisonEngine
{
    public IReadOnlyList<ShadowFieldComparison> Compare(
        IReadOnlyList<ShadowFieldCandidate> candidates,
        IReadOnlyList<ShadowReferenceReading> references)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(references);
        foreach (var candidate in candidates) candidate.Validate();
        foreach (var reference in references) reference.Validate();

        var fields = candidates.Select(x => x.FieldName)
            .Concat(references.Select(x => x.FieldName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return fields.Select(field => CompareField(
            field,
            candidates.Where(x => string.Equals(x.FieldName, field, StringComparison.Ordinal)).ToArray(),
            references.Where(x => string.Equals(x.FieldName, field, StringComparison.Ordinal)).ToArray()))
            .ToArray();
    }

    private static ShadowFieldComparison CompareField(
        string field,
        IReadOnlyList<ShadowFieldCandidate> candidates,
        IReadOnlyList<ShadowReferenceReading> references)
    {
        var knownCandidates = candidates
            .Where(x => x.Status == FieldReadingStatus.Known && !string.IsNullOrWhiteSpace(x.Value))
            .ToArray();
        var values = knownCandidates.Select(x => x.Value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var analyzers = candidates.Select(x => x.Analyzer)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var knownReference = references
            .Where(x => x.Status == FieldReadingStatus.Known && !string.IsNullOrWhiteSpace(x.Value))
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.Provider, StringComparer.Ordinal)
            .FirstOrDefault();

        if (values.Length > 1)
            return Result(ShadowComparisonKind.AnalyzerConflict, "ANALYZER_VALUES_CONFLICT", knownReference?.Value);
        if (values.Length == 1 && knownReference is not null)
            return string.Equals(values[0], knownReference.Value, StringComparison.Ordinal)
                ? Result(ShadowComparisonKind.Agreement, "CANDIDATE_MATCHES_REFERENCE", knownReference.Value)
                : Result(ShadowComparisonKind.ReferenceConflict, "CANDIDATE_DIFFERS_FROM_REFERENCE", knownReference.Value);
        if (values.Length == 1)
            return Result(ShadowComparisonKind.UnverifiedAgreement, "KNOWN_CANDIDATE_WITHOUT_REFERENCE", null);
        if (knownReference is not null)
            return Result(ShadowComparisonKind.CoverageGap, "REFERENCE_KNOWN_CANDIDATE_UNKNOWN", knownReference.Value);
        return Result(ShadowComparisonKind.NoKnownCandidate, "NO_KNOWN_VALUE", null);

        ShadowFieldComparison Result(ShadowComparisonKind kind, string reason, string? referenceValue) =>
            new(field, kind, values, referenceValue, analyzers, reason);
    }
}
