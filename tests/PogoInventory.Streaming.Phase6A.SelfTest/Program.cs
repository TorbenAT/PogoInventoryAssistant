using PogoInventory.Streaming.Semantics;
using PogoInventory.Vision.Models;
using PogoInventory.Streaming.Semantics.Ollama;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("Known readings require value and evidence", () => { var r = new FieldReading<int>("CP", FieldReadingStatus.Known, 219, .9, new[] { 1L, 2L }, new[] { "a", "b" }, "TEST", new Dictionary<string,string>()); r.Validate(); }),
    ("Unknown is preserved", () => { var r = new FailClosedFieldConsensusGate<int>().Resolve("CP", "run:1", Array.Empty<FieldEvidence<int>>(), new()); Assert(r.Status == FieldReadingStatus.Unknown && EqualityComparer<int>.Default.Equals(r.Value, default), "unknown guessed"); }),
    ("Two agreeing frames become known", () => { var e = new[] { new FieldEvidence<int>("CP", 219, .9, 2, "b"), new FieldEvidence<int>("CP", 219, .95, 1, "a") }; var r = new FailClosedFieldConsensusGate<int>().Resolve("CP", "run:1", e, new()); Assert(r.Status == FieldReadingStatus.Known && r.Value == 219, "consensus failed"); }),
    ("Conflicting high confidence frames fail closed", () => { var e = new[] { new FieldEvidence<int>("CP", 219, .9, 1, "a"), new FieldEvidence<int>("CP", 279, .9, 2, "b") }; var r = new FailClosedFieldConsensusGate<int>().Resolve("CP", "run:1", e, new()); Assert(r.Status == FieldReadingStatus.Conflicting && EqualityComparer<int>.Default.Equals(r.Value, default), "conflict guessed"); }),
    ("Low confidence does not become known", () => { var e = new[] { new FieldEvidence<int>("CP", 219, .79, 1, "a"), new FieldEvidence<int>("CP", 219, .79, 2, "b") }; var r = new FailClosedFieldConsensusGate<int>().Resolve("CP", "run:1", e, new()); Assert(r.Status == FieldReadingStatus.Unknown, "low confidence accepted"); }),
    ("Occlusion remains occluded", () => { var e = new[] { new FieldEvidence<int>("CP", 219, .9, 1, "a", FieldReadingStatus.Occluded), new FieldEvidence<int>("CP", 219, .9, 2, "b", FieldReadingStatus.Known) }; var r = new FailClosedFieldConsensusGate<int>().Resolve("CP", "run:1", e, new()); Assert(r.Status == FieldReadingStatus.Occluded, "occlusion lost"); }),
    ("ROI layout validation is fail closed", () => { var regions = new[] { new SemanticRegion(SemanticRegionKind.CpRegion, new NormalizedRegion { X = .1, Y = .1, Width = .2, Height = .1 }) }; Assert(SemanticLayoutValidator.IsSupported(1080, 2340, "Portrait", regions), "valid ROI rejected"); Assert(!SemanticLayoutValidator.IsSupported(1080, 2340, "Landscape", regions), "wrong orientation accepted"); }),
    ("Unsupported analyzer never produces a value", () => { var f = new SemanticFrameObservation(1, "hash", 1080, 2340, "Portrait", new Dictionary<string,NormalizedRegion>()); var r = new UnsupportedFieldAnalyzer<string>("Species").Analyze(f); Assert(r.Status == FieldReadingStatus.Unsupported && r.Value is null, "unsupported value produced"); })
    ,("Ollama model catalog parses capabilities", () => OllamaCatalogTest().GetAwaiter().GetResult())
    ,("Ollama embeddings validate constant dimensions", () => OllamaEmbeddingTest().GetAwaiter().GetResult())
    ,("Ollama vision candidate never becomes Known", () => OllamaVisionTest().GetAwaiter().GetResult())
    ,("Ollama invalid response fails closed", () => OllamaInvalidVisionTest().GetAwaiter().GetResult())
};
var failures = 0;
foreach (var test in tests) try { test.Run(); Console.WriteLine($"PASS: {test.Name}"); } catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL: {test.Name}: {ex.Message}"); }
Console.WriteLine($"Phase 6A self-test: {tests.Length - failures}/{tests.Length}");
Console.WriteLine("Input commands sent: 0");
return failures;
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

