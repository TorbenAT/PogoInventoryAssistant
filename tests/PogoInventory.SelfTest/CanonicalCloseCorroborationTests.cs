using PogoInventory.Appraisal.Models;
using PogoInventory.Automation.Models;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Models;
using PogoInventory.Exploration.Services;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.SelfTest;

/// <summary>
/// Task J: implements the quadruple-corroborated relaxed branch of
/// <see cref="VisualControlLocator.LocateDetailsPageTopology"/> (three area
/// floors + <see cref="VisualControlLocator.LocateCanonicalCloseControl"/>),
/// measured safe against the full local-data/ corpus in task-I-report.md
/// (0/168 confidently-classified-other-state colliders pass the X signal;
/// all 5 gate-4 evidence frames score X=0.975). It also covers the two
/// gated call sites in <see cref="AndroidVerifiedInventoryNamedOperations"/>
/// that must never treat a confidently-classified other-state frame as
/// Details evidence via the topology fallback.
/// </summary>
internal static class CanonicalCloseCorroborationTests
{
    public static Task RunAsync()
    {
        WarmBackgroundFrameWithCanonicalCloseClassifiesAsDetails();
        WarmBackgroundFrameWithoutCanonicalCloseStaysNull();
        DegenerateModelAreaWithCanonicalCloseAndStrongCpPanelStillNull();
        CleanupIdentityCaptureNeverCountsConfidentMainMenuAsDetails();
        CallSiteDecisionExpressionMinimalHarness();
        EvidenceFramesFileBasedSanityCheck();
        return Task.CompletedTask;
    }

    /// <summary>
    /// modelArea ~0.12 (below the 0.15 primary floor, above the 0.08 relaxed
    /// floor), strong cp/panel (near 1.0, matching the 0.958-0.998 magnitudes
    /// task-I measured on the 5 real evidence frames) and a synthetic
    /// canonical-close X in the lower-centre safe zone. The relaxed branch
    /// must classify, carry "CanonicalCloseCorroborated" evidence, and the
    /// full detector must resolve to PokemonDetails (this frame does not
    /// satisfy any earlier, higher-priority Detect() branch).
    /// </summary>
    private static void WarmBackgroundFrameWithCanonicalCloseClassifiesAsDetails()
    {
        var frame = CreateWarmBackgroundFrame(modelAreaTargetScore: 0.12, includeCanonicalClose: true);
        var located = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(located is not null, "warm-background frame with canonical-close corroboration must classify");
        AssertTrue(located!.Evidence.Contains("CanonicalCloseCorroborated"),
            "relaxed branch must carry CanonicalCloseCorroborated evidence");
        AssertEqual("PokemonDetailsPageTopology", located.ControlName, "relaxed branch control name");
        AssertTrue(located.Target.X == 0.50 && located.Target.Y == 0.40, "relaxed branch target matches primary shape");

        var detection = new PokemonGoGameStateDetector().Detect(frame);
        AssertEqual(PokemonGoGameState.PokemonDetails, detection.State, "full detector must resolve PokemonDetails");
    }

    /// <summary>
    /// Identical frame, but with no canonical-close X drawn: the fourth
    /// signal is load-bearing, so the relaxed branch (and the blue-header
    /// fallback, which this frame also does not satisfy) must both fail.
    /// </summary>
    private static void WarmBackgroundFrameWithoutCanonicalCloseStaysNull()
    {
        var frame = CreateWarmBackgroundFrame(modelAreaTargetScore: 0.12, includeCanonicalClose: false);
        var located = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(located is null, "without the canonical-close X the relaxed branch must not fire");
    }

    /// <summary>
    /// Task H's original <c>DetailsTopologyDegenerateFrameStillNull</c> was
    /// over-determined: its frame also failed the (unrelated) blue-header
    /// fallback, so the null was not attributable to the modelArea axis
    /// specifically. Here modelArea is degenerate (spread-based
    /// <c>IsDetailsModelArea</c> never matches -> ~0 coverage, well below the
    /// 0.08 relaxed floor) while cp/panel are strong (>= 0.50) AND the
    /// canonical-close X is present -- i.e. every other corroboration signal
    /// the relaxed branch needs is satisfied. The result must still be null,
    /// isolating that modelArea's own floor is what gates this frame.
    /// </summary>
    private static void DegenerateModelAreaWithCanonicalCloseAndStrongCpPanelStillNull()
    {
        var frame = CreateWarmBackgroundFrame(modelAreaTargetScore: 0.0, includeCanonicalClose: true);
        var located = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(located is null,
            "degenerate modelArea must remain unclassified even with strong cp/panel and the X present");
    }

