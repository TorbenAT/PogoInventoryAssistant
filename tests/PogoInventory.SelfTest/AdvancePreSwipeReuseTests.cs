using PogoInventory.Appraisal.Models;
using PogoInventory.Automation.Models;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task 3: <see cref="AndroidVerifiedInventoryNamedOperations.AdvanceToNextPokemonInAppraisalAsync"/>
/// re-captured a fresh pre-swipe stability window even though
/// <see cref="AndroidVerifiedInventoryNamedOperations.CaptureCurrentCleanupAppraisalAsync"/>
/// just established a stable, fingerprinted AppraisalBars frame for the same
/// Pokémon one step earlier in the cleanup-proof per-item loop. These tests
/// exercise the real named operation against a synthetic AppraisalBars
/// screenshot (crafted to satisfy the real <c>AppraisalAnalyzer</c>) with a
/// counting fake transport, so the capture count proves the redundant
/// pre-swipe window is skipped when the immediately-preceding capture is
/// supplied, and unchanged when it is not.
/// </summary>
internal static class AdvancePreSwipeReuseTests
{
    public static async Task RunAsync()
    {
        await AdvanceReusesSuppliedPreSwipeFrameAsync();
        await AdvanceWithoutSuppliedFrameCapturesPreSwipeWindowAsync();
    }

    public static async Task AdvanceReusesSuppliedPreSwipeFrameAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var frame = CreateAppraisalBarsFrame();
            var transport = new CountingCaptureTransport(frame);
            var operations = CreateOperations(transport, directory);

            // Establish the stable frame exactly like the cleanup-proof
            // per-item loop does one step earlier.
            var confirmed = await operations.CaptureCurrentCleanupAppraisalAsync(CancellationToken.None);
            AssertTrue(confirmed.StableFingerprintSha256 is not null,
                "the fixture frame must be confirmed stable before the reuse can be exercised");
            var captureCountAfterConfirm = transport.CaptureCount;

            var result = await operations.AdvanceToNextPokemonInAppraisalAsync(
                "a-fingerprint-that-does-not-match-the-supplied-frame",
                confirmed,
                CancellationToken.None);

            AssertTrue(
                result == AppraisalCarouselAdvanceResult.SUCCESS_CHANGED_POKEMON,
                $"advance must still verify the swipe with real post-swipe data, got {result}");
            var capturesDuringAdvance = transport.CaptureCount - captureCountAfterConfirm;
            AssertTrue(
                capturesDuringAdvance == 4,
                $"supplying the just-confirmed frame must skip the redundant pre-swipe window " +
                $"(expected 1 authorization capture + 3 post-swipe captures = 4, got {capturesDuringAdvance})");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    public static async Task AdvanceWithoutSuppliedFrameCapturesPreSwipeWindowAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var frame = CreateAppraisalBarsFrame();
            var transport = new CountingCaptureTransport(frame);
            var operations = CreateOperations(transport, directory);

            var result = await operations.AdvanceToNextPokemonInAppraisalAsync(
                "a-fingerprint-that-does-not-match-the-captured-frame",
                confirmedPreSwipeCapture: null,
                CancellationToken.None);

            AssertTrue(
                result == AppraisalCarouselAdvanceResult.SUCCESS_CHANGED_POKEMON,
                $"regression path must still succeed with real data, got {result}");
            AssertTrue(
                transport.CaptureCount == 7,
                $"without a supplied frame the pre-swipe window must still be captured " +
                $"(expected 3 pre-swipe + 1 authorization + 3 post-swipe = 7, got {transport.CaptureCount})");
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
                Name = "advance-pre-swipe-reuse-test",
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
                StateTimeoutSeconds = 2,
                PostActionSettleMilliseconds = 0
            },
            evidenceDirectory,
            appraisalProfile: CreateAppraisalProfile());

    /// <summary>
    /// A minimal, self-contained appraisal profile whose bar regions match
    /// <see cref="CreateAppraisalBarsFrame"/>'s painted geometry closely enough
    /// that the real <c>AppraisalAnalyzer</c> reports IsAppraisal with
    /// Confidence &gt;= 0.90 (the threshold <c>GuardedInventoryRecovery.Observe</c>
    /// requires to classify a frame as <see cref="RecoveryFrameKind.AppraisalBars"/>).
    /// </summary>
    private static AppraisalVisualProfile CreateAppraisalProfile() => new()
    {
        ProfileId = "advance-pre-swipe-reuse-test-profile",
        Bars = new[]
        {
            new AppraisalBarDefinition
            {
                Kind = AppraisalBarKind.Attack,
                Region = new NormalizedRegion { X = 0.08, Y = 0.58, Width = 0.70, Height = 0.05 }
            },
            new AppraisalBarDefinition
            {
                Kind = AppraisalBarKind.Defense,
                Region = new NormalizedRegion { X = 0.08, Y = 0.66, Width = 0.70, Height = 0.05 }
            },
            new AppraisalBarDefinition
            {
                Kind = AppraisalBarKind.Hp,
                Region = new NormalizedRegion { X = 0.08, Y = 0.74, Width = 0.70, Height = 0.05 }
            }
        },
        SearchXOffsets = new[] { 0.0 },
        SearchYOffsets = new[] { 0.0 },
        SearchScales = new[] { 1.0 },
        Colors = new AppraisalColorProfile()
    };

    /// <summary>
    /// Paints three horizontal track/orange-fill bars over a non-light
    /// background at the normalized regions <see cref="CreateAppraisalProfile"/>
    /// expects, so the real <c>AppraisalAnalyzer</c> confidently detects all
    /// three IV bars (see AppraisalAnalyzer.Measure / IsTrack / IsOrangeFill).
    /// </summary>
    private static byte[] CreateAppraisalBarsFrame()
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: 80, g: 120, b: 150);
        PaintBar(rgba, width, height, 0.58);
        PaintBar(rgba, width, height, 0.66);
        PaintBar(rgba, width, height, 0.74);
        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    private static void PaintBar(byte[] rgba, int width, int height, double top)
    {
        const double left = 0.08;
        const double barWidth = 0.70;
        const double barHeight = 0.05;
        // Track (unfilled) color across the whole bar region.
        Paint(rgba, width, height, left, top, left + barWidth, top + barHeight, r: 200, g: 200, b: 200);
        // Orange fill over the left half.
        Paint(rgba, width, height, left, top, left + barWidth * 0.5, top + barHeight, r: 220, g: 150, b: 60);
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
    /// can prove exactly how many screenshots each path took.
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
            string serial, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(serial, Serial, StringComparison.Ordinal))
                throw new DeviceHarnessException(DeviceErrorCode.RequestedDeviceNotFound, "not needed by this test");
            return Task.FromResult(new AndroidDeviceMetadata
            {
                Serial = Serial,
                Manufacturer = "Test",
                Model = "Test Android",
                Product = "test product",
                DeviceName = "test-device",
                AndroidVersion = "16",
                ApiLevel = 36,
                BuildFingerprint = "test/fingerprint/0.1",
                Screen = new AndroidScreenInfo { PhysicalWidth = 1080, PhysicalHeight = 2340 },
                Battery = new AndroidBatteryInfo
                {
                    LevelPercent = 90,
                    TemperatureCelsius = 28.0m,
                    StatusCode = 2,
                    StatusName = "Charging",
                    UsbPowered = true,
                    Present = true,
                    Technology = "Li-ion"
                },
                CapturedAtUtc = new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero)
            });
        }

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
            Task.CompletedTask;
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "pogo-advance-pre-swipe-reuse-selftest", Guid.NewGuid().ToString("N"));
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
