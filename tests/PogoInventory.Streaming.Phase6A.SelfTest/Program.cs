using PogoInventory.Streaming.Semantics;
using PogoInventory.Vision.Models;

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
};
var failures = 0;
foreach (var test in tests) try { test.Run(); Console.WriteLine($"PASS: {test.Name}"); } catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL: {test.Name}: {ex.Message}"); }
Console.WriteLine($"Phase 6A self-test: {tests.Length - failures}/{tests.Length}");
Console.WriteLine("Input commands sent: 0");
return failures;
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
