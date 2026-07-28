using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PogoInventory.Vision.Imaging;
using PogoInventory.Streaming.Semantics.Ollama.Evidence;

var options = BenchmarkOptions.Parse(args);
var outputRoot = Path.GetFullPath(options.OutputRoot);
Directory.CreateDirectory(outputRoot);
foreach (var folder in new[] { "images", "crops", "raw-responses", "parsed-responses", "metrics", "failures" }) Directory.CreateDirectory(Path.Combine(outputRoot, folder));
var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(options.ManifestPath));
var cases = manifest.RootElement.GetProperty("cases").EnumerateArray().Select(CaseInput.Parse).ToArray();
var selectedCases = options.Stage == 1 ? cases.Take(3).ToArray() : cases;
var models = options.Models.Count == 0 ? ["qwen3-vl:2b-instruct", "minicpm-v4.6:1b", "gemma3:4b", "qwen3-vl:4b-instruct", "qwen3.5:9b"] : options.Models.ToArray();
var schema = EvidenceSchema.BuildSchema();
var rows = new List<BenchmarkRow>();
using var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds) };
var budget = Stopwatch.StartNew();
foreach (var model in models)
{
    foreach (var item in selectedCases)
    {
        if (budget.Elapsed > TimeSpan.FromMinutes(options.BudgetMinutes)) break;
        var imagePath = Path.GetFullPath(item.ImagePath);
        var source = await File.ReadAllBytesAsync(imagePath);
        var hash = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        await File.WriteAllBytesAsync(Path.Combine(outputRoot, "images", item.CaseId + Path.GetExtension(imagePath)), source);
        var repeats = options.Stage == 1 ? (model == "qwen3.5:9b" ? 2 : 3) : 5;
        for (var attempt = 0; attempt <= repeats; attempt++)
        {
            if (budget.Elapsed > TimeSpan.FromMinutes(options.BudgetMinutes)) break;
            var cold = attempt == 0;
            var profile = item.CaseId.StartsWith("details-", StringComparison.Ordinal) || item.CaseId.StartsWith("appraisal-", StringComparison.Ordinal) ? "FixedSemanticCrop" : "ScreenClassification";
            string? cropHash = null;
            var requestImage = profile == "FixedSemanticCrop" ? MakeSemanticCrop(source, out cropHash) : source;
            if (profile == "FixedSemanticCrop") await File.WriteAllBytesAsync(Path.Combine(outputRoot, "crops", item.CaseId + ".png"), requestImage);
            var result = await ProbeAsync(http, model, item, requestImage, profile == "FixedSemanticCrop" ? cropHash! : hash, schema, options, cold, profile, CancellationToken.None);
            rows.Add(result);
            var stem = $"{model.Replace(':', '_')}_{item.CaseId}_{attempt}";
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "raw-responses", stem + ".json"), result.RawResponse ?? "", CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "parsed-responses", stem + ".json"), JsonSerializer.Serialize(result.Parsed, JsonDefaults.Options), CancellationToken.None);
            await File.WriteAllTextAsync(Path.Combine(outputRoot, "metrics", stem + ".json"), JsonSerializer.Serialize(result.Metrics, JsonDefaults.Options), CancellationToken.None);
            if (result.ReasonCode == "Timeout" && rows.Count(x => x.Model == model && x.ReasonCode == "Timeout") >= 3) break;
        }
    }
}
var summary = Summarize(rows, models, options);
await File.WriteAllTextAsync(Path.Combine(outputRoot, "manifest.json"), JsonSerializer.Serialize(new { schema = "phase6a-vlm-evidence-pack-1", generatedUtc = DateTimeOffset.UtcNow, options = new { options.BaseUrl, options.ManifestPath, options.OutputRoot, options.Stage, options.BudgetMinutes, options.TimeoutSeconds, options.Models }, cases, models }, JsonDefaults.Options));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "summary.json"), JsonSerializer.Serialize(summary, JsonDefaults.Options));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "summary.csv"), Csv(rows));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "model-ranking.md"), Ranking(summary));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "evidence-index.html"), Html(rows, outputRoot));
Console.WriteLine($"Evidence pack: {outputRoot}");
Console.WriteLine($"Rows: {rows.Count}; schema-valid: {rows.Count(x => x.SchemaValid)}; budget minutes: {budget.Elapsed.TotalMinutes:F1}");
Console.WriteLine("AuthorizesPhoneInput: false");
Console.WriteLine("InputCommandsSent: 0");

