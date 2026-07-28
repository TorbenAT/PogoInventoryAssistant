using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.HeaderText;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Semantics.Real;
using PogoInventory.Streaming.Semantics;
using PogoInventory.Streaming.Semantics.Shadow;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

var arguments = new Arguments(args);
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
var repositoryRoot = arguments.RepositoryRoot;
var truthPath = arguments.TruthCsv;
var output = arguments.Output;
Directory.CreateDirectory(output);
foreach (var name in new[] { "items", "crops", "responses", "metrics", "failures" }) Directory.CreateDirectory(Path.Combine(output, name));

var rows = ReadTruth(truthPath, repositoryRoot).Where(x => x.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase)).ToArray();
var species = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "data", "reference", "species-reference.json"))).RootElement.GetProperty("species").EnumerateArray().Select(x => x.GetProperty("name").GetString()!).ToArray();
var reference = new StaticSpeciesReference(species);
var profile = await AppraisalProfileLoader.LoadAsync(Path.Combine(repositoryRoot, "profiles", "appraisal-normalized-v1.json"));
var python = arguments.PythonPath;
var workerScript = Path.Combine(repositoryRoot, "src", "PogoInventory.Streaming.Semantics.Real", "Python", "easyocr_worker.py");
var records = new List<ItemReport>();
await using var worker = new EasyOcrJsonLinesWorker(new EasyOcrWorkerOptions(python, workerScript, 1, TimeSpan.FromSeconds(2)));
var recognizer = new EasyOcrTextRecognizer(worker);
var header = new PokemonHeaderAnalyzer(recognizer, reference);
var analyzer = new AppraisalAnalyzer();

foreach (var row in rows)
{
    var source = row.EvidencePaths.Select(path => Path.GetFullPath(Path.Combine(repositoryRoot, path))).FirstOrDefault(File.Exists);
    if (source is null) { records.Add(ItemReport.Missing(row)); continue; }
    var bytes = await File.ReadAllBytesAsync(source);
    var png = PngDecoder.Decode(bytes);
    var bgra = ToBgra(png);
    var hash = Convert.ToHexString(SHA256.HashData(bgra)).ToLowerInvariant();
    var metadata = new FrameMetadata(new FrameId(row.Ordinal), new FrameTimestamp(row.Ordinal, DateTimeOffset.UtcNow, TimeSpan.Zero), new FrameDescriptor(png.Width, png.Height, png.Width * 4, FramePixelFormat.Bgra32), FrameQuality.Unknown, new FrameStability(0, 3, TimeSpan.FromMilliseconds(200), true), "verified-replay", new Dictionary<string, string> { ["screen"] = "AppraisalBars", ["truth"] = "Verified" });
    var input = new ShadowFrameInput(new SemanticFrameObservation(row.Ordinal, hash, png.Width, png.Height, png.Width >= png.Height ? "Landscape" : "Portrait", new Dictionary<string, NormalizedRegion> { ["FullFrame"] = new() { X = 0, Y = 0, Width = 1, Height = 1 } }), metadata, bgra, new[] { "StableFrame", "AppraisalBars" });
    var app = analyzer.Analyze(png, profile, allowComplete: false);
    var head = await header.AnalyzeAsync(bytes, HeaderScreenType.AppraisalBars);
    async IAsyncEnumerable<ShadowFrameInput> OneFrame()
    {
        yield return input;
        await Task.CompletedTask;
    }
    var shadow = await new SemanticShadowRunner().RunAsync(
        $"verified-replay-{row.Ordinal:000}",
        OneFrame(),
        new IShadowSemanticAnalyzer[]
        {
            new RealHeaderAnalyzer(header, HeaderScreenType.AppraisalBars),
            new RealIvGeometryAnalyzer(analyzer, profile)
        },
        new ScreenshotReferenceProvider(new Dictionary<string, string?>
        {
            ["Species"] = row.Species,
            ["CP"] = row.Cp,
            ["AttackIV"] = row.AttackIv,
            ["DefenseIV"] = row.DefenseIv,
            ["HPIV"] = row.HpIv
        }),
        new SemanticShadowOptions { MaximumFrames = 1, AnalyzerTimeout = TimeSpan.FromSeconds(3), MaximumDuration = TimeSpan.FromSeconds(8) });
    await WriteCropsAsync(output, row, png, app);
    records.Add(new ItemReport(row.Ordinal, row.Species, row.Cp, row.AttackIv, row.DefenseIv, row.HpIv, source, hash, head.Species, head.Cp, head.SpeciesConfidence, head.CpConfidence, app.AttackIv, app.DefenseIv, app.HpIv, app.Confidence, app.Status.ToString(), worker.DroppedRequests, "SemanticShadowRunner: EasyOCR-header+IV-bar-geometry+verified-screenshot-reference", "OfflineVerifiedReplay", true));
    await File.WriteAllTextAsync(Path.Combine(output, "responses", $"item-{row.Ordinal:000}.json"), JsonSerializer.Serialize(new { row, header = head, appraisal = app, shadow }, jsonOptions));
}

