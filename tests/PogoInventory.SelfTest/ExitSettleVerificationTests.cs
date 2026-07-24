using PogoInventory.Automation.Models;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task G: <see cref="AndroidVerifiedInventoryNamedOperations.VerifyPokemonDetailsSettledAsync"/>
/// is the exit-tap sibling of <see cref="AndroidVerifiedInventoryNamedOperations.VerifyGameplayMapSettledAsync"/>
/// (both share the same private <c>VerifySettledStateAsync</c> core). These
/// tests exercise the real named operation against a counting/stepping fake
/// transport, so the observed-state trail proves: (1) a single or double
/// PokemonDetails frame is not enough -- 3 CONSECUTIVE detections are
/// required, an interruption resets the streak; (2) the poll is bounded by
/// <c>StateTimeoutSeconds</c> and returns Verified=false rather than looping
/// forever; (3) it never sends input (the fake transport throws on any
/// Tap/Swipe/PressBack call).
/// </summary>
internal static class ExitSettleVerificationTests
{
    public static async Task RunAsync()
    {
        await RequiresThreeConsecutiveDetailsFramesAsync();
        await IsBoundedByDeadlineWhenNeverSettledAsync();
    }

    /// <summary>
    /// The transport yields Details, Details, NonDetails, Details, Details,
    /// Details -- a two-frame streak that gets interrupted before it reaches
    /// three, followed by the real three-in-a-row. Verified must only become
    /// true after the LAST three frames, proving the consensus rule resets on
    /// interruption instead of accepting any 3 Details observations overall.
    /// </summary>
    public static async Task RequiresThreeConsecutiveDetailsFramesAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var details = CreateDetailsFrame();
            var notDetails = CreateNonDetailsFrame();
            var transport = new SteppingCaptureTransport(new[]
            {
                details, details, notDetails, details, details, details
            });
            var operations = CreateOperations(transport, directory, stateTimeoutSeconds: 5);

            var result = await operations.VerifyPokemonDetailsSettledAsync(CancellationToken.None);

            AssertTrue(result.Verified, "three consecutive Details frames at the end must verify");
            AssertTrue(
                result.ObservedStates.Count == 6,
                $"verification must stop exactly at the third consecutive Details frame, observed {result.ObservedStates.Count}");
            AssertTrue(
                result.ObservedStates.SkipLast(3).Any(state => state != "PokemonDetails"),
                "the interrupting frame must be recorded, proving the earlier two-frame streak did not count");
            AssertTrue(transport.CaptureCount == 6, "no extra frames captured once the trailing streak of three is confirmed");
            AssertTrue(transport.InputCallCount == 0, "verification must never send input");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// The transport never yields a Details frame. With a short bounded
    /// deadline the verification must give up as Verified=false instead of
    /// polling forever, and must still have sent zero input.
    /// </summary>
    public static async Task IsBoundedByDeadlineWhenNeverSettledAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var notDetails = CreateNonDetailsFrame();
            var transport = new SteppingCaptureTransport(new[] { notDetails });
            var operations = CreateOperations(transport, directory, stateTimeoutSeconds: 2);

            var result = await operations.VerifyPokemonDetailsSettledAsync(CancellationToken.None);

            AssertTrue(!result.Verified, "an unsettled phone must never be declared verified");
            AssertTrue(result.ObservedStates.Count > 0, "at least one frame must have been captured and recorded");
            AssertTrue(
                result.ObservedStates.All(state => state != "PokemonDetails"),
                "no Details frame was ever observed in this scenario");
            AssertTrue(transport.InputCallCount == 0, "a bounded, unverified deadline must never send input");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static AndroidVerifiedInventoryNamedOperations CreateOperations(
        IAndroidAutomationTransport transport, string evidenceDirectory, int stateTimeoutSeconds) =>
        new(
            transport,
            "TEST-SERIAL",
            new InventoryAutomationProfile
            {
                Name = "exit-settle-verification-test",
                FirstInventoryCard = new NormalizedPoint { X = 0.18, Y = 0.72 },
                DetailsMenuButton = new NormalizedPoint { X = 0.92, Y = 0.08 },
                AppraiseMenuItem = new NormalizedPoint { X = 0.82, Y = 0.58 },
                NextPokemonSwipe = new NormalizedSwipe
                {
                    Start = new NormalizedPoint { X = 0.78, Y = 0.48 },
                    End = new NormalizedPoint { X = 0.22, Y = 0.48 },
                    DurationMilliseconds = 250
                },
                IdentityRegion = new NormalizedRegion { X = 0.05, Y = 0.35, Width = 0.25, Height = 0.2 },
                IdentityFingerprintMode = FingerprintMode.Color,
                IdentityFingerprintWidth = 12,
                IdentityFingerprintHeight = 12,
                SamePokemonSimilarityThreshold = 0.995,
                StatePollMilliseconds = 100,
                StateTimeoutSeconds = stateTimeoutSeconds
            },
            evidenceDirectory);

