using System.Buffers;
using PogoInventory.Streaming;
using PogoInventory.Streaming.Semantics;
using PogoInventory.Streaming.Semantics.Shadow;
using PogoInventory.Vision.Models;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Frame factory copies evidence and releases lease independently", TestFrameFactory),
    ("Runner respects maximum frame bound", TestBoundedRunner),
    ("Existing semantic analyzer adapter remains fail closed", TestSemanticAdapter),
    ("Conflicting analyzers are reported, not resolved", TestConflict),
    ("Reference agreement is recorded", TestReferenceAgreement),
    ("Analyzer exceptions become fault evidence", TestFault),
    ("Analyzer timeout becomes timeout evidence", TestTimeout),
    ("Result ordering is deterministic", TestDeterminism),
    ("Report writer produces bounded JSON and Markdown", TestReportWriter),
    ("Shadow path cannot authorize phone input", TestReadOnlyContract)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {test.Name}: {error.Message}");
    }
}

Console.WriteLine($"Phase 6B self-test: {tests.Length - failures}/{tests.Length}");
Console.WriteLine("Input commands sent: 0");
return failures;

static Task TestFrameFactory()
{
    var bytes = Enumerable.Range(0, 16).Select(x => (byte)x).ToArray();
    using var lease = new TestLease(CreateMetadata(1), bytes);
    var frame = ShadowFrameFactory.Capture(lease, Regions());
    Assert(frame.Pixels.Span.SequenceEqual(bytes), "pixels were not copied");
    Assert(frame.EvidenceHash.Length == 64, "SHA-256 evidence hash missing");
    frame.Validate();
    return Task.CompletedTask;
}

static async Task TestBoundedRunner()
{
    var analyzer = KnownAnalyzer("known", "CP", "219");
    var report = await new SemanticShadowRunner().RunAsync(
        "bounded",
        Frames(CreateFrame(1), CreateFrame(2), CreateFrame(3)),
        new[] { analyzer },
        options: new SemanticShadowOptions
        {
            MaximumFrames = 2,
            MaximumDuration = TimeSpan.FromSeconds(2),
            AnalyzerTimeout = TimeSpan.FromSeconds(1)
        });
    Assert(report.Frames.Count == 2, "maximum frame bound was ignored");
}

static async Task TestSemanticAdapter()
{
    var adapter = new SemanticFieldAnalyzerAdapter<string>(new UnsupportedFieldAnalyzer<string>("Species"));
    var readings = await adapter.AnalyzeAsync(CreateFrame(1));
    Assert(readings.Count == 1 && readings[0].Status == FieldReadingStatus.Unsupported, "unsupported became known");
}

static async Task TestConflict()
{
    var report = await new SemanticShadowRunner().RunAsync(
        "conflict",
        Frames(CreateFrame(1)),
        new[]
        {
            KnownAnalyzer("a", "CP", "219"),
            KnownAnalyzer("b", "CP", "279")
        });
    Assert(report.Frames.Single().Comparisons.Single().Kind == ShadowComparisonKind.AnalyzerConflict, "conflict was not retained");
    Assert(report.ComparisonConflicts == 1, "conflict count is wrong");
}

static async Task TestReferenceAgreement()
{
    var report = await new SemanticShadowRunner().RunAsync(
        "reference",
        Frames(CreateFrame(1)),
        new[] { KnownAnalyzer("a", "CP", "219") },
        new StaticReferenceProvider("CP", "219"));
    Assert(report.Frames.Single().Comparisons.Single().Kind == ShadowComparisonKind.Agreement, "reference agreement missing");
}

static async Task TestFault()
{
    var faulty = new DelegateShadowAnalyzer(
        "faulty",
        (_, _) => ValueTask.FromException<IReadOnlyList<ShadowFieldCandidate>>(new InvalidOperationException("expected")));
    var report = await new SemanticShadowRunner().RunAsync("fault", Frames(CreateFrame(1)), new[] { faulty });
    Assert(report.AnalyzerFaults == 1, "fault was not reported");
    Assert(report.KnownCandidates == 0, "fault produced a known candidate");
}

static async Task TestTimeout()
{
    var slow = new DelegateShadowAnalyzer(
        "slow",
        async (_, token) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            return Array.Empty<ShadowFieldCandidate>();
        });
    var report = await new SemanticShadowRunner().RunAsync(
        "timeout",
        Frames(CreateFrame(1)),
        new[] { slow },
        options: new SemanticShadowOptions
        {
            MaximumDuration = TimeSpan.FromSeconds(2),
            AnalyzerTimeout = TimeSpan.FromMilliseconds(25)
        });
    Assert(report.AnalyzerTimeouts == 1, "timeout was not reported");
}

