using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Diagnostics;

namespace PogoInventory.Streaming.Semantics.Ollama;

public sealed class OllamaClient : IOllamaModelCatalog, IOllamaTextEmbeddingProvider, IOllamaVisionCandidateAnalyzer, IDisposable
{
    private static readonly string[] AllowedStatuses = ["Known", "Candidate", "Unknown", "Conflicting", "Occluded", "Unreadable", "NotVisible", "Unsupported"];
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public OllamaClient(OllamaClientOptions? options = null, HttpClient? httpClient = null)
    {
        options ??= new();
        _http = httpClient ?? new HttpClient { Timeout = options.Timeout };
        _ownsClient = httpClient is null;
        _http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
    }

    public async Task<IReadOnlyList<OllamaModelCapabilities>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("api/tags", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.GetProperty("models").EnumerateArray().Select(ParseModel).ToArray();
    }

    public async Task<OllamaModelCapabilities> ShowAsync(string model, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        using var response = await _http.PostAsJsonAsync("api/show", new { name = model }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        var root = document.RootElement;
        var details = root.GetProperty("details");
        var capabilities = root.TryGetProperty("capabilities", out var caps) ? caps.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => x.Length > 0).ToArray() : Array.Empty<string>();
        return new(model, root.TryGetProperty("digest", out var digest) ? digest.GetString() ?? string.Empty : string.Empty, 0, StringOrNull(details, "family"), StringOrNull(details, "parameter_size"), StringOrNull(details, "quantization_level"), IntOrNull(details, "context_length"), capabilities);
    }

    public async Task<OllamaEmbeddingResult> EmbedAsync(string model, IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0 || inputs.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Embedding inputs must be non-empty.", nameof(inputs));
        var started = Stopwatch.GetTimestamp();
        using var response = await _http.PostAsJsonAsync("api/embed", new { model, input = inputs }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
        var vectors = document.RootElement.GetProperty("embeddings").EnumerateArray().Select(x => (IReadOnlyList<double>)x.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToArray();
        if (vectors.Length != inputs.Count || vectors.Length == 0 || vectors.Any(x => x.Count != vectors[0].Count)) throw new InvalidDataException("Ollama returned inconsistent embedding dimensions.");
        return new(model, vectors, vectors[0].Count, Metrics(document.RootElement, started));
    }

    public async Task<OllamaVisionCandidate> AnalyzeAsync(string model, ReadOnlyMemory<byte> image, string promptVersion, string prompt, string schemaVersion = "phase6a-ollama-vision-1", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        if (image.IsEmpty) throw new ArgumentException("Image is required.", nameof(image));
        var digest = await ShowAsync(model, cancellationToken).ConfigureAwait(false);
        var imageHash = Convert.ToHexString(SHA256.HashData(image.Span)).ToLowerInvariant();
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var response = await _http.PostAsJsonAsync("api/chat", new { model, stream = false, think = false, keep_alive = "5m", format = "json", options = new { temperature = 0, num_predict = 128 }, messages = new[] { new { role = "user", content = prompt, images = new[] { Convert.ToBase64String(image.ToArray()) } } } }, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var envelope = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false));
            var content = envelope.RootElement.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content)) return Invalid("EMPTY_MODEL_RESPONSE");
            using var result = JsonDocument.Parse(content);
            return ParseCandidate(result.RootElement, digest, imageHash, promptVersion, schemaVersion, Metrics(envelope.RootElement, started));
        }
        catch (JsonException) { return Invalid("INVALID_MODEL_RESPONSE"); }
        catch (KeyNotFoundException) { return Invalid("INVALID_MODEL_RESPONSE"); }
        catch (InvalidOperationException) { return Invalid("INVALID_MODEL_RESPONSE"); }
        catch (HttpRequestException) { return Invalid("MODEL_UNAVAILABLE"); }
        catch (TaskCanceledException) { return Invalid("TIMED_OUT"); }

        OllamaVisionCandidate Invalid(string reason) => new(false, Empty(), Empty(), Empty(), Empty(), Empty(), Empty(), [reason], model, digest.Digest, promptVersion, schemaVersion, imageHash, new(0, 0, 0, 0, null, null, null), reason);
    }

    private static OllamaVisionCandidate ParseCandidate(JsonElement root, OllamaModelCapabilities model, string imageHash, string promptVersion, string schemaVersion, OllamaInferenceMetrics metrics)
    {
        var candidate = new OllamaVisionCandidate(root.GetProperty("layoutSupported").GetBoolean(), ParseField(root.GetProperty("screenState")), ParseField(root.GetProperty("species")), ParseField(root.GetProperty("cp")), ParseField(root.GetProperty("attackIv")), ParseField(root.GetProperty("defenseIv")), ParseField(root.GetProperty("hpIv")), root.TryGetProperty("diagnostics", out var diagnostics) ? diagnostics.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray() : Array.Empty<string>(), model.Name, model.Digest, promptVersion, schemaVersion, imageHash, metrics, "VALIDATED_CANDIDATE");
        return candidate;
    }

    private static OllamaCandidateField ParseField(JsonElement value)
    {
        var status = value.GetProperty("status").GetString() ?? "Unsupported";
        if (!AllowedStatuses.Contains(status, StringComparer.Ordinal)) throw new JsonException("Unknown candidate status.");
        var confidence = value.GetProperty("confidence").GetDouble();
        if (confidence is < 0 or > 1) throw new JsonException("Candidate confidence is out of range.");
        var normalized = status is "Known" or "Candidate" ? OllamaCandidateStatus.Candidate : Enum.Parse<OllamaCandidateStatus>(status);
        return new(normalized, value.TryGetProperty("value", out var fieldValue) && fieldValue.ValueKind != JsonValueKind.Null ? fieldValue.ToString() : null, confidence, value.TryGetProperty("visibleText", out var visible) ? visible.GetString() : null);
    }

    private static OllamaCandidateField Empty() => new(OllamaCandidateStatus.Unsupported, null, 0, null);
    private static OllamaModelCapabilities ParseModel(JsonElement root) => new(root.GetProperty("name").GetString() ?? string.Empty, root.GetProperty("digest").GetString() ?? string.Empty, root.GetProperty("size").GetInt64(), StringOrNull(root.GetProperty("details"), "family"), StringOrNull(root.GetProperty("details"), "parameter_size"), StringOrNull(root.GetProperty("details"), "quantization_level"), IntOrNull(root.GetProperty("details"), "context_length"), root.TryGetProperty("capabilities", out var caps) ? caps.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray() : Array.Empty<string>());
    private static string? StringOrNull(JsonElement root, string property) => root.TryGetProperty(property, out var value) ? value.GetString() : null;
    private static int? IntOrNull(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static OllamaInferenceMetrics Metrics(JsonElement root, long started) => new((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency, NsMs(root, "load_duration"), NsMs(root, "prompt_eval_duration"), NsMs(root, "eval_duration"), IntOrNull(root, "prompt_eval_count"), IntOrNull(root, "eval_count"), null);
    private static double NsMs(JsonElement root, string property) => root.TryGetProperty(property, out var value) && value.TryGetInt64(out var ns) ? ns / 1_000_000d : 0;
    public void Dispose() { if (_ownsClient) _http.Dispose(); }
}
