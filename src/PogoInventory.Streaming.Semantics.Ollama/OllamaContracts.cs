namespace PogoInventory.Streaming.Semantics.Ollama;

public sealed record OllamaClientOptions
{
    public string BaseUrl { get; init; } = Environment.GetEnvironmentVariable("POGO_OLLAMA_BASE_URL") ?? Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(3);
}

public sealed record OllamaModelCapabilities(string Name, string Digest, long Size, string? Family, string? ParameterSize, string? Quantization, int? ContextLength, IReadOnlyList<string> Capabilities)
{
    public bool IsVision => Capabilities.Contains("vision", StringComparer.OrdinalIgnoreCase);
    public bool IsEmbedding => Capabilities.Contains("embedding", StringComparer.OrdinalIgnoreCase);
}

public sealed record OllamaInferenceMetrics(double TotalMilliseconds, double LoadMilliseconds, double PromptEvaluationMilliseconds, double GenerationMilliseconds, int? InputTokens, int? OutputTokens, long? VramBytes);

public sealed record OllamaEmbeddingResult(string Model, IReadOnlyList<IReadOnlyList<double>> Vectors, int Dimensions, OllamaInferenceMetrics Metrics);

public enum OllamaCandidateStatus { Candidate, Unknown, Conflicting, Occluded, Unreadable, NotVisible, Unsupported }

public sealed record OllamaCandidateField(OllamaCandidateStatus Status, string? Value, double Confidence, string? VisibleText);

public sealed record OllamaVisionCandidate(
    bool LayoutSupported,
    OllamaCandidateField ScreenState,
    OllamaCandidateField Species,
    OllamaCandidateField Cp,
    OllamaCandidateField AttackIv,
    OllamaCandidateField DefenseIv,
    OllamaCandidateField HpIv,
    IReadOnlyList<string> Diagnostics,
    string Model,
    string ModelDigest,
    string PromptVersion,
    string SchemaVersion,
    string InputImageSha256,
    OllamaInferenceMetrics Metrics,
    string ReasonCode);

public interface IOllamaModelCatalog
{
    Task<IReadOnlyList<OllamaModelCapabilities>> ListAsync(CancellationToken cancellationToken = default);
    Task<OllamaModelCapabilities> ShowAsync(string model, CancellationToken cancellationToken = default);
}

public interface IOllamaTextEmbeddingProvider
{
    Task<OllamaEmbeddingResult> EmbedAsync(string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken = default);
}

public interface IOllamaVisionCandidateAnalyzer
{
    Task<OllamaVisionCandidate> AnalyzeAsync(string model, ReadOnlyMemory<byte> image, string promptVersion, string prompt, string schemaVersion = "phase6a-ollama-vision-1", CancellationToken cancellationToken = default);
}
