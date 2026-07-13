using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Reporting;
using PogoInventory.Calibration.Services;
using PogoInventory.Calibration.Workspace;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Core.Reporting;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Transport;
using PogoInventory.Vision.Errors;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;
using PogoInventory.Vision.Reporting;

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    using var cancellationSource = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellationSource.Cancel();
    };

    Console.CancelKeyPress += cancelHandler;

    try
    {
        if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "analyze" => await AnalyzeAsync(args.Skip(1).ToArray(), cancellationSource.Token),
            "device-snapshot" => await CaptureDeviceSnapshotAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "screen-detect" => await DetectScreenAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "screen-fingerprint" => await ExtractScreenFingerprintAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-init" => await InitializeCalibrationAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-index" => await IndexCalibrationAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-build-profile" => await BuildCalibrationProfileAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-validate" => await ValidateCalibrationAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            _ => UnknownCommand(args[0])
        };
    }
    catch (CalibrationException exception)
    {
        Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
        return CalibrationExitCode(exception.Code);
    }
    catch (DeviceHarnessException exception)
    {
        Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
        return DeviceExitCode(exception.Code);
    }
    catch (ScreenVisionException exception)
    {
        Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
        return VisionExitCode(exception.Code);
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Operation cancelled.");
        return 130;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}

static async Task<int> AnalyzeAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inventoryPath = Require(options, "inventory");
    var policyPath = Require(options, "policy");
    var outputDirectory = Require(options, "out");

    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    var observations = JsonSerializer.Deserialize<List<PokemonObservation>>(
        await File.ReadAllTextAsync(inventoryPath, cancellationToken),
        jsonOptions) ?? throw new InvalidOperationException("Inventory JSON contained no data.");

    var policy = JsonSerializer.Deserialize<RulePolicy>(
        await File.ReadAllTextAsync(policyPath, cancellationToken),
        jsonOptions) ?? throw new InvalidOperationException("Policy JSON contained no data.");

    var analyzer = new InventoryAnalyzer();
    var result = analyzer.Analyze(observations, policy);

    var writer = new DecisionReportWriter();
    await writer.WriteAsync(result, outputDirectory, cancellationToken);

    Console.WriteLine($"Analysed {result.Decisions.Count} Pokémon.");
    Console.WriteLine($"KEEP: {result.KeepCount}");
    Console.WriteLine($"REVIEW: {result.ReviewCount}");
    Console.WriteLine($"DELETE: {result.DeleteCount}");
    Console.WriteLine($"Reports written to: {Path.GetFullPath(outputDirectory)}");
    return 0;
}

static async Task<int> CaptureDeviceSnapshotAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "fake");
    var outputDirectory = Require(options, "out");
    var requestedSerial = Optional(options, "serial");
    var useFake = options.ContainsKey("fake");

    var commandTimeout = TimeSpan.FromSeconds(
        ParsePositiveInt(options, "timeout-seconds", 15));
    var adbPath = Optional(options, "adb") ?? "adb";

    var harnessOptions = new DeviceHarnessOptions
    {
        AdbPath = adbPath,
        CommandTimeout = commandTimeout,
        HarnessVersion = DeviceHarnessOptions.CurrentVersion
    };
    harnessOptions.Validate();

    IDeviceLog log = new ConsoleDeviceLog();
    IAndroidDeviceTransport transport;

    if (useFake)
    {
        transport = FakeAndroidDeviceTransport.CreateSingleAuthorized();
        Console.WriteLine("Using the built-in fake Android device. No phone or ADB command will be accessed.");
    }
    else
    {
        var runner = new AdbProcessRunner(harnessOptions.AdbPath, log);
        transport = new AdbAndroidDeviceTransport(runner, harnessOptions, log);
    }

    var service = new DeviceSnapshotService(
        transport,
        harnessOptions.HarnessVersion,
        log);

    var result = await service.CaptureAsync(
        outputDirectory,
        requestedSerial,
        cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Read-only device snapshot completed.");
    Console.WriteLine($"Device: {result.Device.Model ?? "Unknown model"} ({result.Device.Serial})");
    Console.WriteLine($"Screenshot: {result.ScreenshotPath}");
    Console.WriteLine($"Metadata: {result.MetadataPath}");
    Console.WriteLine($"Manifest: {result.ManifestPath}");
    Console.WriteLine($"Screenshot SHA-256: {result.ScreenshotSha256}");
    return 0;
}