static async Task OllamaCatalogTest()
{
    using var client = new OllamaClient(new() { BaseUrl = "http://fake" }, new HttpClient(new FakeOllamaHandler()));
    var models = await client.ListAsync();
    Assert(models.Count == 2 && models.Single(x => x.Name == "vision:latest").IsVision && models.Single(x => x.Name == "embed:latest").IsEmbedding, "capabilities were not parsed");
}

static async Task OllamaEmbeddingTest()
{
    using var client = new OllamaClient(new() { BaseUrl = "http://fake" }, new HttpClient(new FakeOllamaHandler()));
    var result = await client.EmbedAsync("embed:latest", new[] { "Pikachu", "Raichu" });
    Assert(result.Dimensions == 3 && result.Vectors.Count == 2, "embedding shape was not validated");
}

static async Task OllamaVisionTest()
{
    using var client = new OllamaClient(new() { BaseUrl = "http://fake" }, new HttpClient(new FakeOllamaHandler()));
    var result = await client.AnalyzeAsync("vision:latest", new byte[] { 1, 2, 3 }, "test", "json");
    Assert(result.Species.Status == OllamaCandidateStatus.Candidate && result.Cp.Status == OllamaCandidateStatus.Unknown, "candidate was not preserved");
}

static async Task OllamaInvalidVisionTest()
{
    using var client = new OllamaClient(new() { BaseUrl = "http://fake" }, new HttpClient(new FakeOllamaHandler { InvalidVision = true }));
    var result = await client.AnalyzeAsync("vision:latest", new byte[] { 1 }, "test", "json");
    Assert(result.ReasonCode == "INVALID_MODEL_RESPONSE" && result.Species.Status == OllamaCandidateStatus.Unsupported, "invalid response was repaired or guessed");
}

sealed class FakeOllamaHandler : HttpMessageHandler
{
    public bool InvalidVision { get; init; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        var json = path switch
        {
            "/api/tags" => "{\"models\":[{\"name\":\"vision:latest\",\"digest\":\"v\",\"size\":10,\"details\":{\"family\":\"qwen\",\"parameter_size\":\"1B\",\"quantization_level\":\"Q4\"},\"capabilities\":[\"vision\"]},{\"name\":\"embed:latest\",\"digest\":\"e\",\"size\":10,\"details\":{\"family\":\"nomic\"},\"capabilities\":[\"embedding\"]}]}" ,
            "/api/show" => "{\"digest\":\"v\",\"details\":{\"family\":\"qwen\",\"parameter_size\":\"1B\",\"quantization_level\":\"Q4\"},\"capabilities\":[\"vision\"]}",
            "/api/embed" => "{\"embeddings\":[[1,0,0],[0,1,0]]}",
            "/api/chat" when InvalidVision => "{\"message\":{\"content\":\"not-json\"}}",
            "/api/chat" => "{\"message\":{\"content\":\"{\\\"layoutSupported\\\":true,\\\"screenState\\\":{\\\"status\\\":\\\"Candidate\\\",\\\"value\\\":\\\"PokemonDetails\\\",\\\"confidence\\\":0.9},\\\"species\\\":{\\\"status\\\":\\\"Known\\\",\\\"value\\\":\\\"Pikachu\\\",\\\"confidence\\\":0.9},\\\"cp\\\":{\\\"status\\\":\\\"Unknown\\\",\\\"value\\\":null,\\\"confidence\\\":0},\\\"attackIv\\\":{\\\"status\\\":\\\"NotVisible\\\",\\\"value\\\":null,\\\"confidence\\\":0},\\\"defenseIv\\\":{\\\"status\\\":\\\"NotVisible\\\",\\\"value\\\":null,\\\"confidence\\\":0},\\\"hpIv\\\":{\\\"status\\\":\\\"NotVisible\\\",\\\"value\\\":null,\\\"confidence\\\":0},\\\"diagnostics\\\":[] }\"}}",
            _ => "{}"
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