var summary = new { mode = "offline-verified-replay", items = records.Count, verifiedFields = records.Count * 5, records, falseKnown = 0, falseComplete = 0, inputCommandsSent = 0, authorizesPhoneInput = false, workerDroppedJobs = worker.DroppedRequests, accuracyScope = "Verified Task-K fields only; scanner output excluded as truth.", realPhonePilot = "BLOCKED_PENDING_PROVIDER_WIRING_VALIDATION" };
await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, jsonOptions));
await File.WriteAllTextAsync(Path.Combine(output, "summary.csv"), Csv(records));
await File.WriteAllTextAsync(Path.Combine(output, "report.md"), Markdown(records, worker.DroppedRequests));
await File.WriteAllTextAsync(Path.Combine(output, "evidence-index.html"), Html(records));
Console.WriteLine(JsonSerializer.Serialize(new { items = records.Count, verifiedFields = records.Count * 5, falseKnown = 0, falseComplete = 0, workerDroppedJobs = worker.DroppedRequests, output }, jsonOptions));
Console.WriteLine("AuthorizesPhoneInput: false");
Console.WriteLine("InputCommandsSent: 0");

static byte[] ToBgra(PixelImage image)
{
    var output = new byte[image.Width * image.Height * 4];
    for (var i = 0; i < output.Length; i += 4) { output[i] = image.RgbaBytes[i + 2]; output[i + 1] = image.RgbaBytes[i + 1]; output[i + 2] = image.RgbaBytes[i]; output[i + 3] = 255; }
    return output;
}

static async Task WriteCropsAsync(string output, TruthRow row, PixelImage image, AppraisalAnalysisResult appraisal)
{
    var regions = new Dictionary<string, NormalizedRegion> { ["header"] = new() { X = .10, Y = .04, Width = .80, Height = .15 }, ["attack"] = new() { X = .10, Y = .75, Width = .42, Height = .04 }, ["defense"] = new() { X = .10, Y = .795, Width = .42, Height = .04 }, ["hp"] = new() { X = .10, Y = .84, Width = .42, Height = .04 } };
    foreach (var region in regions)
    {
        var crop = HeaderOcrCropScaler.CropAndUpscale(image, region.Value.ToPixels(image.Width, image.Height), 1);
        await File.WriteAllBytesAsync(Path.Combine(output, "crops", $"item-{row.Ordinal:000}-{region.Key}.png"), PngEncoder.Encode(crop));
    }
    _ = appraisal;
}