static async Task<int> DetectScreenAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var imagePath = Require(options, "image");
    var profilePath = Require(options, "profile");
    var outputPath = Require(options, "out");

    var png = await File.ReadAllBytesAsync(imagePath, cancellationToken);
    var image = PngDecoder.Decode(png);
    var profile = await ScreenProfileLoader.LoadAsync(profilePath, cancellationToken);
    var result = new ScreenStateDetector().Detect(image, profile);

    await ScreenDetectionReportWriter.WriteAsync(result, outputPath, cancellationToken);

    Console.WriteLine($"State: {result.State}");
    Console.WriteLine($"Confidence: {result.Confidence:F6}");
    Console.WriteLine($"Image: {result.ImageWidth}x{result.ImageHeight} ({result.Orientation})");
    Console.WriteLine($"Evidence report: {Path.GetFullPath(outputPath)}");
    return 0;
}

static async Task<int> ExtractScreenFingerprintAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var imagePath = Require(options, "image");
    var outputPath = Require(options, "out");
    var region = ParseRegion(Require(options, "region"));
    var mode = ParseFingerprintMode(Optional(options, "mode") ?? "grayscale");
    var width = ParsePositiveInt(options, "width", 16);
    var height = ParsePositiveInt(options, "height", 16);

    var png = await File.ReadAllBytesAsync(imagePath, cancellationToken);
    var image = PngDecoder.Decode(png);
    var fingerprint = FingerprintExtractor.Extract(image, region, mode, width, height);

    var result = new ScreenFingerprintResult
    {
        SourceImage = Path.GetFileName(imagePath),
        Region = region,
        Mode = mode,
        FingerprintWidth = width,
        FingerprintHeight = height,
        FingerprintBase64 = Convert.ToBase64String(fingerprint)
    };

    var fullPath = Path.GetFullPath(outputPath);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    await File.WriteAllTextAsync(
        fullPath,
        JsonSerializer.Serialize(
            result,
            ScreenProfileLoader.CreateJsonOptions(writeIndented: true)),
        cancellationToken);

    Console.WriteLine($"Fingerprint written to: {fullPath}");
    return 0;
}

static async Task<int> InitializeCalibrationAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var workspacePath = Require(options, "workspace");
    var workspace = await CalibrationWorkspace.InitializeAsync(
        workspacePath,
        cancellationToken);

    Console.WriteLine("Private calibration workspace initialised.");
    Console.WriteLine($"Root: {workspace.RootPath}");
    Console.WriteLine($"Fixtures: {workspace.FixturesPath}");
    Console.WriteLine($"Manifest: {workspace.ManifestPath}");
    Console.WriteLine($"Anchor plan: {workspace.AnchorPlanPath}");
    Console.WriteLine("Real screenshots remain local and are ignored by Git when the workspace is under local-data/.");
    return 0;
}

static async Task<int> IndexCalibrationAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var workspace = CalibrationWorkspace.Open(Require(options, "workspace"));
    var result = await FixtureIndexer.IndexAsync(workspace, cancellationToken);

    Console.WriteLine($"Indexed fixtures: {result.FixtureCount}");
    Console.WriteLine($"New: {result.NewFixtureCount}");
    Console.WriteLine($"Changed and approval reset: {result.ChangedFixtureCount}");
    Console.WriteLine($"Approvals preserved: {result.PreservedApprovalCount}");
    Console.WriteLine($"Manifest: {result.ManifestPath}");
    return 0;
}

static async Task<int> BuildCalibrationProfileAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "synthetic");
    string manifestPath;
    string anchorPlanPath;
    string fixturesPath;
    string outputPath;

    if (Optional(options, "workspace") is { } workspacePath)
    {
        var workspace = CalibrationWorkspace.Open(workspacePath);
        manifestPath = workspace.ManifestPath;
        anchorPlanPath = workspace.AnchorPlanPath;
        fixturesPath = workspace.FixturesPath;
        outputPath = workspace.ProfilePath;
    }
    else
    {
        if (!options.ContainsKey("synthetic"))
        {
            throw new ArgumentException(
                "Use --workspace for real screenshots. Explicit paths are only allowed with --synthetic.");
        }

        manifestPath = Require(options, "manifest");
        anchorPlanPath = Require(options, "anchors");
        fixturesPath = Require(options, "fixtures");
        outputPath = Require(options, "out");
    }

    var manifest = await FixtureManifestLoader.LoadAsync(manifestPath, cancellationToken);
    var plan = await AnchorPlanLoader.LoadAsync(anchorPlanPath, cancellationToken);
    var profile = await CalibrationProfileBuilder.BuildAsync(
        manifest,
        plan,
        fixturesPath,
        outputPath,
        cancellationToken);

    Console.WriteLine($"Profile built: {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"States: {profile.States.Count}");
    Console.WriteLine($"Anchors: {profile.States.Sum(x => x.Anchors.Count)}");
    Console.WriteLine($"Minimum winner margin: {profile.MinimumWinnerMargin:F6}");
    return 0;
}

static async Task<int> ValidateCalibrationAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "synthetic");
    string manifestPath;
    string fixturesPath;
    string profilePath;
    string outputDirectory;

    if (Optional(options, "workspace") is { } workspacePath)
    {
        var workspace = CalibrationWorkspace.Open(workspacePath);
        manifestPath = workspace.ManifestPath;
        fixturesPath = workspace.FixturesPath;
        profilePath = workspace.ProfilePath;
        outputDirectory = Path.Combine(workspace.ReportsPath, "acceptance");
    }
    else
    {
        if (!options.ContainsKey("synthetic"))
        {
            throw new ArgumentException(
                "Use --workspace for real screenshots. Explicit paths are only allowed with --synthetic.");
        }

        manifestPath = Require(options, "manifest");
        fixturesPath = Require(options, "fixtures");
        profilePath = Require(options, "profile");
        outputDirectory = Require(options, "out");
    }

    var manifest = await FixtureManifestLoader.LoadAsync(manifestPath, cancellationToken);
    var profile = await ScreenProfileLoader.LoadAsync(profilePath, cancellationToken);
    var report = await CalibrationAcceptanceRunner.RunAsync(
        manifest,
        profile,
        fixturesPath,
        cancellationToken);
    await CalibrationReportWriter.WriteAsync(
        report,
        outputDirectory,
        cancellationToken);

    Console.WriteLine($"Calibration result: {(report.Accepted ? "ACCEPTED" : "REJECTED")}");
    Console.WriteLine($"Approved fixtures: {report.ApprovedFixtureCount}");
    Console.WriteLine($"False positives: {report.FalsePositiveCount}");
    Console.WriteLine($"Misclassifications: {report.MisclassificationCount}");
    Console.WriteLine($"False negatives: {report.FalseNegativeCount}");
    Console.WriteLine($"Weak anchors: {report.WeakAnchorCount}");
    Console.WriteLine($"Reports: {Path.GetFullPath(outputDirectory)}");
    return report.Accepted ? 0 : 4;
}

static Dictionary<string, string> ParseOptions(
    string[] args,
    params string[] flagNames)
{
    var flags = new HashSet<string>(flagNames, StringComparer.OrdinalIgnoreCase);
    var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var index = 0; index < args.Length; index++)
    {
        var token = args[index];
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unexpected argument: {token}");
        }

        var key = token[2..];
        if (flags.Contains(key))
        {
            options[key] = "true";
            continue;
        }

        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {token}");
        }

        var value = args[++index];
        if (value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for {token}");
        }

        options[key] = value;
    }

    return options;
}

static int ParsePositiveInt(
    IReadOnlyDictionary<string, string> options,
    string key,
    int defaultValue)
{
    if (!options.TryGetValue(key, out var raw))
    {
        return defaultValue;
    }

    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
        value <= 0)
    {
        throw new ArgumentException($"--{key} must be a positive integer.");
    }

    return value;
}

