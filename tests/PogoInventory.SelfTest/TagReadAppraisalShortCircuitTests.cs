using PogoInventory.Automation.Models;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task 2: <see cref="AndroidVerifiedInventoryNamedOperations.ReadTagObservationAsync"/>
/// must not wait for PokemonDetails (a state that can never appear) while the
/// phone is in the Appraisal carousel, and must never read tag pills from
/// that wrong frame. These tests exercise the real detector against
/// synthetic screenshots crafted to satisfy
/// <see cref="VisualControlLocator"/>'s Appraisal-intro and Details-page
/// heuristics, using a counting fake transport so the capture count proves
/// no PokemonDetails wait loop ran.
/// </summary>
internal static class TagReadAppraisalShortCircuitTests
{
    public static async Task RunAsync()
    {
        await TagReadShortCircuitsInAppraisalCarouselAsync();
        await TagReadStillWorksOnDetailsAsync();
    }

    public static async Task TagReadShortCircuitsInAppraisalCarouselAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var transport = new CountingCaptureTransport(CreateAppraisalFrame());
            var operations = CreateOperations(transport, directory);

            var observation = await operations.ReadTagObservationAsync(CancellationToken.None);

            AssertTrue(
                observation.Evidence.Contains(
                    AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason),
                "appraisal-carousel tag read must be marked with the skip reason");
            AssertTrue(!observation.NamesComplete, "skipped tag read cannot claim complete names");
            AssertTrue(observation.TagCount == 0, "skipped tag read reports no tags");
            AssertTrue(
                transport.CaptureCount == 1,
                $"appraisal-carousel short circuit must capture exactly one probe frame, captured {transport.CaptureCount}");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    public static async Task TagReadStillWorksOnDetailsAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var transport = new CountingCaptureTransport(CreateDetailsFrame());
            var operations = CreateOperations(transport, directory);

            var observation = await operations.ReadTagObservationAsync(CancellationToken.None);

            AssertTrue(
                !observation.Evidence.Contains(
                    AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason),
                "Details-page tag read must not carry the appraisal skip reason");
            AssertTrue(
                observation.Evidence.Contains("details-tag-pills"),
                "Details-page tag read keeps its existing evidence tag");
            AssertTrue(
                transport.CaptureCount > 1,
                "Details-page path still performs the bounded PokemonDetails wait (probe + settle captures)");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static AndroidVerifiedInventoryNamedOperations CreateOperations(
        IAndroidAutomationTransport transport, string evidenceDirectory) =>
        new(
            transport,
            "TEST-SERIAL",
            new InventoryAutomationProfile
            {
                Name = "tag-read-short-circuit-test",
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
                StateTimeoutSeconds = 2
            },
            evidenceDirectory);

    /// <summary>
    /// Satisfies <see cref="VisualControlLocator.LocateAppraisalIntroContinue"/>
    /// (a light dialog band low on screen over a blue-tinted overlay), which is
    /// the first check <see cref="PokemonGoGameStateDetector.Detect"/> performs
    /// when no appraisal bars profile is configured.
    /// </summary>
    private static byte[] CreateAppraisalFrame()
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 80, g: 120, b: 150);
        Paint(rgba, width, height, 0.02, 0.82, 0.98, 0.97, r: 200, g: 200, b: 200);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

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
    /// Always returns the same fixed frame and counts capture calls so tests
    /// can prove no PokemonDetails wait loop ran after the appraisal-carousel
    /// short circuit.
    /// </summary>
    private sealed class CountingCaptureTransport : IAndroidAutomationTransport
    {
        private readonly byte[] _frame;
        private const string Serial = "TEST-SERIAL";

        public CountingCaptureTransport(byte[] frame) => _frame = frame;

        public int CaptureCount { get; private set; }

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
            CaptureCount++;
            return Task.FromResult(_frame.ToArray());
        }

        public Task<string> CaptureUiHierarchyAsync(string serial, CancellationToken cancellationToken = default) =>
            Task.FromResult("<hierarchy rotation=\"0\"><node class=\"test\" /></hierarchy>");

        public Task PressBackAsync(string serial, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no input expected by this test");

        public Task OpenPokemonInventoryAsync(string serial, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no input expected by this test");

        public Task EnterInventorySearchQueryAsync(
            string serial, string query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no input expected by this test");

        public Task SubmitInventorySearchQueryAsync(string serial, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no input expected by this test");

        public Task TapAsync(string serial, int x, int y, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no input expected by this test");

        public Task SwipeAsync(
            string serial, int startX, int startY, int endX, int endY, int durationMilliseconds,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("no input expected by this test");
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pogo-tag-read-selftest", Guid.NewGuid().ToString("N"));
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
