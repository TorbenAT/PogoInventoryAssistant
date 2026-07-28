using PogoInventory.Streaming;
using PogoInventory.Streaming.Semantics;

namespace PogoInventory.Streaming.Semantics.Shadow;

public enum ShadowAnalyzerExecutionStatus
{
    Completed = 0,
    TimedOut = 1,
    Faulted = 2
}

public enum ShadowComparisonKind
{
    Agreement = 0,
    UnverifiedAgreement = 1,
    AnalyzerConflict = 2,
    ReferenceConflict = 3,
    CoverageGap = 4,
    NoKnownCandidate = 5
}

public sealed record ShadowFrameInput(
    SemanticFrameObservation SemanticObservation,
    FrameMetadata Metadata,
    ReadOnlyMemory<byte> Pixels,
    IReadOnlyList<string> Roles)
{
    public long FrameId => Metadata.Id.Value;
    public string EvidenceHash => SemanticObservation.EvidenceHash;

    public void Validate()
    {
        SemanticObservation.Validate();
        if (SemanticObservation.FrameId != Metadata.Id.Value)
            throw new ArgumentException("Semantic and streaming frame identities do not match.");
        if (SemanticObservation.Width != Metadata.Descriptor.Width ||
            SemanticObservation.Height != Metadata.Descriptor.Height)
            throw new ArgumentException("Semantic and streaming frame dimensions do not match.");
        if (Metadata.Descriptor.PixelFormat != FramePixelFormat.Bgra32)
            throw new ArgumentException("Phase 6B shadow input requires BGRA32 frames.");
        if (Pixels.Length < Metadata.Descriptor.RequiredByteLength)
            throw new ArgumentException("The copied frame does not contain a complete image.");
        if (Roles.Count == 0 || Roles.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty frame role is required.");
    }
}

public sealed record ShadowFieldCandidate(
    string Analyzer,
    string FieldName,
    FieldReadingStatus Status,
    string? Value,
    double Confidence,
    string ReasonCode,
    long FrameId,
    string EvidenceHash,
    IReadOnlyDictionary<string, string>? Diagnostics = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Analyzer) ||
            string.IsNullOrWhiteSpace(FieldName) ||
            string.IsNullOrWhiteSpace(ReasonCode) ||
            string.IsNullOrWhiteSpace(EvidenceHash))
            throw new ArgumentException("Analyzer, field, reason and evidence hash are required.");
        if (FrameId < 0 || Confidence is < 0 or > 1)
            throw new ArgumentException("Frame identity or confidence is invalid.");
        if (Status == FieldReadingStatus.Known && string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("Known shadow candidates require a value.");
    }
}

public sealed record ShadowReferenceReading(
    string Provider,
    string FieldName,
    FieldReadingStatus Status,
    string? Value,
    double Confidence,
    string ReasonCode)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Provider) ||
            string.IsNullOrWhiteSpace(FieldName) ||
            string.IsNullOrWhiteSpace(ReasonCode))
            throw new ArgumentException("Reference provider, field and reason are required.");
        if (Confidence is < 0 or > 1)
            throw new ArgumentException("Reference confidence is invalid.");
        if (Status == FieldReadingStatus.Known && string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("Known reference readings require a value.");
    }
}

public sealed record ShadowAnalyzerExecution(
    string Analyzer,
    ShadowAnalyzerExecutionStatus Status,
    double DurationMilliseconds,
    IReadOnlyList<ShadowFieldCandidate> Candidates,
    string? Error);

public sealed record ShadowFieldComparison(
    string FieldName,
    ShadowComparisonKind Kind,
    IReadOnlyList<string> CandidateValues,
    string? ReferenceValue,
    IReadOnlyList<string> Analyzers,
    string ReasonCode);

public sealed record ShadowFrameResult(
    long FrameId,
    DateTimeOffset CapturedAtUtc,
    string EvidenceHash,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ShadowAnalyzerExecution> Executions,
    IReadOnlyList<ShadowReferenceReading> References,
    IReadOnlyList<ShadowFieldComparison> Comparisons);

public sealed record SemanticShadowOptions
{
    public int MaximumFrames { get; init; } = 30;
    public TimeSpan MaximumDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan AnalyzerTimeout { get; init; } = TimeSpan.FromSeconds(3);
    public int MaximumAnalyzerConcurrency { get; init; } = 2;
    public bool RequireAtLeastOneAnalyzer { get; init; } = true;

    public void Validate()
    {
        if (MaximumFrames < 1 || MaximumFrames > 1000)
            throw new ArgumentOutOfRangeException(nameof(MaximumFrames));
        if (MaximumDuration <= TimeSpan.Zero || MaximumDuration > TimeSpan.FromMinutes(30))
            throw new ArgumentOutOfRangeException(nameof(MaximumDuration));
        if (AnalyzerTimeout <= TimeSpan.Zero || AnalyzerTimeout > MaximumDuration)
            throw new ArgumentOutOfRangeException(nameof(AnalyzerTimeout));
        if (MaximumAnalyzerConcurrency < 1 || MaximumAnalyzerConcurrency > 32)
            throw new ArgumentOutOfRangeException(nameof(MaximumAnalyzerConcurrency));
    }
}

public sealed record ShadowSessionReport
{
    public required string SessionId { get; init; }
    public required DateTimeOffset StartedUtc { get; init; }
    public required DateTimeOffset EndedUtc { get; init; }
    public required string FinalStatus { get; init; }
    public required IReadOnlyList<ShadowFrameResult> Frames { get; init; }
    public int AnalyzerFaults { get; init; }
    public int AnalyzerTimeouts { get; init; }
    public int KnownCandidates { get; init; }
    public int ComparisonConflicts { get; init; }
    public bool TimedOut { get; init; }
    public int InputCommandsSent => 0;
    public bool AuthorizesPhoneInput => false;
}

public interface IShadowSemanticAnalyzer
{
    string Name { get; }
    ValueTask<IReadOnlyList<ShadowFieldCandidate>> AnalyzeAsync(
        ShadowFrameInput frame,
        CancellationToken cancellationToken = default);
}

public interface IShadowReferenceProvider
{
    string Name { get; }
    ValueTask<IReadOnlyList<ShadowReferenceReading>> GetReferenceAsync(
        ShadowFrameInput frame,
        CancellationToken cancellationToken = default);
}