static NormalizedRegion ParseRegion(string value)
{
    var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 4)
    {
        throw new ArgumentException("--region must contain x,y,width,height using normalised values.");
    }

    var parsed = parts.Select(part =>
    {
        if (!double.TryParse(
                part,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw new ArgumentException($"Invalid normalised region value: {part}");
        }

        return number;
    }).ToArray();

    var region = new NormalizedRegion
    {
        X = parsed[0],
        Y = parsed[1],
        Width = parsed[2],
        Height = parsed[3]
    };
    region.Validate();
    return region;
}

static FingerprintMode ParseFingerprintMode(string value) =>
    Enum.TryParse<FingerprintMode>(value, ignoreCase: true, out var mode)
        ? mode
        : throw new ArgumentException(
            "--mode must be Color, Grayscale or Edge.");

static string Require(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Missing required option --{key}");

static string? Optional(IReadOnlyDictionary<string, string> options, string key) =>
    options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : null;

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 2;
}

static int CalibrationExitCode(CalibrationErrorCode code) =>
    code switch
    {
        CalibrationErrorCode.InvalidWorkspace => 40,
        CalibrationErrorCode.InvalidManifest => 41,
        CalibrationErrorCode.InvalidAnchorPlan => 42,
        CalibrationErrorCode.FixtureMissing => 43,
        CalibrationErrorCode.FixtureHashMismatch => 44,
        CalibrationErrorCode.UnsafePath => 45,
        CalibrationErrorCode.FileSystemFailure => 46,
        _ => 1
    };

static int DeviceExitCode(DeviceErrorCode code) =>
    code switch
    {
        DeviceErrorCode.AdbNotFound => 10,
        DeviceErrorCode.AdbStartFailed => 11,
        DeviceErrorCode.CommandTimedOut => 12,
        DeviceErrorCode.CommandFailed => 13,
        DeviceErrorCode.NoAuthorizedDevice => 14,
        DeviceErrorCode.MultipleAuthorizedDevices => 15,
        DeviceErrorCode.RequestedDeviceNotFound => 16,
        DeviceErrorCode.RequestedDeviceNotAuthorized => 17,
        DeviceErrorCode.InvalidAdbOutput => 18,
        DeviceErrorCode.InvalidScreenshot => 19,
        DeviceErrorCode.IoFailure => 20,
        _ => 1
    };

static int VisionExitCode(VisionErrorCode code) =>
    code switch
    {
        VisionErrorCode.InvalidPng => 30,
        VisionErrorCode.UnsupportedPng => 31,
        VisionErrorCode.InvalidProfile => 32,
        VisionErrorCode.InvalidRegion => 33,
        VisionErrorCode.InvalidFingerprint => 34,
        VisionErrorCode.FileSystemFailure => 35,
        _ => 1
    };

static void PrintHelp()
{
    Console.WriteLine("Pogo Inventory Assistant");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  analyze --inventory <file> --policy <file> --out <directory>");
    Console.WriteLine();
    Console.WriteLine("  device-snapshot --out <directory> [--adb <adb.exe>] [--serial <serial>] [--timeout-seconds <n>]");
    Console.WriteLine("  device-snapshot --fake --out <directory>");
    Console.WriteLine();
    Console.WriteLine("  screen-detect --image <screen.png> --profile <profile.json> --out <evidence.json>");
    Console.WriteLine("  screen-fingerprint --image <screen.png> --region <x,y,w,h> --out <fingerprint.json>");
    Console.WriteLine("                     [--mode <Color|Grayscale|Edge>] [--width <n>] [--height <n>]");
    Console.WriteLine();
    Console.WriteLine("  calibration-init --workspace <private-local-directory>");
    Console.WriteLine("  calibration-index --workspace <private-local-directory>");
    Console.WriteLine("  calibration-build-profile --workspace <private-local-directory>");
    Console.WriteLine("  calibration-validate --workspace <private-local-directory>");
    Console.WriteLine();
    Console.WriteLine("  calibration-build-profile --synthetic --manifest <file> --anchors <file> --fixtures <dir> --out <file>");
    Console.WriteLine("  calibration-validate --synthetic --manifest <file> --fixtures <dir> --profile <file> --out <dir>");
    Console.WriteLine();
    Console.WriteLine("Device snapshot, screen analysis and calibration are read-only. They contain no tap, swipe, text input or game-changing action.");
}