static async Task<BenchmarkRow> ProbeAsync(HttpClient http, string model, CaseInput item, byte[] image, string hash, JsonObject schema, BenchmarkOptions options, bool cold, string profile, CancellationToken cancellationToken)
{
    var started = Stopwatch.GetTimestamp();
    var sampler = new GpuSampler();
    await sampler.StartAsync(cancellationToken);
    var prompt = profile == "FixedSemanticCrop" ? "Profile B: inspect the fixed semantic crop for details/appraisal fields only. Missing fields must be NotVisible; never guess. Return only the schema." : "Profile A: classify the full screen and report visible semantic evidence. Return only the schema. Candidate means a model proposal, never an authoritative Known value. Unknown, NotVisible, Occluded or Unsupported are preferred to guesses.";
    var body = new { model, stream = false, think = false, keep_alive = "5m", format = schema, options = new { temperature = 0, num_predict = 512 }, messages = new[] { new { role = "user", content = prompt, images = new[] { Convert.ToBase64String(image) } } } };
    string? raw = null; ParsedResponse? parsed = null; var reason = "HttpError"; ApiMetrics metrics = new(0, 0, 0, 0, 0, 0, 0);
    try
    {
        using var response = await http.PostAsJsonAsync("api/chat", body, cancellationToken);
        raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) reason = "HttpError";
        else
        {
            using var envelope = JsonDocument.Parse(raw);
            var content = envelope.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            metrics = ApiMetrics.Parse(envelope.RootElement, (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency, await sampler.StopAsync());
            try { parsed = ParsedResponse.Parse(content); reason = parsed.SchemaValid ? "Valid" : "InvalidModelResponse"; }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { reason = "InvalidModelResponse"; }
        }
    }
    catch (TaskCanceledException) { reason = "Timeout"; }
    catch (HttpRequestException) { reason = "ModelUnavailable"; }
    if (metrics.TotalMilliseconds == 0) metrics = new((Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency, 0, 0, 0, 0, 0, sampler.PeakVramBytes);
    await sampler.StopAsync();
    return new(model, item.CaseId, item.ImagePath, hash, cold, reason == "Valid", reason, parsed, raw, metrics, item.TruthStatus, item.ScreenStateTruth);
}

static byte[] MakeSemanticCrop(byte[] source, out string? hash)
{
    try
    {
        var decoded = PngDecoder.Decode(source);
        var x = decoded.Width / 10;
        var y = decoded.Height / 5;
        var width = Math.Max(1, decoded.Width * 8 / 10);
        var height = Math.Max(1, decoded.Height * 3 / 5);
        width = Math.Min(width, decoded.Width - x);
        height = Math.Min(height, decoded.Height - y);
        var rgba = new byte[checked(width * height * 4)];
        for (var row = 0; row < height; row++) decoded.RgbaBytes.Slice(((y + row) * decoded.Width + x) * 4, width * 4).CopyTo(rgba.AsSpan(row * width * 4));
        var crop = PngEncoder.Encode(new PixelImage(width, height, rgba));
        hash = Convert.ToHexString(SHA256.HashData(crop)).ToLowerInvariant();
        return crop;
    }
    catch
    {
        hash = null;
        return source;
    }
}

static object Summarize(IReadOnlyList<BenchmarkRow> rows, IReadOnlyList<string> models, BenchmarkOptions options) => new { models, stage = options.Stage, rows = rows.Count, schemaValidRate = rows.Count == 0 ? (double?)null : rows.Count(x => x.SchemaValid) / (double)rows.Count, timeoutRate = rows.Count == 0 ? (double?)null : rows.Count(x => x.ReasonCode == "Timeout") / (double)rows.Count, falseKnown = (int?)null, falseComplete = (int?)null, repeatability = Repeatability(rows), generatedUtc = DateTimeOffset.UtcNow };
static object Repeatability(IReadOnlyList<BenchmarkRow> rows) => rows.GroupBy(x => (x.Model, x.CaseId)).Select(g => new { g.Key.Model, g.Key.CaseId, runs = g.Count(), exactResponseRepeatability = (double?)null, normalizedFieldRepeatability = g.Select(x => x.Parsed?.NormalizedKey).Distinct().Count() == 1 && g.Any(x => x.Parsed is not null) ? 1d : 0d, valueFlipCount = g.Select(x => x.Parsed?.Species.Value).Distinct().Count() - 1 }).ToArray();
static string Csv(IReadOnlyList<BenchmarkRow> rows) { var b = new StringBuilder("Model,CaseId,Cold,SchemaValid,ReasonCode,TotalMs,LoadMs,PromptEvalMs,EvalMs,PeakVramBytes\n"); foreach (var x in rows) b.AppendLine($"{x.Model},{x.CaseId},{x.Cold},{x.SchemaValid},{x.ReasonCode},{x.Metrics.TotalMilliseconds:F2},{x.Metrics.LoadMilliseconds:F2},{x.Metrics.PromptEvalMilliseconds:F2},{x.Metrics.EvalMilliseconds:F2},{x.Metrics.PeakVramBytes}"); return b.ToString(); }
static string Ranking(object summary) => "# VLM model ranking\n\nThis ranking is generated from the staged local evidence pack. Accuracy is null where truth is not verified; VLM output remains candidate-only.\n\n" + JsonSerializer.Serialize(summary, JsonDefaults.Options);
static string Html(IReadOnlyList<BenchmarkRow> rows, string root) => "<!doctype html><meta charset='utf-8'><title>Phase 6A VLM evidence</title><h1>Phase 6A VLM evidence</h1><p>Raw responses and parsed responses are stored beside this index.</p><table border='1'><tr><th>Model</th><th>Case</th><th>Cold</th><th>Schema</th><th>Reason</th><th>Total ms</th><th>Peak VRAM</th></tr>" + string.Join("", rows.Select(x => $"<tr><td>{System.Net.WebUtility.HtmlEncode(x.Model)}</td><td>{System.Net.WebUtility.HtmlEncode(x.CaseId)}</td><td>{x.Cold}</td><td>{x.SchemaValid}</td><td>{x.ReasonCode}</td><td>{x.Metrics.TotalMilliseconds:F1}</td><td>{x.Metrics.PeakVramBytes}</td></tr>")) + "</table>";

static class JsonDefaults { public static JsonSerializerOptions Options { get; } = new() { WriteIndented = true }; }
record BenchmarkRow(string Model, string CaseId, string ImagePath, string ImageSha256, bool Cold, bool SchemaValid, string ReasonCode, ParsedResponse? Parsed, string? RawResponse, ApiMetrics Metrics, string TruthStatus, string? ScreenStateTruth);
record CaseInput(string CaseId, string ImagePath, string ImageClass, string? ScreenStateTruth, string TruthStatus) { public static CaseInput Parse(JsonElement e) => new(e.GetProperty("caseId").GetString()!, e.GetProperty("imagePath").GetString()!, e.GetProperty("imageClass").GetString()!, e.TryGetProperty("screenStateTruth", out var s) ? s.GetString() : null, e.GetProperty("truthStatus").GetString()!); }
record ApiMetrics(double TotalMilliseconds, double LoadMilliseconds, double PromptEvalMilliseconds, double EvalMilliseconds, int PromptTokens, int OutputTokens, long PeakVramBytes) { public static ApiMetrics Parse(JsonElement e, double total, long peak) => new(total, Ns(e, "load_duration"), Ns(e, "prompt_eval_duration"), Ns(e, "eval_duration"), Int(e, "prompt_eval_count"), Int(e, "eval_count"), peak); static double Ns(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt64(out var n) ? n / 1_000_000d : 0; static int Int(JsonElement e, string p) => e.TryGetProperty(p, out var v) && v.TryGetInt32(out var n) ? n : 0; }
record ParsedField(string Status, string? Value, double Confidence, string? VisibleText);
record ParsedResponse(bool LayoutSupported, ParsedField ScreenState, ParsedField Species, ParsedField Cp, ParsedField AttackIv, ParsedField DefenseIv, ParsedField HpIv, IReadOnlyList<string> Diagnostics, bool SchemaValid, string NormalizedKey) { public static ParsedResponse Parse(string json) { using var d = JsonDocument.Parse(json); var r = d.RootElement; var fields = new[] { "screenState", "species", "cp", "attackIv", "defenseIv", "hpIv" }.Select(p => ParseField(r.GetProperty(p))).ToArray(); var normalized = JsonSerializer.Serialize(new { fields, diagnostics = r.GetProperty("diagnostics") }); return new(r.GetProperty("layoutSupported").GetBoolean(), fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], r.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray(), true, normalized); } static ParsedField ParseField(JsonElement e) { var status = e.GetProperty("status").GetString()!; if (status == "Known") status = "Candidate"; if (!new[] { "Candidate", "Unknown", "Conflicting", "Occluded", "Unreadable", "NotVisible", "Unsupported" }.Contains(status, StringComparer.Ordinal)) throw new JsonException(); var confidence = e.GetProperty("confidence").GetDouble(); if (confidence is < 0 or > 1) throw new JsonException(); var value = e.GetProperty("value"); return new(status, value.ValueKind == JsonValueKind.Null ? null : value.ToString(), confidence, e.GetProperty("visibleText").ValueKind == JsonValueKind.Null ? null : e.GetProperty("visibleText").GetString()); } }

sealed class GpuSampler
{
    private CancellationTokenSource? _stop; private Task? _task; public long PeakVramBytes { get; private set; }
    public Task StartAsync(CancellationToken token) { _stop = CancellationTokenSource.CreateLinkedTokenSource(token); _task = Task.Run(async () => { while (!_stop.IsCancellationRequested) { var used = ReadUsedBytes(); if (used > PeakVramBytes) PeakVramBytes = used; await Task.Delay(250, _stop.Token).ConfigureAwait(false); } }, _stop.Token); return Task.CompletedTask; }
    public async Task<long> StopAsync() { if (_stop is null) return PeakVramBytes; _stop.Cancel(); try { if (_task is not null) await _task.ConfigureAwait(false); } catch (OperationCanceledException) { } return PeakVramBytes; }
    private static long ReadUsedBytes() { try { using var p = Process.Start(new ProcessStartInfo("nvidia-smi") { ArgumentList = { "--query-gpu=memory.used", "--format=csv,noheader,nounits" }, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true }); var s = p?.StandardOutput.ReadToEnd().Trim(); p?.WaitForExit(2000); return long.TryParse(s, out var mib) ? mib * 1024L * 1024L : 0; } catch { return 0; } }
}

sealed record BenchmarkOptions(string BaseUrl, string ManifestPath, string OutputRoot, int Stage, int BudgetMinutes, int TimeoutSeconds, IReadOnlyList<string> Models, CancellationToken Token)
{
    public static BenchmarkOptions Parse(string[] args) { string Get(string n, string d) { var i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : d; } var models = Get("--models", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); return new(Get("--base-url", "http://localhost:11434"), Get("--manifest", "data/phase6a-vlm-evidence-manifest.synthetic.json"), Get("--output", "C:/Data/PokemonGo-Tools/evidence/phase6a-vlm-bakeoff"), int.Parse(Get("--stage", "1")), int.Parse(Get("--budget-minutes", "90")), int.Parse(Get("--timeout-seconds", "90")), models, CancellationToken.None); }
}
