using System.Text.Json;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;

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
    ("Cancelled snapshot does not write files", CancelledSnapshotDoesNotWriteFilesAsync)
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
