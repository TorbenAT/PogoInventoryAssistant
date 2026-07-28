using PogoInventory.Streaming.Scrcpy;

var tests = new (string Name, Action Run)[]
{
    ("ADB wm size parser uses the physical size line", () =>
    {
        var dimensions = AdbDisplayDimensionParser.ParseWmSize("Physical size: 1080x2400\nOverride size: 720x1600");
        Assert(dimensions.Width == 720 && dimensions.Height == 1600 && dimensions.Orientation == "Portrait", "override dimensions were not selected");
    }),
    ("Automatic stream dimensions preserve aspect ratio and max size", () =>
    {
        var resolved = StreamDimensionResolver.Resolve(new DisplayDimensions(1080, 2400, "Portrait"), 1920);
        Assert(resolved.Width == 864 && resolved.Height == 1920 && resolved.Source == "AdbWmSize", "automatic dimensions were not scaled deterministically");
    }),
    ("Explicit dimensions require both values", () =>
    {
        try { _ = StreamDimensionResolver.Resolve(new DisplayDimensions(1080, 2400, "Portrait"), 1920, 720, null); throw new Exception("partial override accepted"); }
        catch (StreamTransportException error) { Assert(error.Failure.Code == StreamFailureCode.StreamDimensionMismatch, "wrong failure code"); }
    }),
    ("Explicit dimensions are represented in the result", () =>
    {
        var resolved = StreamDimensionResolver.Resolve(new DisplayDimensions(1080, 2400, "Portrait"), 1920, 720, 1600);
        Assert(resolved.Width == 720 && resolved.Height == 1600 && resolved.Source == "ExplicitOverride", "explicit dimensions were not preserved");
    }),
    ("Invalid display output fails closed", () =>
    {
        try { _ = AdbDisplayDimensionParser.ParseWmSize("unexpected output"); throw new Exception("invalid display output accepted"); }
        catch (FormatException) { }
    }),
    ("Read-only contract exposes zero input", () => Assert(!ScrcpyReadOnlyContract.ControlChannelEnabled && ScrcpyReadOnlyContract.InputCommandsSent == 0, "read-only contract violated")),
    ("Preflight reason codes include all required hardware failures", () =>
    {
        var names = Enum.GetNames<StreamingPreflightReasonCode>();
        foreach (var required in new[] { "AdbUnavailable", "DeviceNotFound", "DeviceUnauthorized", "DeviceOffline", "ScrcpyServerMissing", "ScrcpyVersionMismatch", "FfmpegUnavailable", "OutputDirectoryUnavailable" })
            Assert(names.Contains(required, StringComparer.Ordinal), $"missing reason code {required}");
    })
};

var failures = 0;
foreach (var test in tests)
{
    try { test.Run(); Console.WriteLine($"PASS: {test.Name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL: {test.Name}: {error.Message}"); }
}
Console.WriteLine($"Phase 4 self-test: {tests.Length - failures}/{tests.Length}");
Console.WriteLine("Input commands sent: 0");
return failures;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
