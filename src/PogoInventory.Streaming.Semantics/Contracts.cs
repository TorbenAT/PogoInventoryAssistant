using PogoInventory.Vision.Models;

namespace PogoInventory.Streaming.Semantics;

public enum FieldReadingStatus { Known, Conflicting, Occluded, Unreadable, NotVisible, Unsupported, Unknown }

public enum GroundTruthStatus { Verified, Synthetic, Unverifiable }

public sealed record SemanticFrameObservation(
    long FrameId,
    string EvidenceHash,
    int Width,
    int Height,
    string Orientation,
    IReadOnlyDictionary<string, NormalizedRegion> Regions)
{
    public void Validate()
    {
        if (FrameId < 0 || string.IsNullOrWhiteSpace(EvidenceHash) || Width <= 0 || Height <= 0)
            throw new ArgumentException("Frame identity, evidence hash and dimensions are required.");
        if (!string.Equals(Orientation, Width >= Height ? "Landscape" : "Portrait", StringComparison.Ordinal))
            throw new ArgumentException("Orientation does not match frame dimensions.");
        foreach (var pair in Regions) pair.Value.Validate(pair.Key);
    }
}

public sealed record FieldReading<T>(
    string FieldName,
    FieldReadingStatus Status,
    T? Value,
    double Confidence,
    IReadOnlyList<long> EvidenceFrameIds,
    IReadOnlyList<string> EvidenceHashes,
    string ReasonCode,
    IReadOnlyDictionary<string, string> Diagnostics)
{
    public bool IsKnown => Status == FieldReadingStatus.Known;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FieldName) || string.IsNullOrWhiteSpace(ReasonCode))
            throw new ArgumentException("FieldName and ReasonCode are required.");
        if (Confidence is < 0 or > 1 || EvidenceFrameIds.Count != EvidenceHashes.Count)
            throw new ArgumentException("Confidence and evidence are invalid.");
        if (Status == FieldReadingStatus.Known && (Value is null || EvidenceFrameIds.Count == 0))
            throw new ArgumentException("Known readings require a value and evidence.");
    }
}

public sealed record FieldEvidence<T>(
    string FieldName,
    T? Value,
    double Confidence,
    long FrameId,
    string EvidenceHash,
    FieldReadingStatus Status = FieldReadingStatus.Known);

public sealed record SemanticObservationSet(
    string IdentityKey,
    IReadOnlyList<SemanticFrameObservation> Frames)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IdentityKey) || Frames.Count == 0) throw new ArgumentException("Identity and frames are required.");
        foreach (var frame in Frames) frame.Validate();
    }
}

public sealed record FieldConsensusOptions(double MinimumConfidence = 0.80, int RequiredAgreement = 2)
{
    public void Validate() { if (MinimumConfidence is < 0 or > 1 || RequiredAgreement < 1) throw new ArgumentOutOfRangeException(); }
}

public interface IFieldConsensusGate<T>
{
    FieldReading<T> Resolve(string fieldName, string identityKey, IReadOnlyList<FieldEvidence<T>> evidence, FieldConsensusOptions options);
}

public interface ISemanticFieldAnalyzer<T>
{
    string Method { get; }
    FieldEvidence<T> Analyze(SemanticFrameObservation frame);
}
