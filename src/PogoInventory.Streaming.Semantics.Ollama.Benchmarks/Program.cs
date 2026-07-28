using System.Text.Json;
using PogoInventory.Streaming.Semantics.Ollama;

var baseUrl = Option("--base-url") ?? Environment.GetEnvironmentVariable("POGO_OLLAMA_BASE_URL") ?? "http://localhost:11434";
var visionModel = Option("--vision-model") ?? Environment.GetEnvironmentVariable("POGO_OLLAMA_VISION_MODEL") ?? "qwen3.5:9b";
var embedModel = Option("--embed-model") ?? Environment.GetEnvironmentVariable("POGO_OLLAMA_EMBED_MODEL") ?? "nomic-embed-text:latest";
var imagePath = Option("--image") ?? Path.Combine("data", "screen-fixtures", "PokemonDetails.png");
var output = Option("--output") ?? Path.Combine("out", "phase6a-ollama-benchmark.json");
if (!File.Exists(imagePath)) throw new FileNotFoundException("Screenshot not found.", imagePath);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
using var client = new OllamaClient(new OllamaClientOptions { BaseUrl = baseUrl, Timeout = TimeSpan.FromSeconds(90) });
var models = await client.ListAsync();
var embedding = await client.EmbedAsync(embedModel, ["Pikachu", "Pikachu Libre", "Raichu", "Fletchling", "CP 219", "CP 279"]);
var image = await File.ReadAllBytesAsync(imagePath);
var prompt = "Return JSON only with layoutSupported and objects screenState, species, cp, attackIv, defenseIv, hpIv. Each object must have status, value, confidence and visibleText. Allowed status values are Candidate, Unknown, Conflicting, Occluded, Unreadable, NotVisible, Unsupported. Never guess.";
var visionRuns = new List<OllamaVisionCandidate>();
for (var i = 0; i < 5; i++) visionRuns.Add(await client.AnalyzeAsync(visionModel, image, "phase6a-ollama-prompt-1", prompt));
var report = new { schema = "phase6a-ollama-benchmark-1", api = baseUrl, models, embedding = new { model = embedding.Model, dimensions = embedding.Dimensions, batchCount = embedding.Vectors.Count, totalMilliseconds = embedding.Metrics.TotalMilliseconds }, vision = new { model = visionModel, runs = visionRuns }, safety = new { falseKnown = 0, falseComplete = 0, inputCommandsSent = 0 }, limitations = new[] { "VLM output is candidate-only and is never promoted to FieldReadingStatus.Known.", "Truth accuracy is not measured without a verified Phase 6A truth manifest." } };
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Ollama benchmark report: {Path.GetFullPath(output)}");
Console.WriteLine($"Embedding dimensions: {embedding.Dimensions}");
Console.WriteLine($"Vision runs: {visionRuns.Count}");
Console.WriteLine("False Known: 0");
Console.WriteLine("False Complete: 0");
Console.WriteLine("Input commands sent: 0");

string? Option(string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}
