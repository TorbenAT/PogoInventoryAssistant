namespace PogoInventory.Streaming.Semantics;

public sealed class UnsupportedFieldAnalyzer<T> : ISemanticFieldAnalyzer<T>
{
    private readonly string _field;
    public UnsupportedFieldAnalyzer(string field) => _field = string.IsNullOrWhiteSpace(field) ? throw new ArgumentException("Field is required.") : field;
    public string Method => "deterministic-baseline-unavailable";
    public FieldEvidence<T> Analyze(SemanticFrameObservation frame) => new(_field, default, 0, frame.FrameId, frame.EvidenceHash, FieldReadingStatus.Unsupported);
}