    /// <summary>
    /// Change (2), site 1 (<c>CaptureCleanupIdentityAsync</c>). Builds a
    /// synthetic frame confidently classified MainMenu by the real (trusted)
    /// <see cref="PokemonGoGameStateDetector"/> (via
    /// <see cref="VisualControlLocator.LocatePokemonInventory"/>'s background
    /// fallback) that ALSO satisfies the relaxed Details topology branch
    /// (strong cp/panel, modelArea ~0.12, canonical-close X present) -- the
    /// exact shape of a Task-I "collider". Runs the real
    /// <see cref="AndroidVerifiedInventoryNamedOperations"/> against a fake
    /// transport that always returns this frame and asserts the tightened
    /// gate (<c>detection.State == PokemonDetails ||
    /// (detection.State == Unknown &amp;&amp; topology is not null)</c>) does
    /// NOT count it as Details: zero frames captured, and the failure
    /// reason names the confidently-classified state.
    /// </summary>
    private static void CleanupIdentityCaptureNeverCountsConfidentMainMenuAsDetails()
    {
        var frame = CreateMainMenuColliderFrame(modelAreaTargetScore: 0.12, includeCanonicalClose: true);
        // Confirm the frame is a genuine collider shape before trusting the
        // integration assertion below: MainMenu-confident AND topology non-null.
        var detector = new PokemonGoGameStateDetector();
        var detection = detector.Detect(frame);
        AssertEqual(PokemonGoGameState.MainMenu, detection.State, "fixture must be a confident MainMenu collider");
        var topology = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(topology is not null, "fixture must also satisfy the relaxed Details topology branch");

        var directory = CreateTemporaryDirectory();
        try
        {
            var transport = new FixedFrameTransport(frame);
            var operations = new AndroidVerifiedInventoryNamedOperations(
                transport,
                "TEST-SERIAL",
                CreateAutomationProfile(),
                directory);

            var result = operations.CaptureCleanupIdentityAsync(
                maximumFrames: 3,
                minimumCompleteFrames: 3,
                minimumPartialFrames: 2,
                CancellationToken.None).GetAwaiter().GetResult();

            AssertEqual(0, result.ScreenshotPaths.Count, "a confidently-classified MainMenu frame must capture zero Details evidence");
            AssertTrue(
                result.FailureReasons.All(reason => reason.StartsWith("ExpectedPokemonDetails:MainMenu", StringComparison.Ordinal)),
                $"failure reasons must name the confidently-classified state, got: {string.Join(",", result.FailureReasons)}");
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    /// <summary>
    /// Change (2), site 2 (<c>CaptureIndependentDetailsFramesAsync</c>,
    /// private, reached only via <c>AdvanceToNextPokemonAsync</c> which
    /// first requires polling to an already-observed PokemonDetails
    /// precondition and a swipe-transition observation). Wiring a fake
    /// transport through that full precondition chain is disproportionate
    /// to re-testing a boolean pattern already covered above, so per the
    /// brief's documented fallback this test exercises the decision
    /// expression directly as a minimal harness: given a confidently
    /// MainMenu-classified detection and a non-null topology (the same
    /// collider fixture as above), the site-2 expression
    /// <c>detection.State == PokemonDetails || (allowVisualDetailsFallback
    /// &amp;&amp; detection.State == Unknown &amp;&amp; detailsTopology is
    /// not null)</c> must evaluate false for both possible values of
    /// <c>allowVisualDetailsFallback</c>.
    /// </summary>
    private static void CallSiteDecisionExpressionMinimalHarness()
    {
        var frame = CreateMainMenuColliderFrame(modelAreaTargetScore: 0.12, includeCanonicalClose: true);
        var detection = new PokemonGoGameStateDetector().Detect(frame);
        AssertEqual(PokemonGoGameState.MainMenu, detection.State, "fixture must be a confident MainMenu collider");
        var detailsTopology = new VisualControlLocator().LocateDetailsPageTopology(frame);
        AssertTrue(detailsTopology is not null, "fixture must also satisfy the relaxed Details topology branch");

        foreach (var allowVisualDetailsFallback in new[] { true, false })
        {
            var isDetails = detection.State == PokemonGoGameState.PokemonDetails ||
                (allowVisualDetailsFallback && detection.State == PokemonGoGameState.Unknown && detailsTopology is not null);
            AssertTrue(!isDetails,
                $"confidently-classified MainMenu must never count as Details (allowVisualDetailsFallback={allowVisualDetailsFallback})");
        }
    }

    /// <summary>
    /// File-based sanity check against the 5 real gate-4 evidence frames
    /// (task-I-report.md: X = 0.975 on all 5; m 0.103-0.158, c 0.958-0.998,
    /// p 0.978). With change (1) shipped these must all classify
    /// PokemonDetails. SKIPs (passes with a note) when the directory is
    /// absent so fresh clones without local-data/ stay green.
    /// </summary>
    private static void EvidenceFramesFileBasedSanityCheck()
    {
        var directory = RepositoryPath("local-data", "validation", "reid-pilot-2x50", "run1", "evidence");
        if (!Directory.Exists(directory))
        {
            Console.WriteLine("SKIP  evidence-frames-sanity-check: local-data/ not present in this clone.");
            return;
        }

        var names = new[]
        {
            "0006-canonical-close-pre-",
            "0007-canonical-close-pre-",
            "0008-canonical-close-pre-",
            "0009-canonical-close-pre-",
            "0010-canonical-close-pre-"
        };
        var detector = new PokemonGoGameStateDetector();
        foreach (var prefix in names)
        {
            var match = Directory.GetFiles(directory, prefix + "*.png").SingleOrDefault();
            AssertTrue(match is not null, $"expected exactly one evidence file matching {prefix}*.png");
            var bytes = File.ReadAllBytes(match!);
            var detection = detector.Detect(bytes);
            AssertEqual(PokemonGoGameState.PokemonDetails, detection.State, $"evidence frame {Path.GetFileName(match)} must classify PokemonDetails");
        }
    }

    /// <summary>
    /// A 300x600 synthetic frame shaped like the real warm/sunset-background
    /// Details screens task-I measured: cp band and lower details panel
    /// painted near-maximal (RegionMatch ~1.0, matching the 0.958-0.998
    /// magnitudes measured on the corpus), and a controllable slice of the
    /// modelArea band painted to hit approximately
    /// <paramref name="modelAreaTargetScore"/> (0.0 leaves it fully
    /// degenerate). Its base fill is a dark neutral gray chosen to fail
    /// every earlier, higher-priority <c>PokemonGoGameStateDetector.Detect</c>
    /// branch (MainMenu/Appraisal/Inventory/GameplayMap locators), so a
    /// genuine such frame falls through to the Details check.
    /// </summary>
    private static byte[] CreateWarmBackgroundFrame(double modelAreaTargetScore, bool includeCanonicalClose) =>
        // Dark neutral gray base: fails IsMenuBackground (needs G>=185),
        // IsLight (needs R/G/B>=170), IsDetailsMenuBackground (needs G>=80),
        // IsRed, IsTeal and IsAppraisalOverlay (needs B>=105), and
        // IsDetailsModelArea (R=G=B=60 has spread 0, failing the >=8
        // requirement) -- so a genuine frame like this falls through every
        // earlier Detect() branch to the Details check, and any unpainted
        // portion of the model-area band stays degenerate.
        BuildDetailsShapedFrame(baseGray: 60, modelAreaTargetScore, includeCanonicalClose);

    /// <summary>
    /// Identical Details-page topology (cp/panel/model bands, optional
    /// canonical-close X) as <see cref="CreateWarmBackgroundFrame"/>, but
    /// built on the same light-gray base
    /// <c>WarmBackgroundDetectionStabilityTests.CreateMenuBackgroundFrame</c>
    /// uses (200,200,200), which satisfies all 3 of
    /// <c>LocatePokemonInventory</c>'s <c>IsMenuBackground</c> sample points.
    /// The real, trusted <see cref="PokemonGoGameStateDetector"/> therefore
    /// resolves this frame to MainMenu (checked before the Details branch),
    /// while <see cref="VisualControlLocator.LocateDetailsPageTopology"/>
    /// independently resolves non-null -- the exact shape of a Task-I
    /// "collider", used to test the two Task-J call-site gates.
    /// </summary>
    private static byte[] CreateMainMenuColliderFrame(double modelAreaTargetScore, bool includeCanonicalClose) =>
        BuildDetailsShapedFrame(baseGray: 200, modelAreaTargetScore, includeCanonicalClose);

    private static byte[] BuildDetailsShapedFrame(byte baseGray, double modelAreaTargetScore, bool includeCanonicalClose)
    {
        const int width = 300;
        const int height = 600;
        var rgba = Fill(width, height, r: baseGray, g: baseGray, b: baseGray);

        // cpArea region (0.25,0.07)-(0.75,0.18): light, matches IsDetailsPanel -> ~1.0.
        Paint(rgba, width, height, 0.25, 0.07, 0.75, 0.18, r: 240, g: 240, b: 240);
        // detailsPanel region (0.03,0.39)-(0.97,0.92): light, matches IsDetailsPanelBroad -> ~1.0.
        Paint(rgba, width, height, 0.03, 0.39, 0.97, 0.92, r: 235, g: 240, b: 235);

        // modelArea region (0.22,0.12)-(0.78,0.42): paint a top slice whose
        // height fraction of the 0.30 total region height equals the target
        // RegionMatch score with a spread>=8 color that satisfies
        // IsDetailsModelArea. Painted LAST: its y-range (0.12-~0.16)
        // overlaps the cpArea region's y-range (0.07-0.18), so it must not
        // be overwritten by the cpArea paint above.
        if (modelAreaTargetScore > 0)
        {
            var sliceHeight = 0.30 * modelAreaTargetScore;
            Paint(rgba, width, height, 0.22, 0.12, 0.78, 0.12 + sliceHeight, r: 100, g: 90, b: 70);
        }

        // cx=148, cy=494 are chosen to land exactly on LocateCanonicalCloseControl's
        // candidate scan grid (x step 4 from width*0.32, y step 2 from
        // height*0.74) for this 300x600 canvas, while still sitting in its
        // lower-centre safe zone (x 0.45-0.55, y 0.76-0.91).
        if (includeCanonicalClose)
            DrawCanonicalClose(rgba, width, height, cx: 148, cy: 494, radius: 13);

        return PngEncoder.Encode(new PixelImage(width, height, rgba));
    }

    /// <summary>
    /// Draws a dark-teal circular shell with crossing light-green X strokes,
    /// matching the shapes <c>IsCanonicalShell</c>/<c>IsCanonicalStroke</c>
    /// require, in the lower-centre safe zone (x 0.45-0.55, y 0.76-0.91)
    /// <see cref="VisualControlLocator.LocateCanonicalCloseControl"/> scores.
    /// </summary>
    private static void DrawCanonicalClose(byte[] rgba, int width, int height, int cx, int cy, int radius)
    {
        for (var y = cy - radius - 2; y <= cy + radius + 2; y++)
        for (var x = cx - radius - 2; x <= cx + radius + 2; x++)
        {
            var distance = Math.Sqrt(Math.Pow(x - cx, 2) + Math.Pow(y - cy, 2));
            if (distance is >= 11 and <= 15)
                Set(rgba, width, height, x, y, (0, 165, 175));
        }
        for (var offset = -9; offset <= 9; offset++)
        {
            Set(rgba, width, height, cx + offset, cy + offset, (190, 235, 205));
            Set(rgba, width, height, cx + offset, cy + offset + 1, (190, 235, 205));
            Set(rgba, width, height, cx + offset, cy - offset, (190, 235, 205));
            Set(rgba, width, height, cx + offset, cy - offset + 1, (190, 235, 205));
        }
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

    private static void Set(byte[] rgba, int width, int height, int x, int y, (int R, int G, int B) color)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        var offset = (y * width + x) * 4;
        rgba[offset] = (byte)color.R;
        rgba[offset + 1] = (byte)color.G;
        rgba[offset + 2] = (byte)color.B;
        rgba[offset + 3] = 255;
    }

    private static InventoryAutomationProfile CreateAutomationProfile() => new()
    {
        Name = "canonical-close-corroboration-test",
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
    };

    /// <summary>Always returns the same fixed frame; never expects input to be sent.</summary>
    private sealed class FixedFrameTransport : IAndroidAutomationTransport
    {
        private readonly byte[] _frame;
        private const string Serial = "TEST-SERIAL";

        public FixedFrameTransport(byte[] frame) => _frame = frame;

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
        var path = Path.Combine(Path.GetTempPath(), "pogo-canonical-close-corroboration-selftest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static string RepositoryPath(params string[] parts)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root not found.");
        return parts.Aggregate(directory.FullName, Path.Combine);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {message} to be '{expected}', got '{actual}'.");
    }
}
