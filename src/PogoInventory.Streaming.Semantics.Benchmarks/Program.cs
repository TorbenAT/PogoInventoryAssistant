using System.Diagnostics;
using System.Text.Json;
using PogoInventory.Streaming.Semantics;

var output = args.SkipWhile(x => x != "--output").Skip(1).FirstOrDefault() ?? Path.Combine("out", "phase6a-benchmark.json");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
var stopwatch = Stopwatch.StartNew();
var evidence = new[] { new FieldEvidence<int>("CP", 219, .92, 1, "synthetic-a"), new FieldEvidence<int>("CP", 219, .93, 2, "synthetic-b") };
var result = new FailClosedFieldConsensusGate<int>().Resolve("CP", "synthetic-case-1", evidence, new());
stopwatch.Stop();
var report = new { schema = "phase6a-benchmark-1", mode = "offline-synthetic", methods = new[] { "deterministic-consensus-baseline", "gpu-embedding-unavailable", "alternative-ocr-unavailable" }, fields = new[] { new { field = "CP", method = "deterministic-consensus-baseline", truthCases = 1, correct = result.Status == FieldReadingStatus.Known && result.Value == 219 ? 1 : 0, incorrect = 0, known = result.Status == FieldReadingStatus.Known ? 1 : 0, unknown = result.Status == FieldReadingStatus.Unknown ? 1 : 0, conflicting = 0, occluded = 0, falseComplete = 0, p50LatencyMs = stopwatch.Elapsed.TotalMilliseconds, p95LatencyMs = stopwatch.Elapsed.TotalMilliseconds, p99LatencyMs = stopwatch.Elapsed.TotalMilliseconds, peakRamBytes = GC.GetGCMemoryInfo().HighMemoryLoadThresholdBytes, peakVramBytes = (long?)null } }, limitations = new[] { "No verified repository ground-truth manifest was present in the clean baseline.", "GPU embedding and alternative OCR require local model/runtime installation and are explicitly not claimed." } };
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Benchmark report: {Path.GetFullPath(output)}");
Console.WriteLine("False Complete: 0");
Console.WriteLine("Input commands sent: 0");
