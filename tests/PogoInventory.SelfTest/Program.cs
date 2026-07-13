using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Reporting;
using PogoInventory.Calibration.Services;
using PogoInventory.Calibration.Workspace;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;
using PogoInventory.Vision.Reporting;
using PogoInventory.Vision.Errors;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Perfect is kept", Sync(PerfectIsKept)),
    ("Trade nickname is kept", Sync(TradeNicknameIsKept)),
    ("Old Pokémon is kept", Sync(OldPokemonIsKept)),
    ("Shadow is reviewed", Sync(ShadowIsReviewed)),
    ("Inferior duplicate is deleted", Sync(InferiorDuplicateIsDeleted)),
    ("Preliminary PvP candidate is reviewed", Sync(PvpCandidateIsReviewed)),
    ("Non-exact identity cannot be deleted", Sync(NonExactIdentityCannotBeDeleted)),
    ("ADB device list parser recognises states", Sync(AdbDeviceListParserRecognisesStates)),
    ("ADB metadata parsers read screen and battery", Sync(AdbMetadataParsersReadScreenAndBattery)),
    ("ADB transport uses only expected read-only commands", AdbTransportUsesExpectedCommandsAsync),
    ("Device selection rejects no authorised device", RejectsNoAuthorisedDeviceAsync),
    ("Device selection rejects multiple authorised devices", RejectsMultipleAuthorisedDevicesAsync),
    ("Requested serial selects one authorised device", RequestedSerialSelectsDeviceAsync),
    ("Fake snapshot writes PNG, metadata and manifest", FakeSnapshotWritesFilesAsync),
    ("Metadata serial mismatch is rejected", MetadataSerialMismatchIsRejectedAsync),
    ("Invalid screenshot is rejected", InvalidScreenshotIsRejectedAsync),
    ("Cancelled snapshot does not write files", CancelledSnapshotDoesNotWriteFilesAsync),
    ("PNG decoder reads synthetic fixture", Sync(PngDecoderReadsSyntheticFixture)),
    ("PNG decoder reconstructs Paeth-filtered row", Sync(PngDecoderReconstructsPaethFilteredRow)),
    ("Known screen fixtures classify correctly", KnownScreenFixturesClassifyAsync),
    ("Incomplete screen returns Unknown", IncompleteScreenReturnsUnknownAsync),
    ("Conflicting screen returns Unknown", ConflictingScreenReturnsUnknownAsync),
    ("Landscape screen fails closed", LandscapeScreenFailsClosedAsync),
    ("Confidence thresholds are deterministic", ConfidenceThresholdsAreDeterministicAsync),
    ("Invalid PNG is rejected", Sync(InvalidPngIsRejected)),
    ("Screen evidence report writes JSON", ScreenEvidenceReportWritesJsonAsync),
    ("Calibration workspace creates private structure", CalibrationWorkspaceCreatesPrivateStructureAsync),
    ("Calibration index preserves and resets approvals", CalibrationIndexPreservesAndResetsApprovalsAsync),
    ("Calibration rejects fixture path traversal", Sync(CalibrationRejectsFixturePathTraversal)),
    ("Synthetic calibration profile builds", SyntheticCalibrationProfileBuildsAsync),
    ("Synthetic calibration acceptance passes", SyntheticCalibrationAcceptancePassesAsync),
    ("Calibration false positive fails acceptance", CalibrationFalsePositiveFailsAcceptanceAsync),
    ("Calibration rejects changed approved fixture", CalibrationRejectsChangedApprovedFixtureAsync),
    ("Calibration report writes all formats", CalibrationReportWritesAllFormatsAsync)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static Func<Task> Sync(Action action) =>
    () =>
    {
        action();
        return Task.CompletedTask;
    };