    /// <summary>
    /// Satisfies <see cref="VisualControlLocator.LocateDetailsPageTopology"/>'s
    /// fallback branch (blue header band plus a light lower panel) while
    /// staying below the thresholds for every other known screen so the
    /// detector resolves to PokemonDetails.
    /// </summary>
    private static byte[] CreateDetailsFrame()
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 220, g: 220, b: 220);
        Paint(rgba, width, height, 0.05, 0.05, 0.95, 0.36, r: 30, g: 40, b: 100);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    /// <summary>
    /// A plain, uniform frame with none of the Details-page anchors, so the
    /// real state detector never classifies it as PokemonDetails.
    /// </summary>
    private static byte[] CreateNonDetailsFrame()
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 30, g: 30, b: 30);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    private static byte[] Fill(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (var index = 0; index < rgba.Length; index += 4)
        {
            rgba[index] = r;
            rgba[index + 1] = g;
            rgba[index + 2] = b;
            rgba[index + 3] = 255;
        }
        return rgba;
    }

    private static void Paint(
        byte[] rgba, int width, int height,
        double left, double top, double right, double bottom, byte r, byte g, byte b)
    {
        var x0 = (int)(width * left);
        var x1 = (int)(width * right);
        var y0 = (int)(height * top);
        var y1 = (int)(height * bottom);
        for (var y = y0; y < y1; y++)
        for (var x = x0; x < x1; x++)
        {
            var offset = (y * width + x) * 4;
            rgba[offset] = r;
            rgba[offset + 1] = g;
            rgba[offset + 2] = b;
            rgba[offset + 3] = 255;
        }
    }

    /// <summary>
    /// Returns frames from a fixed sequence (repeating the last frame once
    /// the sequence is exhausted) and counts both capture calls and any
    /// input call (Tap/Swipe/PressBack/search entry), so a test can prove
    /// exactly how many frames were polled and that zero input was ever
    /// sent.
    /// </summary>
    private sealed class SteppingCaptureTransport : IAndroidAutomationTransport
    {
        private readonly byte[][] _frames;
        private const string Serial = "TEST-SERIAL";

        public SteppingCaptureTransport(byte[][] frames) => _frames = frames;

        public int CaptureCount { get; private set; }
        public int InputCallCount { get; private set; }

        public Task<IReadOnlyList<AndroidDeviceDescriptor>> ListDevicesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AndroidDeviceDescriptor>>(Array.Empty<AndroidDeviceDescriptor>());

        public Task<AndroidDeviceMetadata> ReadMetadataAsync(
            string serial, CancellationToken cancellationToken = default) =>
            throw new DeviceHarnessException(DeviceErrorCode.RequestedDeviceNotFound, "not needed by this test");

        public Task<byte[]> CaptureScreenshotPngAsync(string serial, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(serial, Serial, StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected serial '{serial}'.", nameof(serial));
            var index = Math.Min(CaptureCount, _frames.Length - 1);
            CaptureCount++;
            return Task.FromResult(_frames[index].ToArray());
        }

        public Task<string> CaptureUiHierarchyAsync(string serial, CancellationToken cancellationToken = default) =>
            Task.FromResult("<hierarchy rotation=\"0\"><node class=\"test\" /></hierarchy>");

        public Task PressBackAsync(string serial, CancellationToken cancellationToken = default)
        {
            InputCallCount++;
            throw new InvalidOperationException("no input expected by this test");
        }

        public Task OpenPokemonInventoryAsync(string serial, CancellationToken cancellationToken = default)
        {
            InputCallCount++;
            throw new InvalidOperationException("no input expected by this test");
        }

        public Task EnterInventorySearchQueryAsync(
            string serial, string query, CancellationToken cancellationToken = default)
        {
            InputCallCount++;
            throw new InvalidOperationException("no input expected by this test");
        }

        public Task SubmitInventorySearchQueryAsync(string serial, CancellationToken cancellationToken = default)
        {
            InputCallCount++;
            throw new InvalidOperationException("no input expected by this test");
        }

        public Task TapAsync(string serial, int x, int y, CancellationToken cancellationToken = default)
        {
            InputCallCount++;
            throw new InvalidOperationException("no input expected by this test");
        }

        public Task SwipeAsync(
            string serial, int startX, int startY, int endX, int endY, int durationMilliseconds,
            CancellationToken cancellationToken = default)
        {
            InputCallCount++;
            throw new InvalidOperationException("no input expected by this test");
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pogo-exit-settle-verification-selftest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