static async Task TestDeterminism()
{
    var report = await new SemanticShadowRunner().RunAsync(
        "order",
        Frames(CreateFrame(2), CreateFrame(1)),
        new[]
        {
            KnownAnalyzer("z", "Species", "Pikachu"),
            KnownAnalyzer("a", "CP", "219")
        });
    var names = report.Frames[0].Executions.Select(x => x.Analyzer).ToArray();
    Assert(names.SequenceEqual(new[] { "a", "z" }), "analyzer order is non-deterministic");
}

static async Task TestReportWriter()
{
    var report = await new SemanticShadowRunner().RunAsync(
        "writer",
        Frames(CreateFrame(1)),
        new[] { KnownAnalyzer("a", "CP", "219") });
    var directory = Path.Combine(Path.GetTempPath(), $"pogo-phase6b-{Guid.NewGuid():N}");
    try
    {
        var paths = await new ShadowReportWriter().WriteAsync(report, directory);
        Assert(File.Exists(paths.JsonPath) && File.Exists(paths.MarkdownPath), "reports were not written");
        Assert(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "temporary report file remained");
    }
    finally
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}

static Task TestReadOnlyContract()
{
    var report = new ShadowSessionReport
    {
        SessionId = "read-only",
        StartedUtc = DateTimeOffset.UtcNow,
        EndedUtc = DateTimeOffset.UtcNow,
        FinalStatus = "Completed",
        Frames = Array.Empty<ShadowFrameResult>()
    };
    Assert(report.InputCommandsSent == 0, "input count is not zero");
    Assert(!report.AuthorizesPhoneInput, "shadow report authorizes input");
    return Task.CompletedTask;
}

static IShadowSemanticAnalyzer KnownAnalyzer(string name, string field, string value) =>
    new DelegateShadowAnalyzer(
        name,
        (frame, token) =>
        {
            token.ThrowIfCancellationRequested();
            IReadOnlyList<ShadowFieldCandidate> candidates = new[]
            {
                new ShadowFieldCandidate(name, field, FieldReadingStatus.Known, value, .95, "TEST_KNOWN", frame.FrameId, frame.EvidenceHash)
            };
            return ValueTask.FromResult(candidates);
        });

static async IAsyncEnumerable<ShadowFrameInput> Frames(params ShadowFrameInput[] frames)
{
    foreach (var frame in frames)
    {
        await Task.Yield();
        yield return frame;
    }
}

static ShadowFrameInput CreateFrame(long id)
{
    var metadata = CreateMetadata(id);
    var pixels = Enumerable.Repeat((byte)id, metadata.Descriptor.RequiredByteLength).ToArray();
    var semantic = new SemanticFrameObservation(
        id,
        new string('A', 64),
        metadata.Descriptor.Width,
        metadata.Descriptor.Height,
        metadata.Descriptor.Width >= metadata.Descriptor.Height ? "Landscape" : "Portrait",
        Regions());
    return new ShadowFrameInput(semantic, metadata, pixels, new[] { "Test" });
}

static FrameMetadata CreateMetadata(long id) =>
    new(
        new FrameId(id),
        new FrameTimestamp(id, DateTimeOffset.UnixEpoch.AddMilliseconds(id), TimeSpan.FromMilliseconds(id)),
        new FrameDescriptor(2, 2, 8, FramePixelFormat.Bgra32),
        new FrameQuality(.5, .5, 0, 0, .5),
        new FrameStability(0, 3, TimeSpan.FromMilliseconds(150), true),
        "self-test");

static IReadOnlyDictionary<string, NormalizedRegion> Regions() =>
    new Dictionary<string, NormalizedRegion>(StringComparer.Ordinal)
    {
        ["Header"] = new() { X = 0, Y = 0, Width = 1, Height = .2 }
    };

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class TestLease : IFrameLease
{
    private byte[]? _pixels;

    public TestLease(FrameMetadata metadata, byte[] pixels)
    {
        Metadata = metadata;
        _pixels = pixels;
    }

    public FrameMetadata Metadata { get; }
    public ReadOnlyMemory<byte> Pixels => _pixels ?? throw new ObjectDisposedException(nameof(TestLease));
    public void Dispose() => _pixels = null;
}

sealed class StaticReferenceProvider : IShadowReferenceProvider
{
    private readonly string _field;
    private readonly string _value;

    public StaticReferenceProvider(string field, string value)
    {
        _field = field;
        _value = value;
    }

    public string Name => "static-reference";

    public ValueTask<IReadOnlyList<ShadowReferenceReading>> GetReferenceAsync(
        ShadowFrameInput frame,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ShadowReferenceReading> values = new[]
        {
            new ShadowReferenceReading(Name, _field, FieldReadingStatus.Known, _value, 1, "VERIFIED_TEST_REFERENCE")
        };
        return ValueTask.FromResult(values);
    }
}