static void PerfectIsKept()
{
    var result = Analyze(
        P("A", "Pikachu", 500, 15, 15, 15));
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void TradeNicknameIsKept()
{
    var result = Analyze(
        P("A", "Bidoof", 100, 1, 1, 1) with { Nickname = "Trade distan" });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void OldPokemonIsKept()
{
    var result = Analyze(
        P("A", "Eevee", 300, 5, 5, 5) with { CatchDate = new DateOnly(2018, 1, 1) });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void ShadowIsReviewed()
{
    var result = Analyze(
        P("A", "Machop", 400, 5, 5, 5) with { IsShadow = true });
    AssertCategory(result, "A", DecisionCategory.Review);
}

static void InferiorDuplicateIsDeleted()
{
    var result = Analyze(
        P("A", "Machop", 900, 14, 14, 14),
        P("B", "Machop", 500, 8, 8, 8) with { SequenceNumber = 2 });
    AssertCategory(result, "A", DecisionCategory.Keep);
    AssertCategory(result, "B", DecisionCategory.Delete);
}

static void PvpCandidateIsReviewed()
{
    var result = Analyze(
        P("A", "Machop", 900, 14, 14, 14),
        P("B", "Machop", 500, 0, 15, 15) with { SequenceNumber = 2 });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static void NonExactIdentityCannotBeDeleted()
{
    var result = Analyze(
        P("A", "Rattata", 900, 14, 14, 14),
        P("B", "Rattata", 500, 8, 8, 8) with
        {
            SequenceNumber = 2,
            IdentityConfidence = IdentityConfidence.HighConfidence
        });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static void AdbDeviceListParserRecognisesStates()
{
    const string output = """
        List of devices attached
        ABC123 device product:foo model:Pixel_9 device:komodo transport_id:1
        DEF456 unauthorized usb:1-2 transport_id:2
        GHI789 offline transport_id:3
        """;

    var devices = AdbOutputParser.ParseDeviceList(output);
    AssertEqual(3, devices.Count, "device count");
    AssertEqual(AndroidDeviceState.Authorized, devices[0].State, "authorised state");
    AssertEqual("Pixel 9", devices[0].Model, "normalised model");
    AssertEqual(AndroidDeviceState.Unauthorized, devices[1].State, "unauthorised state");
    AssertEqual(AndroidDeviceState.Offline, devices[2].State, "offline state");
}

static void AdbMetadataParsersReadScreenAndBattery()
{
    var screen = AdbOutputParser.ParseScreenInfo("Physical size: 1080x2400\nOverride size: 720x1600");
    AssertEqual((int?)1080, screen.PhysicalWidth, "physical width");
    AssertEqual((int?)1600, screen.EffectiveHeight, "effective height");

    const string batteryOutput = """
        Current Battery Service state:
          AC powered: false
          USB powered: true
          Wireless powered: false
          status: 2
          health: 2
          present: true
          level: 85
          scale: 100
          temperature: 295
          technology: Li-ion
        """;

    var battery = AdbOutputParser.ParseBatteryInfo(batteryOutput);
    AssertEqual((int?)85, battery.LevelPercent, "battery percentage");
    AssertEqual((decimal?)29.5m, battery.TemperatureCelsius, "battery temperature");
    AssertEqual("Charging", battery.StatusName, "battery status");
    AssertEqual((bool?)true, battery.UsbPowered, "USB powered");
}

static async Task AdbTransportUsesExpectedCommandsAsync()
{
    static AdbProcessResult Text(string value) =>
        new()
        {
            ExitCode = 0,
            StandardOutput = System.Text.Encoding.UTF8.GetBytes(value),
            StandardError = string.Empty
        };

    var runner = new RecordingAdbProcessRunner(new[]
    {
        Text("List of devices attached\nABC device model:Pixel_9 transport_id:1\n"),
        Text("[ro.product.model]: [Pixel 9]\n[ro.product.manufacturer]: [Google]\n[ro.build.version.release]: [16]\n[ro.build.version.sdk]: [36]\n"),
        Text("Physical size: 1080x2400\n"),
        Text("level: 75\nscale: 100\ntemperature: 300\nstatus: 3\n"),
        new AdbProcessResult
        {
            ExitCode = 0,
            StandardOutput = FakeAndroidDeviceTransport.CreateDefaultScreenshotPng(),
            StandardError = string.Empty
        }
    });

    var options = new DeviceHarnessOptions
    {
        CommandTimeout = TimeSpan.FromSeconds(2)
    };
    var transport = new AdbAndroidDeviceTransport(runner, options);

    var devices = await transport.ListDevicesAsync();
    await transport.ReadMetadataAsync(devices.Single().Serial);
    await transport.CaptureScreenshotPngAsync(devices.Single().Serial);

    var expected = new[]
    {
        "devices -l",
        "-s ABC shell getprop",
        "-s ABC shell wm size",
        "-s ABC shell dumpsys battery",
        "-s ABC exec-out screencap -p"
    };
    var actual = runner.Commands.Select(x => string.Join(" ", x)).ToArray();

    AssertEqual(expected.Length, actual.Length, "ADB command count");
    for (var index = 0; index < expected.Length; index++)
    {
        AssertEqual(expected[index], actual[index], $"ADB command {index + 1}");
    }
}

static async Task RejectsNoAuthorisedDeviceAsync()
{
    var devices = new[]
    {
        FakeAndroidDeviceTransport.CreateDescriptor("A") with
        {
            State = AndroidDeviceState.Unauthorized
        }
    };

    await ExpectDeviceErrorAsync(
        DeviceErrorCode.NoAuthorizedDevice,
        () => RunSnapshotAsync(devices));
}

static async Task RejectsMultipleAuthorisedDevicesAsync()
{
    var devices = new[]
    {
        FakeAndroidDeviceTransport.CreateDescriptor("A"),
        FakeAndroidDeviceTransport.CreateDescriptor("B")
    };

    await ExpectDeviceErrorAsync(
        DeviceErrorCode.MultipleAuthorizedDevices,
        () => RunSnapshotAsync(devices));
}

static Task RequestedSerialSelectsDeviceAsync()
{
    var devices = new[]
    {
        FakeAndroidDeviceTransport.CreateDescriptor("A"),
        FakeAndroidDeviceTransport.CreateDescriptor("B")
    };

    var selected = DeviceSnapshotService.SelectDevice(devices, "B");
    AssertEqual("B", selected.Serial, "selected serial");
    return Task.CompletedTask;
}

static async Task FakeSnapshotWritesFilesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fake = FakeAndroidDeviceTransport.CreateSingleAuthorized();
        var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
        var result = await service.CaptureAsync(directory);

        AssertTrue(File.Exists(result.ScreenshotPath), "screenshot should exist");
        AssertTrue(File.Exists(result.MetadataPath), "metadata should exist");
        AssertTrue(File.Exists(result.ManifestPath), "manifest should exist");

        var screenshot = await File.ReadAllBytesAsync(result.ScreenshotPath);
        AssertTrue(
            screenshot.SequenceEqual(FakeAndroidDeviceTransport.CreateDefaultScreenshotPng()),
            "screenshot bytes should match fake PNG");

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(result.ManifestPath));
        var root = manifest.RootElement;
        AssertEqual("0.2.0", root.GetProperty("harnessVersion").GetString(), "harness version");
        AssertEqual(
            result.ScreenshotSha256,
            root.GetProperty("screenshotSha256").GetString(),
            "screenshot hash");
        AssertEqual(
            "FAKE-001",
            root.GetProperty("device").GetProperty("serial").GetString(),
            "manifest serial");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task MetadataSerialMismatchIsRejectedAsync()
{
    var device = FakeAndroidDeviceTransport.CreateDescriptor("A");
    var fake = new FakeAndroidDeviceTransport(
        new[] { device },
        new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
        {
            ["A"] = FakeAndroidDeviceTransport.CreateMetadata("WRONG")
        },
        FakeAndroidDeviceTransport.CreateDefaultScreenshotPng());
    var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
    var directory = CreateTemporaryDirectory();

    try
    {
        await ExpectDeviceErrorAsync(
            DeviceErrorCode.InvalidAdbOutput,
            () => service.CaptureAsync(directory));
        AssertEqual(0, Directory.GetFiles(directory).Length, "serial mismatch output file count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task InvalidScreenshotIsRejectedAsync()
{
    var device = FakeAndroidDeviceTransport.CreateDescriptor("A");
    var fake = new FakeAndroidDeviceTransport(
        new[] { device },
        new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
        {
            ["A"] = FakeAndroidDeviceTransport.CreateMetadata("A")
        },
        new byte[] { 1, 2, 3, 4 });
    var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
    var directory = CreateTemporaryDirectory();

    try
    {
        await ExpectDeviceErrorAsync(
            DeviceErrorCode.InvalidScreenshot,
            () => service.CaptureAsync(directory));
        AssertEqual(0, Directory.GetFiles(directory).Length, "invalid screenshot output file count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CancelledSnapshotDoesNotWriteFilesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var fake = FakeAndroidDeviceTransport.CreateSingleAuthorized();
        var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);

        try
        {
            await service.CaptureAsync(directory, cancellationToken: cancellationSource.Token);
            throw new InvalidOperationException("Expected OperationCanceledException.");
        }
        catch (OperationCanceledException)
        {
        }

        AssertEqual(0, Directory.GetFiles(directory).Length, "cancelled output file count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static void PngDecoderReadsSyntheticFixture()
{
    var image = LoadFixture("InventoryList.png");
    AssertEqual(180, image.Width, "fixture width");
    AssertEqual(360, image.Height, "fixture height");

    var region = new NormalizedRegion
    {
        X = 0.05,
        Y = 0.70,
        Width = 0.25,
        Height = 0.20
    };
    var fingerprint = FingerprintExtractor.Extract(
        image,
        region,
        FingerprintMode.Color,
        8,
        8);
    AssertEqual(8 * 8 * 3, fingerprint.Length, "color fingerprint length");
    AssertEqual(1d, FingerprintComparer.Similarity(fingerprint, fingerprint), "self similarity");
}

static void PngDecoderReconstructsPaethFilteredRow()
{
    var image = LoadFixture("PaethFilterRgba.png");
    AssertEqual(3, image.Width, "Paeth fixture width");
    AssertEqual(2, image.Height, "Paeth fixture height");
    AssertEqual(new Rgba32(10, 20, 30, 255), image.GetPixel(0, 0), "row 0 pixel 0");
    AssertEqual(new Rgba32(70, 80, 90, 255), image.GetPixel(2, 0), "row 0 pixel 2");
    AssertEqual(new Rgba32(15, 25, 35, 255), image.GetPixel(0, 1), "Paeth row pixel 0");
    AssertEqual(new Rgba32(45, 55, 65, 255), image.GetPixel(1, 1), "Paeth row pixel 1");
    AssertEqual(new Rgba32(75, 85, 95, 255), image.GetPixel(2, 1), "Paeth row pixel 2");
}

static async Task KnownScreenFixturesClassifyAsync()
{
    var profile = await LoadSyntheticProfileAsync();
    var detector = new ScreenStateDetector();
    var fixtures = new Dictionary<string, ScreenState>(StringComparer.Ordinal)
    {
        ["InventoryList.png"] = ScreenState.InventoryList,
        ["PokemonDetails.png"] = ScreenState.PokemonDetails,
        ["AppraisalOpen.png"] = ScreenState.AppraisalOpen,
        ["PokemonMenuOpen.png"] = ScreenState.PokemonMenuOpen,
        ["TagDialogOpen.png"] = ScreenState.TagDialogOpen,
        ["SearchOpen.png"] = ScreenState.SearchOpen,
        ["Loading.png"] = ScreenState.Loading,
        ["Popup.png"] = ScreenState.Popup,
        ["NetworkError.png"] = ScreenState.NetworkError
    };

    foreach (var fixture in fixtures)
    {
        var result = detector.Detect(
            LoadFixture(fixture.Key),
            profile,
            new DateTimeOffset(2026, 7, 13, 21, 0, 0, TimeSpan.Zero));
        AssertEqual(fixture.Value, result.State, $"state for {fixture.Key}");
        AssertTrue(result.Confidence >= 0.99, $"confidence for {fixture.Key}");
    }
}

static async Task IncompleteScreenReturnsUnknownAsync()
{
    var result = new ScreenStateDetector().Detect(
        LoadFixture("Incomplete.png"),
        await LoadSyntheticProfileAsync());

    AssertEqual(ScreenState.Unknown, result.State, "incomplete screen state");
    AssertTrue(
        result.Reasons.Contains("NoStatePassedRequiredAnchorsAndMinimumScore"),
        "incomplete screen reason");
}

static async Task ConflictingScreenReturnsUnknownAsync()
{
    var result = new ScreenStateDetector().Detect(
        LoadFixture("Conflict.png"),
        await LoadSyntheticProfileAsync());

    AssertEqual(ScreenState.Unknown, result.State, "conflicting screen state");
    AssertTrue(
        result.Reasons.Contains("ConflictingStateEvidence"),
        "conflicting screen reason");
    AssertTrue(
        result.States.Count(x => x.Eligible && x.Score >= 0.96) >= 2,
        "conflicting eligible state count");
}

static async Task LandscapeScreenFailsClosedAsync()
{
    var result = new ScreenStateDetector().Detect(
        LoadFixture("Landscape.png"),
        await LoadSyntheticProfileAsync());

    AssertEqual(ScreenState.Unknown, result.State, "landscape screen state");
    AssertEqual(ScreenOrientation.Landscape, result.Orientation, "landscape orientation");
    AssertTrue(
        result.Reasons.Any(x => x.StartsWith("UnsupportedOrientation:", StringComparison.Ordinal)),
        "landscape reason");
    AssertEqual(0, result.States.Count, "landscape evidence state count");
}

static async Task ConfidenceThresholdsAreDeterministicAsync()
{
    var profile = await LoadSyntheticProfileAsync();
    var image = LoadFixture("InventoryListNoisy.png");
    var detector = new ScreenStateDetector();

    var first = detector.Detect(image, profile);
    var second = detector.Detect(image, profile);

    AssertEqual(ScreenState.InventoryList, first.State, "noisy fixture state");
    AssertEqual(first.State, second.State, "repeated state");
    AssertEqual(first.Confidence, second.Confidence, "repeated confidence");
    AssertTrue(first.Confidence < 1d, "noisy confidence should be below one");
    AssertTrue(first.Confidence > profile.MinimumStateScore, "noisy confidence should pass base threshold");

    var stricter = profile with
    {
        MinimumStateScore = first.Confidence + 0.000001
    };
    var rejected = detector.Detect(image, stricter);
    AssertEqual(ScreenState.Unknown, rejected.State, "strict threshold state");
    AssertTrue(
        rejected.Reasons.Contains("NoStatePassedRequiredAnchorsAndMinimumScore"),
        "strict threshold reason");
}


static void InvalidPngIsRejected()
{
    try
    {
        _ = PngDecoder.Decode(new byte[] { 1, 2, 3, 4 });
        throw new InvalidOperationException("Expected invalid PNG rejection.");
    }
    catch (ScreenVisionException exception) when (exception.Code == VisionErrorCode.InvalidPng)
    {
    }
}

static async Task ScreenEvidenceReportWritesJsonAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var result = new ScreenStateDetector().Detect(
            LoadFixture("InventoryList.png"),
            await LoadSyntheticProfileAsync(),
            new DateTimeOffset(2026, 7, 13, 21, 30, 0, TimeSpan.Zero));
        var path = Path.Combine(directory, "evidence.json");

        await ScreenDetectionReportWriter.WriteAsync(result, path);

        AssertTrue(File.Exists(path), "screen evidence report should exist");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        AssertEqual("InventoryList", json.RootElement.GetProperty("state").GetString(), "reported state");
        AssertTrue(
            json.RootElement.GetProperty("states").GetArrayLength() >= 9,
            "reported state evidence count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationWorkspaceCreatesPrivateStructureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(
            Path.Combine(directory, "calibration"));

        AssertTrue(File.Exists(workspace.MarkerPath), "workspace marker should exist");
        AssertTrue(File.Exists(workspace.ManifestPath), "fixture manifest should exist");
        AssertTrue(File.Exists(workspace.AnchorPlanPath), "anchor plan should exist");
        AssertTrue(Directory.Exists(workspace.FixturesPath), "fixtures directory should exist");
        AssertTrue(
            Directory.Exists(Path.Combine(workspace.FixturesPath, "Unknown")),
            "Unknown negative-fixture directory should exist");

        var reopened = CalibrationWorkspace.Open(workspace.RootPath);
        AssertEqual(workspace.RootPath, reopened.RootPath, "reopened workspace path");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationIndexPreservesAndResetsApprovalsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(
            Path.Combine(directory, "calibration"));
        var target = Path.Combine(workspace.FixturesPath, "InventoryList", "one.png");
        File.Copy(RepositoryPath("data", "screen-fixtures", "InventoryList.png"), target);

        var first = await FixtureIndexer.IndexAsync(workspace);
        AssertEqual(1, first.NewFixtureCount, "new fixture count");

        var manifest = await FixtureManifestLoader.LoadAsync(workspace.ManifestPath);
        var approvedFixture = manifest.Fixtures.Single() with
        {
            SafetyReview = CompleteSafetyReview()
        };
        manifest = manifest with { Fixtures = new[] { approvedFixture } };
        await File.WriteAllTextAsync(
            workspace.ManifestPath,
            JsonSerializer.Serialize(
                manifest,
                CalibrationJson.CreateOptions(writeIndented: true)));

        var second = await FixtureIndexer.IndexAsync(workspace);
        AssertEqual(1, second.PreservedApprovalCount, "preserved approval count");

        File.Copy(
            RepositoryPath("data", "screen-fixtures", "InventoryListNoisy.png"),
            target,
            overwrite: true);
        var third = await FixtureIndexer.IndexAsync(workspace);
        AssertEqual(1, third.ChangedFixtureCount, "changed fixture count");

        var changed = await FixtureManifestLoader.LoadAsync(workspace.ManifestPath);
        AssertTrue(
            !changed.Fixtures.Single().SafetyReview.ApprovedForCalibration,
            "changed fixture approval should be reset");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static void CalibrationRejectsFixturePathTraversal()
{
    var manifest = new ScreenFixtureManifest
    {
        Name = "Unsafe",
        ProfileName = "Unsafe",
        Fixtures = new[]
        {
            new ScreenFixtureDefinition
            {
                Id = "escape",
                RelativePath = "../escape.png",
                ExpectedState = ScreenState.Unknown,
                Sha256 = new string('0', 64),
                SafetyReview = CompleteSafetyReview()
            }
        }
    };

    try
    {
        FixtureManifestLoader.Validate(manifest);
        throw new InvalidOperationException("Expected unsafe calibration path rejection.");
    }
    catch (CalibrationException exception) when (exception.Code == CalibrationErrorCode.UnsafePath)
    {
    }
}

static async Task SyntheticCalibrationProfileBuildsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var output = Path.Combine(directory, "profile.json");
        var profile = await BuildSyntheticCalibrationProfileAsync(output);

        AssertTrue(File.Exists(output), "generated profile should exist");
        AssertEqual(9, profile.States.Count, "generated state count");
        AssertEqual(18, profile.States.Sum(x => x.Anchors.Count), "generated anchor count");
        AssertEqual(
            "Synthetic generated calibration profile",
            profile.Name,
            "generated profile name");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SyntheticCalibrationAcceptancePassesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await BuildSyntheticCalibrationProfileAsync(
            Path.Combine(directory, "profile.json"));
        var manifest = await LoadSyntheticCalibrationManifestAsync();
        var report = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            RepositoryPath("data", "screen-fixtures"));

        AssertTrue(report.Accepted, "synthetic calibration should be accepted");
        AssertEqual(14, report.ApprovedFixtureCount, "approved synthetic fixture count");
        AssertEqual(0, report.FalsePositiveCount, "synthetic false positives");
        AssertEqual(0, report.MisclassificationCount, "synthetic misclassifications");
        AssertEqual(0, report.FalseNegativeCount, "synthetic false negatives");
        AssertEqual(0, report.WeakAnchorCount, "synthetic weak anchors");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationFalsePositiveFailsAcceptanceAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await BuildSyntheticCalibrationProfileAsync(
            Path.Combine(directory, "profile.json"));
        var source = await LoadSyntheticCalibrationManifestAsync();
        var inventory = source.Fixtures.Single(x => x.Id == "inventory-list") with
        {
            ExpectedState = ScreenState.Unknown
        };
        var manifest = source with
        {
            Acceptance = source.Acceptance with
            {
                MaximumWeakAnchors = 100,
                States = new[]
                {
                    new StateAcceptanceRequirement
                    {
                        State = ScreenState.Unknown,
                        MinimumApprovedFixtures = 1,
                        MinimumRecall = 1.0
                    }
                }
            },
            Fixtures = new[] { inventory }
        };

        var report = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            RepositoryPath("data", "screen-fixtures"));

        AssertTrue(!report.Accepted, "false-positive calibration should be rejected");
        AssertEqual(1, report.FalsePositiveCount, "false-positive count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationRejectsChangedApprovedFixtureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var target = Path.Combine(directory, "screen.png");
        var original = RepositoryPath("data", "screen-fixtures", "InventoryList.png");
        File.Copy(original, target);
        var hash = await CalibrationHash.Sha256Async(target);
        var manifest = new ScreenFixtureManifest
        {
            Name = "Hash test",
            ProfileName = "Hash test",
            Fixtures = new[]
            {
                new ScreenFixtureDefinition
                {
                    Id = "screen",
                    RelativePath = "screen.png",
                    ExpectedState = ScreenState.InventoryList,
                    Sha256 = hash,
                    SafetyReview = CompleteSafetyReview()
                }
            }
        };

        File.Copy(
            RepositoryPath("data", "screen-fixtures", "PokemonDetails.png"),
            target,
            overwrite: true);
        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.FixtureHashMismatch,
            () => FixtureRepository.LoadApprovedAsync(manifest, directory));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationReportWritesAllFormatsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await BuildSyntheticCalibrationProfileAsync(
            Path.Combine(directory, "profile.json"));
        var manifest = await LoadSyntheticCalibrationManifestAsync();
        var report = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            RepositoryPath("data", "screen-fixtures"));
        var output = Path.Combine(directory, "reports");

        await CalibrationReportWriter.WriteAsync(report, output);

        AssertTrue(
            File.Exists(Path.Combine(output, "calibration-acceptance.json")),
            "calibration JSON report should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "calibration-acceptance.md")),
            "calibration Markdown report should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "confusion-matrix.csv")),
            "confusion matrix should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "fixture-results.csv")),
            "fixture results should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static FixtureSafetyReview CompleteSafetyReview() =>
    new()
    {
        AccountIdentitySafe = true,
        LocationSafe = true,
        NotificationsSafe = true,
        OtherPersonalDataSafe = true,
        ApprovedForCalibration = true,
        ReviewedBy = "Self-test",
        ReviewedAtUtc = new DateTimeOffset(2026, 7, 13, 20, 30, 0, TimeSpan.Zero)
    };

static async Task<ScreenDetectionProfile> BuildSyntheticCalibrationProfileAsync(
    string outputPath)
{
    var manifest = await LoadSyntheticCalibrationManifestAsync();
    var plan = await AnchorPlanLoader.LoadAsync(
        RepositoryPath("data", "calibration", "anchor-plan.synthetic.json"));
    return await CalibrationProfileBuilder.BuildAsync(
        manifest,
        plan,
        RepositoryPath("data", "screen-fixtures"),
        outputPath);
}

static Task<ScreenFixtureManifest> LoadSyntheticCalibrationManifestAsync() =>
    FixtureManifestLoader.LoadAsync(
        RepositoryPath("data", "calibration", "fixture-manifest.synthetic.json"));

static async Task ExpectCalibrationErrorAsync(
    CalibrationErrorCode expectedCode,
    Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected CalibrationException {expectedCode}.");
    }
    catch (CalibrationException exception) when (exception.Code == expectedCode)
    {
    }
}

static PixelImage LoadFixture(string name)
{
    var path = RepositoryPath("data", "screen-fixtures", name);
    return PngDecoder.Decode(File.ReadAllBytes(path));
}

static Task<ScreenDetectionProfile> LoadSyntheticProfileAsync() =>
    ScreenProfileLoader.LoadAsync(
        RepositoryPath("data", "screen-profile.synthetic.json"));

static string RepositoryPath(params string[] parts)
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null &&
           !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
    {
        directory = directory.Parent;
    }

    if (directory is null)
    {
        throw new InvalidOperationException("Repository root could not be located.");
    }

    return parts.Aggregate(directory.FullName, Path.Combine);
}

static async Task RunSnapshotAsync(IReadOnlyList<AndroidDeviceDescriptor> devices)
{
    var metadata = devices.ToDictionary(
        x => x.Serial,
        x => FakeAndroidDeviceTransport.CreateMetadata(x.Serial),
        StringComparer.Ordinal);
    var fake = new FakeAndroidDeviceTransport(
        devices,
        metadata,
        FakeAndroidDeviceTransport.CreateDefaultScreenshotPng());
    var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
    var directory = CreateTemporaryDirectory();

    try
    {
        await service.CaptureAsync(directory);
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ExpectDeviceErrorAsync(
    DeviceErrorCode expectedCode,
    Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected DeviceHarnessException {expectedCode}.");
    }
    catch (DeviceHarnessException exception) when (exception.Code == expectedCode)
    {
    }
}

static InventoryAnalysisResult Analyze(params PokemonObservation[] observations) =>
    new InventoryAnalyzer().Analyze(observations, new RulePolicy());

static PokemonObservation P(
    string key,
    string species,
    int cp,
    int attack,
    int defense,
    int hp) =>
    new()
    {
        ExternalKey = key,
        SequenceNumber = 1,
        Species = species,
        Cp = cp,
        AttackIv = attack,
        DefenseIv = defense,
        HpIv = hp,
        CatchDate = new DateOnly(2026, 7, 1),
        IsShiny = false,
        IsMythical = false,
        IsBackground = false,
        IsFavorite = false,
        IsLegendary = false,
        IsUltraBeast = false,
        IsShadow = false,
        IsPurified = false,
        IsLucky = false,
        IsCostume = false,
        IsDynamax = false,
        IsGigantamax = false,
        HasSpecialMove = false,
        IsXxl = false,
        IsXxs = false,
        IdentityConfidence = IdentityConfidence.Exact
    };

static void AssertCategory(
    InventoryAnalysisResult result,
    string externalKey,
    DecisionCategory expected)
{
    var actual = result.Decisions.Single(x => x.ExternalKey == externalKey).Category;
    AssertEqual(expected, actual, $"decision for {externalKey}");
}

static void AssertEqual<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"Expected {description} to be '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string CreateTemporaryDirectory()
{
    var path = Path.Combine(
        Path.GetTempPath(),
        "PogoInventoryAssistant",
        Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void DeleteDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}