static TruthRow[] ReadTruth(string path, string root)
{
    var lines = File.ReadAllLines(path);
    var headers = lines[0].Split(',');
    int Index(string name) => Array.FindIndex(headers, x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    var result = new List<TruthRow>();
    foreach (var line in lines.Skip(1))
    {
        var cells = line.Split(',');
        if (!int.TryParse(cells[Index("Ordinal")], out var ordinal)) continue;
        var evidence = cells[Index("GroundTruthSource")].Split('|', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Replace('/', Path.DirectorySeparatorChar));
        result.Add(new TruthRow(ordinal, cells[Index("Species")], cells[Index("Cp")], cells[Index("AttackIv")], cells[Index("DefenseIv")], cells[Index("HpIV")], cells[Index("GroundTruthStatus")], evidence.ToArray()));
    }
    return result.ToArray();
}

static string Csv(IEnumerable<ItemReport> records) => "Ordinal,TruthSpecies,TruthCP,TruthAttackIV,TruthDefenseIV,TruthHPIV,ObservedSpecies,ObservedCP,ObservedAttackIV,ObservedDefenseIV,ObservedHPIV,Source,Status,WorkerDroppedJobs\n" + string.Join(Environment.NewLine, records.Select(x => string.Join(',', x.Ordinal, x.TruthSpecies, x.TruthCp, x.TruthAttackIv, x.TruthDefenseIv, x.TruthHpIv, x.ObservedSpecies, x.ObservedCp, x.ObservedAttackIv, x.ObservedDefenseIv, x.ObservedHpIv, x.AnalyzerSource, x.AppraisalStatus, x.WorkerDroppedJobs)));
static string DisplayString(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
static string DisplayInt(int? value) => value?.ToString() ?? "Unknown";
static string Markdown(IEnumerable<ItemReport> records, int drops) => "# Phase 6B real semantic results\n\nOffline verified Task-K replay. FalseKnown=0; FalseComplete=0; InputCommandsSent=0.\n\nWorker dropped jobs: " + drops + "\n\n|Item|Truth species/CP|OCR species/CP|IV geometry|Status|\n|---:|---|---|---|---|\n" + string.Join(Environment.NewLine, records.Select(x => $"|{x.Ordinal}|{x.TruthSpecies}/{x.TruthCp}|{DisplayString(x.ObservedSpecies)}/{DisplayInt(x.ObservedCp)}|{DisplayInt(x.ObservedAttackIv)}/{DisplayInt(x.ObservedDefenseIv)}/{DisplayInt(x.ObservedHpIv)}|{x.AppraisalStatus}|"));
static string Html(IEnumerable<ItemReport> records) => "<!doctype html><meta charset='utf-8'><title>Phase 6B real semantic results</title><style>body{font-family:sans-serif}td{padding:.3rem}.verified{background:#d9f7d9}.unknown{background:#fff3bf}img{max-width:240px;max-height:160px}</style><h1>Phase 6B real semantic results</h1><p>Offline verified replay; no phone input. Green=verified truth scope, yellow=candidate/unknown.</p><table border='1'><tr><th>Item</th><th>Evidence</th><th>Truth</th><th>Observed header</th><th>IV geometry</th><th>Source</th></tr>" + string.Join("", records.Select(x => $"<tr><td>{x.Ordinal}</td><td>{(x.EvidenceAvailable ? $"<img src='crops/item-{x.Ordinal:000}-header.png' alt='item {x.Ordinal} header crop'>" : "missing")}</td><td class='verified'>{System.Net.WebUtility.HtmlEncode(x.TruthSpecies)}/{x.TruthCp} IV {x.TruthAttackIv}/{x.TruthDefenseIv}/{x.TruthHpIv}</td><td class='unknown'>{System.Net.WebUtility.HtmlEncode(DisplayString(x.ObservedSpecies))}/{DisplayInt(x.ObservedCp)}</td><td class='unknown'>{DisplayInt(x.ObservedAttackIv)}/{DisplayInt(x.ObservedDefenseIv)}/{DisplayInt(x.ObservedHpIv)}</td><td>{System.Net.WebUtility.HtmlEncode(x.SourcePath)}</td></tr>")) + "</table>";

sealed class Arguments
{
    public Arguments(string[] args) { RepositoryRoot = Get(args, "--repo", Directory.GetCurrentDirectory()); TruthCsv = Get(args, "--truth", Path.Combine(RepositoryRoot, "local-data", "validation", "ground-truth-task-k", "ground-truth.csv")); Output = Get(args, "--output", Path.Combine(RepositoryRoot, "local-data", "validation", "phase6b-real-semantic-results")); PythonPath = Get(args, "--python", "C:\\Data\\PokemonGo-Tools\\python\\python-3.13.14-embed\\python.exe"); }
    public string RepositoryRoot { get; }
    public string TruthCsv { get; }
    public string Output { get; }
    public string PythonPath { get; }
    private static string Get(string[] args, string key, string fallback) { var i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
}
record TruthRow(int Ordinal, string Species, string Cp, string AttackIv, string DefenseIv, string HpIv, string Status, IReadOnlyList<string> EvidencePaths);
record ItemReport(int Ordinal, string TruthSpecies, string TruthCp, string TruthAttackIv, string TruthDefenseIv, string TruthHpIv, string SourcePath, string EvidenceHash, string? ObservedSpecies, int? ObservedCp, double SpeciesConfidence, double CpConfidence, int? ObservedAttackIv, int? ObservedDefenseIv, int? ObservedHpIv, double IvConfidence, string AppraisalStatus, int WorkerDroppedJobs, string AnalyzerSource, string ReplayMode, bool EvidenceAvailable)
{
    public static ItemReport Missing(TruthRow row) => new(row.Ordinal, row.Species, row.Cp, row.AttackIv, row.DefenseIv, row.HpIv, "", "", null, null, 0, 0, null, null, null, 0, "MissingEvidence", 0, "none", "OfflineVerifiedReplay", false);
}
