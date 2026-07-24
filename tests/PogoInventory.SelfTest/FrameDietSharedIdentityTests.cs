using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Automation.Models;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task D: <see cref="AndroidVerifiedInventoryNamedOperations.CaptureCurrentCleanupAppraisalAsync"/>
/// re-captured a fresh "carousel-appraisal-observation" stability window even
/// though <see cref="AndroidVerifiedInventoryNamedOperations.CaptureCleanupAppraisalIdentityAsync"/>
/// just established the exact same stable AppraisalBars frame one step
/// earlier in the cleanup-proof per-item loop (no phone input between them).
/// These tests exercise the real named operations against a synthetic
/// AppraisalBars screenshot (crafted to satisfy the real
/// <c>AppraisalAnalyzer</c>) with a counting fake transport, proving: (1) the
/// shared-frame path produces the same IV analysis a separate capture of the
/// identical bytes would, (2) it spends zero additional captures, (3) the
/// fallback path (no supplied identity capture) is unchanged, and (4)
/// chaining identity capture + shared-frame IV + advance for one steady-state
/// item costs exactly 7 captures (3 identity + 0 IV + 1 swipe-authorization +
/// 3 post-swipe).
/// </summary>
internal static class FrameDietSharedIdentityTests
{
    public static async Task RunAsync()
    {
        await SharedIdentityFrameSkipsRedundantIvWindowAsync();
        await FallbackWithoutIdentityCapturesOwnWindowAsync();
        await SteadyStateItemCosts7CapturesAsync();
    }

    public static async Task SharedIdentityFrameSkipsRedundantIvWindowAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var frame = CreateAppraisalBarsFrame();
            var transport = new CountingCaptureTransport(frame);
            var operations = CreateOperations(transport, directory);

            var identity = await operations.CaptureCleanupAppraisalIdentityAsync(CancellationToken.None);
            AssertTrue(
                identity.Status == CleanupProofObservationStatus.Complete,
                "the fixture frame must resolve to a stable, complete identity capture");
            AssertTrue(identity.StableScreenshot is not null, "identity capture must retain its stable screenshot bytes");
            var captureCountAfterIdentity = transport.CaptureCount;

            var shared = await operations.CaptureCurrentCleanupAppraisalAsync(identity, CancellationToken.None);

            AssertTrue(
                transport.CaptureCount == captureCountAfterIdentity,
                $"supplying the just-confirmed identity capture must skip the redundant IV capture window " +
                $"(expected 0 additional captures, got {transport.CaptureCount - captureCountAfterIdentity})");
            AssertEqual(identity.Consensus.StableFingerprintSha256, shared.StableFingerprintSha256,
                "IV analysis reuses the identity's fingerprint (same underlying frame)");

            // Ground truth: running the real AppraisalAnalyzer directly on the
            // identity's stable bytes must produce exactly the same IV triple
            // the shared-frame path reports, proving the reuse is not lossy.
            var direct = new AppraisalAnalyzer().Analyze(
                PngDecoder.Decode(identity.StableScreenshot!), CreateAppraisalProfile(), allowComplete: true);
            AssertEqual(direct.AttackIv, shared.AttackIv, "shared-frame AttackIv matches direct analysis of the same bytes");
            AssertEqual(direct.DefenseIv, shared.DefenseIv, "shared-frame DefenseIv matches direct analysis of the same bytes");
            AssertEqual(direct.HpIv, shared.HpIv, "shared-frame HpIv matches direct analysis of the same bytes");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    public static async Task FallbackWithoutIdentityCapturesOwnWindowAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var frame = CreateAppraisalBarsFrame();
            var transport = new CountingCaptureTransport(frame);
            var operations = CreateOperations(transport, directory);

            var result = await operations.CaptureCurrentCleanupAppraisalAsync(
                confirmedIdentityCapture: null, CancellationToken.None);

            AssertTrue(result.AttackIv is not null && result.DefenseIv is not null && result.HpIv is not null,
                "regression path must still succeed with real IV data");
            AssertTrue(
                transport.CaptureCount == 3,
                $"without a supplied identity capture the IV observation window must still be captured " +
                $"(expected 3 captures, got {transport.CaptureCount})");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    public static async Task SteadyStateItemCosts7CapturesAsync()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var frame = CreateAppraisalBarsFrame();
            var transport = new CountingCaptureTransport(frame);
            var operations = CreateOperations(transport, directory);

            var identity = await operations.CaptureCleanupAppraisalIdentityAsync(CancellationToken.None);
            AssertTrue(identity.Status == CleanupProofObservationStatus.Complete, "identity must be stable");
            // Task D change 1b (dropping the tag probe for ordinal>1) is a
            // runner-level decision, not a named-operation capture, so it
            // contributes 0 captures here by construction - nothing to call.
            var shared = await operations.CaptureCurrentCleanupAppraisalAsync(identity, CancellationToken.None);
            AssertTrue(shared.StableFingerprintSha256 is not null, "shared IV capture must carry a fingerprint for the advance below");

            var advanced = await operations.AdvanceToNextPokemonInAppraisalAsync(
                "a-fingerprint-that-does-not-match-the-supplied-frame", shared, CancellationToken.None);
            AssertTrue(
                advanced == AppraisalCarouselAdvanceResult.SUCCESS_CHANGED_POKEMON,
                $"advance must still verify the swipe with real post-swipe data, got {advanced}");

            AssertTrue(
                transport.CaptureCount == 7,
                $"steady-state item (identity + shared IV + advance) must cost exactly 7 captures " +
                $"(3 identity + 0 IV + 1 swipe-authorization + 3 post-swipe), got {transport.CaptureCount}");
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
                Name = "frame-diet-shared-identity-test",
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
        ProfileId = "frame-diet-shared-identity-test-profile",
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
        Paint(rgba, width, height, left, top, left + barWidth, top + barHeight, r: 200, g: 200, b: 200);
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
        private byte[] _frame;
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
        var path = Path.Combine(Path.GetTempPath(), "pogo-frame-diet-shared-identity-selftest", Guid.NewGuid().ToString("N"));
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

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!Equals(expected, actual))
            throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'.");
    }
}
