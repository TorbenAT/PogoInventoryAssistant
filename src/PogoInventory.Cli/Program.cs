using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Application;
using PogoInventory.Persistence;
using PogoInventory.Automation.Errors;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Automation.Transport;
using PogoInventory.Bootstrap.Services;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Reporting;
using PogoInventory.Calibration.Services;
using PogoInventory.Calibration.Workspace;
using PogoInventory.CalcyProbe.Models;
using PogoInventory.CalcyProbe.Services;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Core.Reporting;
using PogoInventory.CropAtlas.Models;
using PogoInventory.CropAtlas.Services;
using PogoInventory.CropAtlas.Semantic.Models;
using PogoInventory.CropAtlas.Semantic.Services;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.ImagePretest.Models;
using PogoInventory.ImagePretest.Services;
using PogoInventory.Exploration.Services;
using PogoInventory.Exploration.Models;
using PogoInventory.Observations.Models;
using PogoInventory.Observations.Parsing;
using PogoInventory.Observations.Providers;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.RegionDiscovery.Services;
using PogoInventory.Vision.Errors;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;
using PogoInventory.Vision.Reporting;
using PogoInventory.Verification.Models;
using PogoInventory.Verification.Services;

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
            "inventory-scan" => await RunInventoryScanAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "real-scan-export" => await ExportRealScanAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "profile-bootstrap" => await RunProfileBootstrapAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calcy-probe" => await RunCalcyProbeAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calcy-live-check" => await RunCalcyLiveCheckAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calcy-parse" => await ParseCalcyOutputAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calcy-verification-init" => await InitializeCalcyVerificationAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calcy-verification-run" => await RunCalcyVerificationAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calcy-provider-select" => await SelectCalcyProviderAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "image-pretest" => await RunImagePretestAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "image-region-discovery" => await RunImageRegionDiscoveryAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "image-crop-atlas" => await RunImageCropAtlasAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "image-semantic-evidence" => await RunImageSemanticEvidenceAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "appraisal-pretest" => await RunAppraisalPretestAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "phone-prepare" => await PreparePhoneAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-stop-known-app" => await StopKnownAppAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-open-inventory" => await OpenInventoryAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-press-back" => await PressBackAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-close-inventory" => await CloseInventoryAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-continue-appraisal-intro" => await ContinueAppraisalIntroAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-detect-game-state" => await DetectGameStateAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "game-state-detect-image" => await DetectGameStateImageAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-recover-inventory" => await RecoverInventoryAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-open-main-menu" => await OpenPokemonGoMainMenuAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-open-pokemon-inventory" => await OpenPokemonInventoryFromMainMenuAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-search-inventory" => await SearchInventoryAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-run-index-sequence" => await RunIndexSequenceAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-run-cleanup-proof" => await RunCleanupProofAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-validate-navigation-safety" => await ValidateNavigationSafetyAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-set-pokemon-tag" => await SetPokemonTagAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "tag-selector-detect-image" => await DetectTagSelectorImageAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-open-appraisal" => await OpenAppraisalFromInventoryAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-snapshot" => await CaptureDeviceSnapshotAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-ui-snapshot" => await CaptureDeviceUiSnapshotAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "device-launch-pokemon-go" => await LaunchPokemonGoAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "inventory-db-init" => await InitializeInventoryDbAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "inventory-db-summary" => await SummarizeInventoryDbAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "screen-detect" => await DetectScreenAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "screen-fingerprint" => await ExtractScreenFingerprintAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "identity-fingerprint" => await ExtractPokemonIdentityFingerprintAsync(
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
            "calibration-capture" => await CaptureCalibrationScreenAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-capture-session" => await RunCalibrationCaptureSessionAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-capture-status" => await ShowCalibrationCaptureStatusAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            "calibration-capture-approve" => await ApproveCalibrationCaptureAsync(
                args.Skip(1).ToArray(),
                cancellationSource.Token),
            _ => UnknownCommand(args[0])
        };
    }
    catch (AutomationException exception)
    {
        Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
        return AutomationExitCode(exception.Code);
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

static async Task<int> InitializeInventoryDbAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var path = Optional(options, "db") ?? Path.Combine("local-data", "inventory", "pogo-inventory.db");
    await new InventoryPersistenceService(path).InitializeAsync(cancellationToken);
    Console.WriteLine($"Inventory database initialized: {Path.GetFullPath(path)}");
    return 0;
}

static async Task<int> SummarizeInventoryDbAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var path = Optional(options, "db") ?? Path.Combine("local-data", "inventory", "pogo-inventory.db");
    var service = new InventoryPersistenceService(path);
    var count = await service.CountObservationsAsync(cancellationToken);
    Console.WriteLine($"Database: {Path.GetFullPath(path)}");
    Console.WriteLine($"Observations: {count}");
    return 0;
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

static async Task<int> RunInventoryScanAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "fake");
    var outputDirectory = Require(options, "out");
    var automationProfile = await AutomationProfileLoader.LoadAsync(
        Require(options, "profile"),
        cancellationToken);
    var screenProfile = await ScreenProfileLoader.LoadAsync(
        Require(options, "screen-profile"),
        cancellationToken);
    var maximumItems = ParsePositiveInt(
        options,
        "max-items",
        automationProfile.DefaultMaximumItems);
    var maximumRuntimeMinutes = ParseOptionalPositiveInt(
        options,
        "max-runtime-minutes");
    var appraisalProfilePath = Optional(options, "appraisal-profile");

    IAndroidAutomationTransport transport;
    var useFake = options.ContainsKey("fake");
    if (useFake)
    {
        var fixtures = Optional(options, "fixtures") ??
            Path.Combine("data", "screen-fixtures");
        transport = await CreateScriptedTransportAsync(fixtures, cancellationToken);
        Console.WriteLine("Using the deterministic scripted Android transport.");
    }
    else
    {
        transport = CreateRealAndroidTransport(options);
        var inspectionTransport = transport as IAndroidAppInspectionTransport ??
            throw new InvalidOperationException(
                "The configured Android transport does not support known-app control.");
        var devices = await inspectionTransport.ListDevicesAsync(cancellationToken);
        var selectedDevice = DeviceSnapshotService.SelectDevice(
            devices,
            Optional(options, "serial"));
        await inspectionTransport.StopKnownAppAsync(
            selectedDevice.Serial,
            KnownAndroidPackage.Calcy,
            cancellationToken);
        Console.WriteLine("Stopped the allow-listed Calcy package before the scan.");
    }

    var providerMode = (Optional(options, "observation-provider") ??
        (appraisalProfilePath is not null ? "appraisal" : useFake ? "fake" : "none"))
        .ToLowerInvariant();
    if (!useFake && providerMode == "fake")
    {
        throw new ArgumentException(
            "The fake observation provider can only be used with the fake Android transport.");
    }

    var appraisalProfile = providerMode == "appraisal"
        ? await AppraisalProfileLoader.LoadAsync(
            appraisalProfilePath ??
                throw new ArgumentException(
                    "The appraisal observation provider requires --appraisal-profile."),
            cancellationToken)
        : null;

    IPokemonObservationProvider observationProvider = providerMode switch
    {
        "fake" => new FakeCalcyObservationProvider(),
        "none" => new UnavailableCalcyObservationProvider(),
        "appraisal" => new AppraisalProfileObservationProvider(appraisalProfile!),
        _ => throw new ArgumentException(
            "Observation provider must be either 'fake', 'none' or 'appraisal'.")
    };

    var runner = new InventoryAutomationRunner(
        transport,
        new ScreenStateDetector(),
        new ConsoleDeviceLog(),
        observationProvider,
        appraisalProfile);
    var result = await runner.RunAsync(
        outputDirectory,
        automationProfile,
        screenProfile,
        Optional(options, "serial"),
        maximumItems,
        cancellationToken,
        maximumRuntimeMinutes is null
            ? null
            : TimeSpan.FromMinutes(maximumRuntimeMinutes.Value));

    Console.WriteLine();
    Console.WriteLine("Automatic inventory navigation finished.");
    Console.WriteLine($"Status: {result.Checkpoint.Status}");
    Console.WriteLine($"Stop reason: {result.Checkpoint.StopReason}");
    Console.WriteLine($"Items captured: {result.Checkpoint.Items.Count}");
    foreach (var group in result.Checkpoint.Items
                 .GroupBy(item => item.Observation.Status)
                 .OrderBy(group => group.Key))
    {
        Console.WriteLine($"Observations {group.Key}: {group.Count()}");
    }
    Console.WriteLine($"Checkpoint schema: {result.Checkpoint.SchemaVersion}");
    Console.WriteLine($"Checkpoint: {result.CheckpointPath}");
    Console.WriteLine($"Captures: {result.CaptureDirectory}");
    if (!string.IsNullOrWhiteSpace(result.Checkpoint.StopDetail))
    {
        Console.WriteLine($"Detail: {result.Checkpoint.StopDetail}");
    }

    return result.Checkpoint.Status == PogoInventory.Automation.Models.AutomationRunStatus.Completed
        ? 0
        : 5;
}

static async Task<int> ExportRealScanAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var minimumItems = ParseOptionalPositiveInt(options, "minimum-items");
    var expectedItems = ParseOptionalPositiveInt(options, "expected-items");
    if (minimumItems is null && expectedItems is null)
    {
        expectedItems = 20;
    }
    var generateOverlays = ParseBoolean(
        options,
        "generate-overlays",
        minimumItems is null);
    var result = await RealScanEvidenceExporter.ExportAsync(
        Require(options, "checkpoint"),
        Require(options, "appraisal-profile"),
        Require(options, "out"),
        Require(options, "calibration-out"),
        new RealScanExportOptions
        {
            ExpectedItems = expectedItems,
            MinimumItems = minimumItems,
            RequestedMaximumItems = ParseOptionalPositiveInt(options, "requested-maximum-items"),
            GenerateOverlays = generateOverlays,
            CopyScreenshots = ParseBoolean(options, "copy-screenshots", minimumItems is null),
            GenerateCheckpointEvidence = ParseBoolean(
                options,
                "generate-checkpoint-evidence",
                minimumItems is null)
        },
        cancellationToken);

    Console.WriteLine($"Real phone demo: {(result.Manifest.RealPhoneDemoPassed ? "PASS" : "FAIL")}");
    var requestedCount = result.Manifest.RequestedMaximumItems?.ToString(
        CultureInfo.InvariantCulture) ?? "unbounded";
    Console.WriteLine($"Scanned: {result.Manifest.Scanned}/{requestedCount}");
    Console.WriteLine($"Unique changed frames: {result.Manifest.UniqueChangedFrames}/{result.Manifest.Scanned}");
    Console.WriteLine($"Swipes: {result.Manifest.SwipesSucceeded}/{Math.Max(0, result.Manifest.Scanned - 1)}");
    Console.WriteLine($"Calibration: {result.CalibrationCases}/3; stable={result.CalibrationStable}");
    Console.WriteLine($"Decisions: KEEP {result.Manifest.Keep}, REVIEW {result.Manifest.Review}, DELETE {result.Manifest.Delete}");
    Console.WriteLine($"Report: {result.ReportPath}");
    return result.Manifest.RealPhoneDemoPassed ? 0 : 1;
}

static async Task<int> RunProfileBootstrapAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "fake");
    var outputDirectory = Require(options, "out");
    var automationProfile = await AutomationProfileLoader.LoadAsync(
        Require(options, "profile"),
        cancellationToken);
    var anchorPlan = await AnchorPlanLoader.LoadAsync(
        Require(options, "anchors"),
        cancellationToken);

    IAndroidAutomationTransport transport;
    if (options.ContainsKey("fake"))
    {
        var fixtures = Optional(options, "fixtures") ??
            Path.Combine("data", "screen-fixtures");
        transport = await CreateScriptedTransportAsync(fixtures, cancellationToken);
        Console.WriteLine("Using the deterministic scripted Android transport.");
    }
    else
    {
        transport = CreateRealAndroidTransport(options);
    }

    var result = await new CoreProfileBootstrapRunner(transport).RunAsync(
        outputDirectory,
        automationProfile,
        anchorPlan,
        Optional(options, "serial"),
        cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Automatic core profile bootstrap finished.");
    Console.WriteLine($"Accepted: {result.Acceptance.Accepted}");
    Console.WriteLine($"Captures: {result.CapturedFiles.Count}");
    Console.WriteLine($"False positives: {result.Acceptance.FalsePositiveCount}");
    Console.WriteLine($"Misclassifications: {result.Acceptance.MisclassificationCount}");
    Console.WriteLine($"Profile: {result.ProfilePath}");
    Console.WriteLine($"Acceptance report: {result.AcceptanceDirectory}");
    return result.Acceptance.Accepted ? 0 : 6;
}

static async Task<int> RunCalcyProbeAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "fake");
    var outputDirectory = Require(options, "out");
    var packageName = Optional(options, "package") ?? CalcyProbeOptions.DefaultPackageName;
    var maximumLogcatLines = ParsePositiveInt(options, "max-log-lines", 4000);

    IAndroidAppInspectionTransport transport;
    if (options.ContainsKey("fake"))
    {
        var fixtures = Optional(options, "fixtures") ?? Path.Combine("data", "calcy-probe");
        transport = CreateScriptedCalcyProbeTransport(fixtures);
        Console.WriteLine("Using scripted Calcy inspection evidence. No phone is accessed.");
    }
    else
    {
        transport = CreateRealAndroidAppInspectionTransport(options);
    }

    var result = await new CalcyProbeRunner(transport, new ConsoleDeviceLog()).RunAsync(
        outputDirectory,
        new CalcyProbeOptions
        {
            PackageName = packageName,
            MaximumLogcatLines = maximumLogcatLines
        },
        Optional(options, "serial"),
        cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Calcy inspection probe finished.");
    Console.WriteLine($"Decision: {result.Report.Decision}");
    Console.WriteLine($"Installed: {result.Report.Package.IsInstalled}");
    Console.WriteLine($"Version: {result.Report.Package.VersionName ?? "Unknown"}");
    Console.WriteLine($"Process id: {result.Report.ProcessId ?? "Not running"}");
    Console.WriteLine($"Filtered log lines: {result.Report.FilteredLogLineCount}");
    Console.WriteLine($"Report: {result.JsonReportPath}");
    Console.WriteLine($"Evidence: {Path.Combine(result.OutputDirectory, "evidence")}");
    return result.Report.Decision == CalcyProbeDecision.PackageMissing ? 7 : 0;
}

static async Task<int> RunCalcyLiveCheckAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "fake");
    var outputDirectory = Require(options, "out");
    var automationProfile = await AutomationProfileLoader.LoadAsync(
        Require(options, "profile"),
        cancellationToken);
    var screenProfile = await ScreenProfileLoader.LoadAsync(
        Require(options, "screen-profile"),
        cancellationToken);
    var probeOptions = new CalcyProbeOptions
    {
        PackageName = Optional(options, "package") ?? CalcyProbeOptions.DefaultPackageName,
        MaximumLogcatLines = ParsePositiveInt(options, "max-log-lines", 4000)
    };
    var settleMilliseconds = ParseNonNegativeInt(options, "settle-ms", 2000);
    CalcyTextParserProfile? parserProfile = null;
    if (Optional(options, "parser-profile") is { } parserProfilePath)
    {
        parserProfile = await CalcyTextParserProfileLoader.LoadAsync(
            parserProfilePath,
            cancellationToken);
    }

    IAndroidAutomationTransport automationTransport;
    IAndroidAppInspectionTransport inspectionTransport;
    if (options.ContainsKey("fake"))
    {
        var screenFixtures = Optional(options, "screen-fixtures") ??
            Path.Combine("data", "screen-fixtures");
        var probeFixtures = Optional(options, "probe-fixtures") ??
            Path.Combine("data", "calcy-probe");
        automationTransport = await CreateScriptedTransportAsync(
            screenFixtures,
            cancellationToken);
        inspectionTransport = CreateScriptedCalcyProbeTransport(
            probeFixtures,
            "FAKE-AUTO-001");
        Console.WriteLine("Using scripted navigation and Calcy inspection evidence.");
    }
    else
    {
        var realTransport = CreateRealAndroidTransport(options);
        automationTransport = realTransport;
        inspectionTransport = realTransport as IAndroidAppInspectionTransport ??
            throw new InvalidOperationException(
                "The configured Android transport does not support app inspection.");
    }

    var result = await new CalcyLiveCheckRunner(
        automationTransport,
        inspectionTransport,
        new ConsoleDeviceLog()).RunAsync(
            outputDirectory,
            automationProfile,
            screenProfile,
            probeOptions,
            parserProfile,
            Optional(options, "serial"),
            TimeSpan.FromMilliseconds(settleMilliseconds),
            cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Automatic Calcy live check finished.");
    Console.WriteLine($"Navigation items: {result.Navigation.Checkpoint.Items.Count}");
    Console.WriteLine($"Probe decision: {result.Probe.Report.Decision}");
    Console.WriteLine($"Calcy version: {result.Probe.Report.Package.VersionName ?? "Unknown"}");
    Console.WriteLine($"Filtered log lines: {result.Probe.Report.FilteredLogLineCount}");
    if (result.ParsedObservation is not null)
    {
        Console.WriteLine($"Parsed observation: {result.ParsedObservation.Status}");
        Console.WriteLine($"Species: {result.ParsedObservation.Species ?? "Unknown"}");
        Console.WriteLine($"Parsed file: {result.ParsedObservationPath}");
    }

    return result.ParsedObservation?.Status == CalcyObservationStatus.Complete ||
           (parserProfile is null && result.Probe.Report.Package.IsInstalled)
        ? 0
        : 9;
}

static async Task<int> ParseCalcyOutputAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inputPath = Require(options, "input");
    var profilePath = Require(options, "profile");
    var outputPath = Require(options, "out");
    var sourceName = Optional(options, "source-name") ?? "logcat";

    var profile = await CalcyTextParserProfileLoader.LoadAsync(
        profilePath,
        cancellationToken);
    var raw = await File.ReadAllTextAsync(inputPath, cancellationToken);
    var observation = new CalcyRawTextParser().Parse(
        profile,
        new CalcyRawOutputBundle
        {
            Sources = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sourceName] = raw
            }
        },
        $"ProfileParser:{sourceName}");

    var fullPath = Path.GetFullPath(outputPath);
    var directory = Path.GetDirectoryName(fullPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }

    await File.WriteAllTextAsync(
        fullPath,
        JsonSerializer.Serialize(
            observation,
            CalcyTextParserProfileLoader.CreateJsonOptions(writeIndented: true)),
        cancellationToken);

    Console.WriteLine($"Observation status: {observation.Status}");
    Console.WriteLine($"Species: {observation.Species ?? "Unknown"}");
    Console.WriteLine($"CP: {observation.Cp?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"}");
    Console.WriteLine($"IV: {observation.AttackIv?.ToString(CultureInfo.InvariantCulture) ?? "?"}/" +
                      $"{observation.DefenseIv?.ToString(CultureInfo.InvariantCulture) ?? "?"}/" +
                      $"{observation.HpIv?.ToString(CultureInfo.InvariantCulture) ?? "?"}");
    Console.WriteLine($"Output: {fullPath}");
    return observation.Status is CalcyObservationStatus.Complete or CalcyObservationStatus.Partial
        ? 0
        : 8;
}


static async Task<int> InitializeCalcyVerificationAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var path = await CalcyVerificationTemplateWriter.InitializeAsync(
        Require(options, "out"),
        ParsePositiveInt(options, "cases", 20),
        cancellationToken);
    Console.WriteLine("Local Calcy verification workspace created.");
    Console.WriteLine($"Manifest: {path}");
    Console.WriteLine("The workspace is local evidence and should remain outside Git.");
    return 0;
}

static async Task<int> RunCalcyVerificationAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    CalcyTextParserProfile? parserProfile = null;
    if (Optional(options, "parser-profile") is { } parserPath)
    {
        parserProfile = await CalcyTextParserProfileLoader.LoadAsync(
            parserPath,
            cancellationToken);
    }

    var report = await new CalcyVerificationRunner().RunAsync(
        Require(options, "manifest"),
        Require(options, "evidence-root"),
        Require(options, "out"),
        parserProfile,
        cancellationToken);
    Console.WriteLine("Calcy provider verification finished.");
    Console.WriteLine($"Cases: {report.CaseCount}");
    Console.WriteLine($"Exact Complete: {report.ExactCompleteCount}");
    Console.WriteLine($"Wrong Complete: {report.WrongCompleteCount}");
    Console.WriteLine($"Exact rate: {report.ExactCompleteRate:P1}");
    Console.WriteLine($"Recommended for long scan: {report.RecommendedForLongScan}");
    Console.WriteLine($"Gate: {report.GateDetail}");
    return report.RecommendedForLongScan ? 0 : 10;
}

static async Task<int> SelectCalcyProviderAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var mechanismText = Require(options, "mechanism");
    if (!Enum.TryParse<CalcyProviderMechanism>(
            mechanismText,
            ignoreCase: true,
            out var mechanism))
    {
        throw new ArgumentException(
            "--mechanism must be PidWindowedLogcat, LocalText or VisualOverlay.");
    }

    var selection = await CalcyProviderSelectionService.SelectAsync(
        Require(options, "report"),
        mechanism,
        Require(options, "version"),
        Require(options, "out"),
        Optional(options, "parser-profile"),
        cancellationToken);
    Console.WriteLine("Verified Calcy provider selection written.");
    Console.WriteLine($"Mechanism: {selection.Mechanism}");
    Console.WriteLine($"Cases: {selection.VerifiedCaseCount}");
    Console.WriteLine($"Exact rate: {selection.ExactCompleteRate:P1}");
    Console.WriteLine($"Output: {Path.GetFullPath(Require(options, "out"))}");
    return 0;
}


static async Task<int> RunImagePretestAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inputDirectory = Require(options, "input");
    var outputDirectory = Require(options, "out");
    var pretestOptions = new ImagePretestOptions
    {
        MinimumImageCount = ParsePositiveInt(options, "min-images", 20),
        NearDuplicateThreshold = ParseUnitDouble(
            options,
            "near-duplicate-threshold",
            0.995),
        ClusterThreshold = ParseUnitDouble(
            options,
            "cluster-threshold",
            0.925),
        MinimumDecodeRate = ParseUnitDouble(
            options,
            "min-decode-rate",
            0.90)
    };

    var report = await ImagePretestRunner.RunAsync(
        inputDirectory,
        pretestOptions,
        cancellationToken);
    await ImagePretestReportWriter.WriteAsync(
        report,
        outputDirectory,
        cancellationToken);

    Console.WriteLine($"iPhone image pretest: {report.DecodedCount}/{report.ImageCount} decoded.");
    Console.WriteLine($"Geometry groups: {report.GeometryGroupCount}.");
    Console.WriteLine($"Visual clusters: {report.ClusterCount}.");
    Console.WriteLine($"Exact duplicates: {report.ExactDuplicatePairCount}.");
    Console.WriteLine($"Near duplicates: {report.NearDuplicatePairCount}.");
    Console.WriteLine($"Decode rate: {report.DecodeRate:P1} " +
        $"(required {report.MinimumDecodeRate:P1}).");
    foreach (var image in report.Images.Where(image => !image.Decoded))
    {
        Console.WriteLine(
            $"Rejected image: {image.FileName}: " +
            $"{image.ErrorCode}: {image.ErrorDetail}");
    }
    Console.WriteLine(report.GateDetail);
    return report.Accepted ? 0 : 1;
}

static async Task<int> RunImageRegionDiscoveryAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inputDirectory = Require(options, "input");
    var outputDirectory = Require(options, "out");
    var discoveryOptions = new RegionDiscoveryOptions
    {
        MinimumDecodedImages = ParsePositiveInt(options, "min-images", 20),
        MinimumDecodeRate = ParseUnitDouble(options, "min-decode-rate", 0.90),
        NearDuplicateThreshold = ParseUnitDouble(
            options,
            "near-duplicate-threshold",
            0.995),
        ClusterThreshold = ParseUnitDouble(
            options,
            "cluster-threshold",
            0.925),
        GridColumns = ParsePositiveInt(options, "grid-columns", 12),
        GridRows = ParsePositiveInt(options, "grid-rows", 24),
        MaximumCandidatesPerKind = ParsePositiveInt(
            options,
            "max-candidates-per-kind",
            6)
    };

    var report = await RegionDiscoveryRunner.RunAsync(
        inputDirectory,
        discoveryOptions,
        cancellationToken);
    await RegionDiscoveryReportWriter.WriteAsync(
        report,
        outputDirectory,
        cancellationToken);

    Console.WriteLine(
        $"iPhone region discovery: {report.DecodedCount}/{report.ImageCount} decoded.");
    Console.WriteLine($"Visual clusters: {report.ClusterCount}.");
    Console.WriteLine(
        $"Grid cells: {report.CellCount} " +
        $"({report.GridColumns}x{report.GridRows}).");
    foreach (var kind in Enum.GetValues<RegionCandidateKind>())
    {
        Console.WriteLine(
            $"{kind} candidates: {report.CandidateCount(kind)}.");
    }
    Console.WriteLine(report.GateDetail);
    return report.Accepted ? 0 : 11;
}


static async Task<int> RunImageCropAtlasAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inputDirectory = Require(options, "input");
    var regionReportPath = Require(options, "region-report");
    var outputDirectory = Require(options, "out");
    var atlasOptions = new CropAtlasOptions
    {
        MaximumCandidates = ParsePositiveInt(
            options,
            "max-candidates",
            8),
        RepresentativesPerCluster = ParsePositiveInt(
            options,
            "representatives-per-cluster",
            2),
        MaximumCropWidth = ParsePositiveInt(
            options,
            "max-crop-width",
            640),
        MaximumCropHeight = ParsePositiveInt(
            options,
            "max-crop-height",
            480),
        OverviewThumbnailWidth = ParsePositiveInt(
            options,
            "overview-width",
            220),
        OverviewThumbnailHeight = ParsePositiveInt(
            options,
            "overview-height",
            480),
        MaximumSameKindOverlap = ParseUnitDouble(
            options,
            "max-same-kind-overlap",
            0.35)
    };

    var report = await CropAtlasRunner.RunAsync(
        inputDirectory,
        regionReportPath,
        outputDirectory,
        atlasOptions,
        cancellationToken);
    await CropAtlasReportWriter.WriteAsync(
        report,
        outputDirectory,
        cancellationToken);

    Console.WriteLine(
        $"iPhone crop atlas: {report.CropCount} crops from " +
        $"{report.SelectedRegionCount} candidate regions.");
    Console.WriteLine($"Visual clusters: {report.ClusterCount}.");
    Console.WriteLine(
        $"Semantic experiment readiness: " +
        $"{report.Readiness.ReadyForSemanticExperiments}.");
    Console.WriteLine(
        $"More images indicated: {report.Readiness.NeedsMoreImages}.");
    foreach (var reason in report.Readiness.Reasons)
    {
        Console.WriteLine($"Readiness: {reason}");
    }
    Console.WriteLine(report.GateDetail);
    return report.Accepted ? 0 : 12;
}


static async Task<int> RunImageSemanticEvidenceAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inputDirectory = Require(options, "input");
    var regionReportPath = Require(options, "region-report");
    var cropAtlasReportPath = Require(options, "crop-atlas-report");
    var outputDirectory = Require(options, "out");
    var evidenceOptions = new SemanticEvidenceOptions
    {
        MinimumCaseCount = ParsePositiveInt(
            options,
            "min-cases",
            20),
        MinimumCasesPerCluster = ParsePositiveInt(
            options,
            "min-cases-per-cluster",
            2),
        MaximumCropWidth = ParsePositiveInt(
            options,
            "max-crop-width",
            640),
        MaximumCropHeight = ParsePositiveInt(
            options,
            "max-crop-height",
            480)
    };

    var report = await SemanticEvidenceRunner.RunAsync(
        inputDirectory,
        regionReportPath,
        cropAtlasReportPath,
        outputDirectory,
        evidenceOptions,
        cancellationToken);
    await SemanticEvidenceReportWriter.WriteAsync(
        report,
        outputDirectory,
        cancellationToken);

    Console.WriteLine(
        $"Semantic evidence: {report.CaseCount} cases and " +
        $"{report.CropCount} derived crops.");
    Console.WriteLine($"Visual clusters: {report.ClusterCount}.");
    Console.WriteLine(
        $"Ready for external visual review: " +
        $"{report.Readiness.ReadyForExternalVisualReview}.");
    Console.WriteLine(
        $"Ready for automated extraction: " +
        $"{report.Readiness.ReadyForAutomatedExtraction}.");
    Console.WriteLine(
        $"More images indicated: " +
        $"{report.Readiness.NeedsMoreImages}.");
    foreach (var reason in report.Readiness.Reasons)
    {
        Console.WriteLine($"Readiness: {reason}");
    }
    Console.WriteLine(
        $"Review pack: {Path.Combine(
            outputDirectory,
            report.ReviewPackFile)}");
    Console.WriteLine(report.GateDetail);
    return report.Accepted ? 0 : 13;
}


static async Task<int> RunAppraisalPretestAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var inputDirectory = Require(options, "input");
    var profilePath = Require(options, "profile");
    var outputDirectory = Require(options, "out");
    var regionReportPath = Optional(options, "region-report");
    var pretestOptions = new AppraisalPretestOptions
    {
        MinimumDecodedImages = ParsePositiveInt(
            options,
            "min-images",
            20),
        MinimumCandidateImages = ParsePositiveInt(
            options,
            "min-candidates",
            5),
        MinimumDominantClusterShare = ParseUnitDouble(
            options,
            "min-dominant-cluster-share",
            0.70)
    };

    var report = await AppraisalPretestRunner.RunAsync(
        inputDirectory,
        profilePath,
        outputDirectory,
        pretestOptions,
        regionReportPath,
        cancellationToken);
    await AppraisalPretestReportWriter.WriteAsync(
        report,
        outputDirectory,
        cancellationToken);

    Console.WriteLine(
        $"Appraisal pretest: {report.CandidateCount} candidates from " +
        $"{report.DecodedCount}/{report.ImageCount} decoded screenshots.");
    Console.WriteLine(
        $"Complete observations: {report.CompleteCount}.");
    Console.WriteLine(
        $"Dominant candidate cluster: " +
        $"{report.DominantCandidateCluster ?? "Unavailable"} " +
        $"({report.DominantCandidateClusterShare:P1}).");
    foreach (var warning in report.Warnings)
    {
        Console.WriteLine($"Warning: {warning}");
    }
    Console.WriteLine(report.GateDetail);
    return report.Accepted ? 0 : 14;
}

static async Task<int> PreparePhoneAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var outputDirectory = Require(options, "out");
    var profile = await AppraisalProfileLoader.LoadAsync(
        Require(options, "profile"),
        cancellationToken);
    var transport = CreateRealAndroidTransport(options);
    var runner = new PhonePreparationRunner(
        transport,
        new ConsoleDeviceLog());
    var report = await runner.RunAsync(
        outputDirectory,
        profile,
        Optional(options, "serial"),
        cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Android phone preparation complete.");
    Console.WriteLine($"Device: {report.Device.Serial}");
    Console.WriteLine(
        $"Screenshot: {report.ScreenshotWidth}x{report.ScreenshotHeight}");
    Console.WriteLine(
        $"Appraisal status: {report.Appraisal.Status}");
    Console.WriteLine(
        $"Appraisal calibration ready: " +
        $"{report.AppraisalCalibrationReady}");
    Console.WriteLine(
        $"Verified IV extraction ready: " +
        $"{report.VerifiedIvExtractionReady}");
    Console.WriteLine(
        $"Automatic navigation ready: " +
        $"{report.AutomaticNavigationReady}");
    foreach (var action in report.NextActions)
    {
        Console.WriteLine($"Next: {action}");
    }

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


static async Task<int> CaptureCalibrationScreenAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var workspace = await CalibrationWorkspace.InitializeAsync(
        Require(options, "workspace"),
        cancellationToken);
    var plan = await CalibrationCapturePlanLoader.LoadAsync(
        workspace.CapturePlanPath,
        cancellationToken);
    var expectedState = ParseScreenState(Require(options, "state"));
    var transport = CreateRealAndroidTransport(options);
    var service = new CalibrationCaptureService(transport, new ConsoleDeviceLog());
    var result = await service.CaptureAsync(
        workspace,
        plan,
        expectedState,
        Optional(options, "serial"),
        Optional(options, "notes"),
        cancellationToken);

    await CalibrationCaptureReportWriter.WriteAsync(
        result.Status,
        Path.Combine(workspace.ReportsPath, "capture"),
        cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Private read-only screenshot captured.");
    Console.WriteLine($"Capture id: {result.Capture.Id}");
    Console.WriteLine($"Expected state: {result.Capture.ExpectedState}");
    Console.WriteLine($"Image: {result.AbsoluteImagePath}");
    Console.WriteLine($"SHA-256: {result.Capture.Sha256}");
    Console.WriteLine($"Duplicate: {(result.Capture.IsDuplicate ? result.Capture.DuplicateOfCaptureId : "No")}");
    Console.WriteLine($"Next recommended state: {result.Status.NextRecommendedState?.ToString() ?? "None"}");
    Console.WriteLine("The image remains unapproved in incoming/ until explicit privacy review.");
    return 0;
}

static async Task<int> RunCalibrationCaptureSessionAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var workspace = await CalibrationWorkspace.InitializeAsync(
        Require(options, "workspace"),
        cancellationToken);
    var plan = await CalibrationCapturePlanLoader.LoadAsync(
        workspace.CapturePlanPath,
        cancellationToken);
    var transport = CreateRealAndroidTransport(options);
    var service = new CalibrationCaptureService(transport, new ConsoleDeviceLog());

    Console.WriteLine("Guided private calibration capture session");
    Console.WriteLine("Navigation remains manual. The program only reads metadata and captures screenshots.");
    Console.WriteLine("Type q at a prompt to stop safely.");
    Console.WriteLine();

    while (true)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
            workspace.CaptureSessionPath,
            plan,
            cancellationToken);
        await CalibrationCaptureService.VerifyExistingCaptureFilesAsync(
            workspace,
            session,
            cancellationToken);
        var status = CalibrationCaptureStatusBuilder.Build(plan, session);
        await CalibrationCaptureReportWriter.WriteAsync(
            status,
            Path.Combine(workspace.ReportsPath, "capture"),
            cancellationToken);

        if (status.RequiredCoverageComplete)
        {
            Console.WriteLine("Required capture coverage is complete.");
            Console.WriteLine("Review and approve screenshots before building a real screen profile.");
            return 0;
        }

        var state = status.NextRecommendedState ??
            throw new InvalidOperationException("Capture coverage is incomplete but no next state was selected.");
        var requirement = plan.Requirements.Single(x => x.State == state);
        var progress = status.States.Single(x => x.State == state);

        Console.WriteLine($"Next: {state} ({progress.UniqueCaptureCount}/{progress.RequiredUniqueCaptures})");
        Console.WriteLine(requirement.Instruction);
        foreach (var hint in requirement.VariationHints)
        {
            Console.WriteLine($"  - {hint}");
        }

        Console.Write("Navigate manually, then press Enter to capture, or q to stop: ");
        var input = Console.ReadLine();
        if (string.Equals(input?.Trim(), "q", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Capture session stopped without changing the phone.");
            return 0;
        }

        var result = await service.CaptureAsync(
            workspace,
            plan,
            state,
            Optional(options, "serial"),
            notes: null,
            cancellationToken: cancellationToken);
        Console.WriteLine($"Captured {result.Capture.Id} ({result.Capture.ImageWidth}x{result.Capture.ImageHeight}).");
        if (result.Capture.IsDuplicate)
        {
            Console.WriteLine($"Pixel-identical duplicate of {result.Capture.DuplicateOfCaptureId}; it does not count toward variation coverage.");
        }
        Console.WriteLine();
    }
}

static async Task<int> ShowCalibrationCaptureStatusAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var workspace = await CalibrationWorkspace.InitializeAsync(
        Require(options, "workspace"),
        cancellationToken);
    var plan = await CalibrationCapturePlanLoader.LoadAsync(
        workspace.CapturePlanPath,
        cancellationToken);
    var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
        workspace.CaptureSessionPath,
        plan,
        cancellationToken);
    await CalibrationCaptureService.VerifyExistingCaptureFilesAsync(
        workspace,
        session,
        cancellationToken);
    var status = CalibrationCaptureStatusBuilder.Build(plan, session);
    var reportDirectory = Path.Combine(workspace.ReportsPath, "capture");
    await CalibrationCaptureReportWriter.WriteAsync(status, reportDirectory, cancellationToken);

    Console.WriteLine($"Required coverage: {(status.RequiredCoverageComplete ? "COMPLETE" : "INCOMPLETE")}");
    Console.WriteLine($"Unique captures: {status.UniqueCaptureCount}");
    Console.WriteLine($"Duplicates: {status.DuplicateCaptureCount}");
    Console.WriteLine($"Promoted: {status.PromotedCaptureCount}");
    Console.WriteLine();
    foreach (var state in status.States)
    {
        Console.WriteLine(
            $"{state.State,-18} {state.UniqueCaptureCount,2}/{state.RequiredUniqueCaptures,-2} " +
            $"remaining {state.Remaining,2} promoted {state.PromotedCaptureCount,2}" +
            (state.OptionalWhenUnavailable ? " optional" : string.Empty));
    }

    if (session.Captures.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Captures:");
        foreach (var capture in session.Captures.OrderBy(x => x.SequenceNumber))
        {
            var flags = new List<string>();
            if (capture.IsDuplicate)
            {
                flags.Add($"duplicate of {capture.DuplicateOfCaptureId}");
            }
            if (capture.IsPromoted)
            {
                flags.Add($"fixture {capture.PromotedFixtureId}");
            }

            Console.WriteLine(
                $"  {capture.Id}  {capture.ExpectedState}" +
                (flags.Count == 0 ? string.Empty : $"  [{string.Join(", ", flags)}]"));
        }
    }

    Console.WriteLine();
    Console.WriteLine($"Reports: {Path.GetFullPath(reportDirectory)}");
    return 0;
}

static async Task<int> ApproveCalibrationCaptureAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "confirm-private-review");
    var workspace = await CalibrationWorkspace.InitializeAsync(
        Require(options, "workspace"),
        cancellationToken);
    var plan = await CalibrationCapturePlanLoader.LoadAsync(
        workspace.CapturePlanPath,
        cancellationToken);
    var result = await CalibrationCapturePromotionService.PromoteAsync(
        workspace,
        plan,
        Require(options, "id"),
        Require(options, "reviewed-by"),
        options.ContainsKey("confirm-private-review"),
        cancellationToken);

    var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
        workspace.CaptureSessionPath,
        plan,
        cancellationToken);
    var status = CalibrationCaptureStatusBuilder.Build(plan, session);
    await CalibrationCaptureReportWriter.WriteAsync(
        status,
        Path.Combine(workspace.ReportsPath, "capture"),
        cancellationToken);

    Console.WriteLine(result.AlreadyPromoted
        ? "Capture was already promoted and remains valid."
        : "Capture reviewed and promoted to an approved calibration fixture.");
    Console.WriteLine($"Capture: {result.CaptureId}");
    Console.WriteLine($"Fixture: {result.FixtureId}");
    Console.WriteLine($"Path: {result.FixturePath}");
    Console.WriteLine("The fixture remains local and must not be committed while the repository is public.");
    return 0;
}

static async Task<ScriptedAndroidAutomationTransport> CreateScriptedTransportAsync(
    string fixtures,
    CancellationToken cancellationToken)
{
    var screens = new Dictionary<ScreenState, byte[]>
    {
        [ScreenState.InventoryList] = await File.ReadAllBytesAsync(
            Path.Combine(fixtures, "InventoryList.png"),
            cancellationToken),
        [ScreenState.PokemonDetails] = await File.ReadAllBytesAsync(
            Path.Combine(fixtures, "PokemonDetails.png"),
            cancellationToken),
        [ScreenState.PokemonMenuOpen] = await File.ReadAllBytesAsync(
            Path.Combine(fixtures, "PokemonMenuOpen.png"),
            cancellationToken)
    };
    var appraisalScreens = new[]
    {
        "AppraisalOpen.png",
        "AppraisalOpenItem2.png",
        "AppraisalOpenItem3.png"
    }.Select(file => File.ReadAllBytes(
        Path.Combine(fixtures, file))).ToArray();
    return new ScriptedAndroidAutomationTransport(screens, appraisalScreens);
}

static IAndroidAutomationTransport CreateRealAndroidTransport(
    IReadOnlyDictionary<string, string> options)
{
    var harnessOptions = new DeviceHarnessOptions
    {
        AdbPath = Optional(options, "adb") ?? "adb",
        CommandTimeout = TimeSpan.FromSeconds(
            ParsePositiveInt(options, "timeout-seconds", 15)),
        HarnessVersion = DeviceHarnessOptions.CurrentVersion
    };
    harnessOptions.Validate();
    var log = new ConsoleDeviceLog();
    var runner = new AdbProcessRunner(harnessOptions.AdbPath, log);
    return new AdbAndroidDeviceTransport(runner, harnessOptions, log);
}

static IAndroidAppInspectionTransport CreateRealAndroidAppInspectionTransport(
    IReadOnlyDictionary<string, string> options)
{
    var transport = CreateRealAndroidTransport(options);
    return transport as IAndroidAppInspectionTransport ??
        throw new InvalidOperationException(
            "The configured Android transport does not support app inspection.");
}

static ScriptedAndroidAppInspectionTransport CreateScriptedCalcyProbeTransport(
    string fixtures,
    string serial = "CALCY-FAKE")
{
    var packageDump = File.ReadAllText(
        Path.Combine(fixtures, "package-dump.synthetic.txt"));
    var logcat = File.ReadAllText(
        Path.Combine(fixtures, "logcat.synthetic.txt"));
    var screenshotPath = Path.Combine("data", "screen-fixtures", "AppraisalOpen.png");

    return new ScriptedAndroidAppInspectionTransport(
        new[] { FakeAndroidDeviceTransport.CreateDescriptor(serial) },
        FakeAndroidDeviceTransport.CreateMetadata(serial),
        File.ReadAllBytes(screenshotPath),
        packageDumps: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CalcyProbeOptions.DefaultPackageName] = packageDump
        },
        packagePaths: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CalcyProbeOptions.DefaultPackageName] = "package:/data/app/tesmath.calcy/base.apk"
        },
        processIds: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CalcyProbeOptions.DefaultPackageName] = "4242"
        },
        appOps: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CalcyProbeOptions.DefaultPackageName] = "SYSTEM_ALERT_WINDOW: allow"
        },
        activityServices: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CalcyProbeOptions.DefaultPackageName] = "ServiceRecord{ tesmath.calcy/.ScanService }"
        },
        logcat: logcat,
        accessibilityState: "Enabled services: tesmath.calcy/.ScanService");
}

static ScreenState ParseScreenState(string value) =>
    Enum.TryParse<ScreenState>(value, ignoreCase: true, out var state)
        ? state
        : throw new ArgumentException(
            $"Unknown screen state '{value}'. Allowed values: " +
            string.Join(", ", Enum.GetNames<ScreenState>()));

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

static async Task<int> RunIndexSequenceAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "apply-index-tag", "apply-classification-tag", "resume", "no-resume");
    var query = Require(options, "query");
    var output = Require(options, "out");
    var itemLimit = ParsePositiveInt(options, "item-limit", 3);
    var profilePath = Optional(options, "automation-profile") ??
        Optional(options, "profile") ?? Path.Combine("local-data", "automation-profile.local.json");
    var automationProfile = await AutomationProfileLoader.LoadAsync(profilePath, cancellationToken);
    var appraisalPath = Optional(options, "appraisal-profile") ??
        Path.Combine("local-data", "phone-preparation", "appraisal-profile.device.generated.json");
    AppraisalVisualProfile? appraisalProfile = File.Exists(appraisalPath)
        ? await AppraisalProfileLoader.LoadAsync(appraisalPath, cancellationToken)
        : null;
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var evidence = Path.Combine(output, "evidence");
    var request = new VerifiedSequenceRequest
    {
        Query = query,
        ItemLimit = itemLimit,
        OutputDirectory = output,
        ApplyIndexTag = options.ContainsKey("apply-index-tag"),
        IndexTag = Optional(options, "index-tag") ?? "AI-Indexed",
        ApplyClassificationTag = options.ContainsKey("apply-classification-tag"),
        ClassificationTag = Optional(options, "classification-tag"),
        ControlledStopAfter = ParseOptionalPositiveInt(options, "controlled-stop-after"),
        Resume = !options.ContainsKey("no-resume")
    };
    var operations = new AndroidVerifiedInventoryNamedOperations(
        transport, selected.Serial, automationProfile, evidence, appraisalProfile);
    var result = await new VerifiedInventoryTaskSequence(operations).RunAsync(request, cancellationToken: cancellationToken);
    Console.WriteLine(JsonSerializer.Serialize(result.Checkpoint,
        new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));
    return result.Checkpoint.State is VerifiedSequenceState.Completed or VerifiedSequenceState.ControlledStopped ? 0 : 1;
}

static async Task<int> RunCleanupProofAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args, "continue-on-partial");
    var species = Require(options, "species");
    var itemLimit = ParsePositiveInt(options, "item-limit", 6);
    if (itemLimit is < 6 or > 20)
        throw new ArgumentException("--item-limit must be between 6 and 20.");
    if (!options.ContainsKey("continue-on-partial"))
        throw new ArgumentException("--continue-on-partial is required for cleanup proof.");
    var database = Require(options, "database");
    var output = Path.GetFullPath(Require(options, "out"));
    var profilePath = Optional(options, "automation-profile") ??
        Optional(options, "profile") ?? Path.Combine("local-data", "automation-profile.local.json");
    var automationProfile = await AutomationProfileLoader.LoadAsync(profilePath, cancellationToken);
    var appraisalPath = Optional(options, "appraisal-profile") ??
        Path.Combine("local-data", "phone-preparation", "appraisal-profile.device.generated.json");
    AppraisalVisualProfile? appraisalProfile = File.Exists(appraisalPath)
        ? await AppraisalProfileLoader.LoadAsync(appraisalPath, cancellationToken)
        : null;
    var transport = CreateRealAndroidTransport(options);
    var selected = DeviceSnapshotService.SelectDevice(
        await transport.ListDevicesAsync(cancellationToken), Optional(options, "serial"));
    var evidence = Path.Combine(output, "evidence");
    Directory.CreateDirectory(evidence);
    var operations = new AndroidVerifiedInventoryNamedOperations(
        transport, selected.Serial, automationProfile, evidence, appraisalProfile);
    var recovery = await new CanonicalCloseUnwindService().UnwindToGameplayMapAsync(
        operations, cancellationToken);
    await File.WriteAllTextAsync(
        Path.Combine(output, "start-state-recovery.json"),
        JsonSerializer.Serialize(recovery, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
    await File.WriteAllTextAsync(
        Path.Combine(output, "start-state-recovery.md"),
        $"# Start-state recovery\n\n- Initial state: `{recovery.InitialState}`\n- Recovery path: `{string.Join(" -> ", recovery.Path)}`\n- Actions: `{string.Join(", ", recovery.Actions)}`\n- Input count: `{recovery.InputCount}`\n- Ready state: `{recovery.FinalState}`\n- Result: `{recovery.Result}`\n- Blocker: `{recovery.Blocker ?? "NONE"}`\n",
        cancellationToken);
    if (!recovery.Succeeded)
    {
        Console.Error.WriteLine($"CLEANUP_START_RECOVERY_BLOCKED: {recovery.Blocker ?? recovery.Result}");
        return 1;
    }

    var request = new CleanupProofRequest
    {
        SpeciesQuery = species,
        ItemLimit = itemLimit,
        DatabasePath = database,
        OutputDirectory = output,
        DeviceSerial = selected.Serial,
        ContinueOnPartial = true,
        MaximumCaptureFrames = ParsePositiveInt(options, "maximum-capture-frames", 8),
        MinimumCompleteFrames = ParsePositiveInt(options, "minimum-complete-frames", 3),
        MinimumPartialFrames = ParsePositiveInt(options, "minimum-partial-frames", 2)
    };
    var result = await new CleanupProofRunner().RunAsync(operations, request, cancellationToken);
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    }));
    return result.CapturedItems >= 6 &&
        result.Status is "Completed" or "CompletedPartial" ? 0 : 1;
}

static async Task<int> ValidateNavigationSafetyAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    var cycles = ParsePositiveInt(options, "cycles", 1);
    if (cycles > 3)
        throw new ArgumentException("--cycles must be between 1 and 3.");
    Directory.CreateDirectory(output);

    var profilePath = Optional(options, "profile") ??
        Optional(options, "automation-profile") ??
        Path.Combine("local-data", "automation-profile.local.json");
    var automationProfile = await AutomationProfileLoader.LoadAsync(profilePath, cancellationToken);
    var appraisalPath = Optional(options, "appraisal-profile") ??
        Path.Combine("local-data", "phone-preparation", "appraisal-profile.device.generated.json");
    AppraisalVisualProfile? appraisalProfile = File.Exists(appraisalPath)
        ? await AppraisalProfileLoader.LoadAsync(appraisalPath, cancellationToken)
        : null;
    var transport = CreateRealAndroidTransport(options);
    var selected = DeviceSnapshotService.SelectDevice(
        await transport.ListDevicesAsync(cancellationToken), Optional(options, "serial"));
    var trace = new NavigationSafetyTraceRecorder(output);
    var operations = new AndroidVerifiedInventoryNamedOperations(
        transport,
        selected.Serial,
        automationProfile,
        Path.Combine(output, "host-audit"),
        appraisalProfile,
        navigationTrace: trace);
    var summaries = new List<object>();
    var allPassed = true;

    for (var cycle = 1; cycle <= cycles; cycle++)
    {
        trace.BeginCycle(cycle);
        var cycleDirectory = Path.Combine(output, "precondition", $"cycle-{cycle:00}");
        var mapPrecondition = await CaptureStableMapPreconditionAsync(cycleDirectory);
        if (!mapPrecondition)
        {
            summaries.Add(new
            {
                cycle,
                result = "FAIL_PRECONDITION_GAMEPLAY_MAP",
                inputCount = 0,
                backCount = 0,
                postFrameCount = 0
            });
            allPassed = false;
            break;
        }

        string result = "PASS";
        VerifiedSequenceState opened = VerifiedSequenceState.Unknown;
        VerifiedSequenceState details = VerifiedSequenceState.Unknown;
        VerifiedSequenceState returned = VerifiedSequenceState.Unknown;
        var closed = PokemonGoGameState.Unknown.ToString();
        try
        {
            opened = await operations.OpenInventoryAsync(cancellationToken);
            if (opened == VerifiedSequenceState.Inventory)
                details = await operations.OpenFirstPokemonAsync(cancellationToken);
            if (details == VerifiedSequenceState.PokemonDetails)
                returned = await operations.ReturnToInventoryAsync(cancellationToken);
            if (returned == VerifiedSequenceState.Inventory)
                closed = await operations.CloseInventoryAsync(cancellationToken);

            var entries = ReadNavigationTrace(output);
            var cycleEntries = entries.Where(entry => entry.Cycle == cycle).ToArray();
            var inputs = cycleEntries.Count(entry => entry.Phase == "INPUT_SENT");
            var backs = cycleEntries.Count(entry =>
                entry.Phase == "INPUT_SENT" && entry.TransportInputType == "PressBack");
            var postFrames = cycleEntries.Count(entry =>
                entry.Phase.StartsWith("POST_INPUT_FRAME_", StringComparison.Ordinal));
            var expectedPass = opened == VerifiedSequenceState.Inventory &&
                details == VerifiedSequenceState.PokemonDetails &&
                returned == VerifiedSequenceState.Inventory &&
                closed == PokemonGoGameState.GameplayMap.ToString() &&
                inputs == 5 && backs == 2 && postFrames == inputs * NavigationSafetyTraceRecorder.RequiredPostInputFrames;
            if (!expectedPass)
                result = "FAIL_SEQUENCE_OR_TRACE_INVARIANT";
            summaries.Add(new
            {
                cycle,
                result,
                opened = opened.ToString(),
                details = details.ToString(),
                returned = returned.ToString(),
                closed = closed.ToString(),
                inputCount = inputs,
                backCount = backs,
                postFrameCount = postFrames,
                noUnsafeInput = cycleEntries.All(entry =>
                    entry.Phase != "INPUT_SENT" || entry.AuthorizationResult == "AUTHORIZED")
            });
            allPassed &= expectedPass;
        }
        catch (Exception exception)
        {
            result = "FAIL_EXCEPTION";
            summaries.Add(new
            {
                cycle,
                result,
                exception = exception.Message,
                inputCount = ReadNavigationTrace(output).Count(entry =>
                    entry.Cycle == cycle && entry.Phase == "INPUT_SENT")
            });
            allPassed = false;
            break;
        }

        if (!allPassed)
            break;
    }

    var summaryPath = Path.Combine(output, "cycle-summary.json");
    await File.WriteAllTextAsync(
        summaryPath,
        JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            command = "device-validate-navigation-safety",
            serial = selected.Serial,
            requestedCycles = cycles,
            completedCycles = summaries.Count,
            result = allPassed ? "PASS" : "FAIL",
            summaries
        }, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
    await File.WriteAllTextAsync(
        Path.Combine(output, "phone-summary.md"),
        $"# Deterministic navigation safety\n\n" +
        $"- Serial: `{selected.Serial}`\n" +
        $"- Requested cycles: {cycles}\n" +
        $"- Completed cycles: {summaries.Count}\n" +
        $"- Result: **{(allPassed ? "PASS" : "FAIL")}**\n" +
        "- Scope: read-only navigation only; no tags or destructive actions.\n" +
        "- Post-input evidence: exactly five bounded frames per authorized input.\n",
        cancellationToken);
    Console.WriteLine($"Navigation safety result: {(allPassed ? "PASS" : "FAIL")}; cycles: {summaries.Count}/{cycles}.");
    return allPassed ? 0 : 1;

    async Task<bool> CaptureStableMapPreconditionAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var detector = new PokemonGoGameStateDetector();
        var allMap = true;
        for (var index = 1; index <= 3; index++)
        {
            var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
            var detection = detector.Detect(screenshot, appraisalProfile);
            await File.WriteAllBytesAsync(
                Path.Combine(directory, $"frame-{index}.png"), screenshot, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(directory, $"frame-{index}.json"),
                JsonSerializer.Serialize(new
                {
                    state = detection.State.ToString(),
                    confidence = detection.Confidence,
                    evidence = detection.Evidence,
                    screenshotSha256 = detection.ScreenshotSha256
                }, new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);
            allMap &= detection.State == PokemonGoGameState.GameplayMap;
            if (index < 3)
                await Task.Delay(automationProfile.PostActionSettleMilliseconds, cancellationToken);
        }
        return allMap;
    }

    static IReadOnlyList<NavigationSafetyTraceEntry> ReadNavigationTrace(string directory)
    {
        var path = Path.Combine(directory, "action-trace.jsonl");
        if (!File.Exists(path)) return Array.Empty<NavigationSafetyTraceEntry>();
        return File.ReadLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<NavigationSafetyTraceEntry>(line)!)
            .ToArray();
    }
}

static async Task<int> StopKnownAppAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var app = Require(options, "app");
    if (!app.Equals("calcy", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("Only --app calcy is allow-listed.");
    }

    var transport = CreateRealAndroidAppInspectionTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(
        devices,
        Optional(options, "serial"));
    await transport.StopKnownAppAsync(
        selected.Serial,
        KnownAndroidPackage.Calcy,
        cancellationToken);
    Console.WriteLine($"Stopped Calcy on {selected.Serial}.");
    return 0;
}

static async Task<int> OpenInventoryAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    await transport.OpenPokemonInventoryAsync(selected.Serial, cancellationToken);
    Console.WriteLine($"Sent one allow-listed OpenPokemonInventory action to {selected.Serial}.");
    return 0;
}

static async Task<int> PressBackAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    await transport.PressBackAsync(selected.Serial, cancellationToken);
    Console.WriteLine($"Pressed Back once on {selected.Serial}.");
    return 0;
}

static async Task<int> ContinueAppraisalIntroAsync(string[] args, CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    Directory.CreateDirectory(output);
    var transport = CreateRealAndroidTransport(options);
    var selected = DeviceSnapshotService.SelectDevice(await transport.ListDevicesAsync(cancellationToken), Optional(options, "serial"));
    var profile = await AppraisalProfileLoader.LoadAsync(Require(options, "appraisal-profile"), cancellationToken);
    var recovery = new GuardedInventoryRecovery();
    var frames = new List<RecoveryFrame>();
    var audit = new List<object>();
    var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
    var index = 0;
    var introTapActions = 0;
    var tapWasSent = false;
    while (DateTimeOffset.UtcNow < deadline)
    {
        var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
        var frame = recovery.Observe(screenshot, profile);
        frames.Add(frame);
        var pollName = $"poll-{index++:00}";
        await File.WriteAllBytesAsync(Path.Combine(output, $"{pollName}.png"), screenshot, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, $"{pollName}.json"),
            JsonSerializer.Serialize(FrameDiagnostic(frame),
                new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        var decision = GuardedInventoryRecovery.DecideAppraisalContinuation(
            frames, introTapActions, tapWasSent);
        if (decision is AppraisalContinuationOutcome.SUCCESS_ALREADY_ADVANCED or
            AppraisalContinuationOutcome.SUCCESS_TAPPED)
        {
            await WriteAuditAsync(decision.ToString());
            Console.WriteLine($"RESULT: {decision}; PHONE_ACTIONS: {introTapActions}");
            return 0;
        }

        if (decision == AppraisalContinuationOutcome.TAP_INTRO_ONCE &&
            GuardedInventoryRecovery.TryGetStableFrame(frames, out var stableIntro) &&
            stableIntro?.LocatorTarget is { } target)
        {
            var image = PngDecoder.Decode(stableIntro.Screenshot);
            var (x, y) = target.ToPixels(image.Width, image.Height);
            var startedAt = DateTimeOffset.UtcNow;
            await transport.TapAsync(selected.Serial, x, y, cancellationToken);
            var completedAt = DateTimeOffset.UtcNow;
            introTapActions++;
            tapWasSent = true;
            audit.Add(new
            {
                sequence = introTapActions,
                action = "TapAppraisalIntroContinue",
                stateBefore = stableIntro.Detection.State.ToString(),
                expectedState = "AppraisalBars",
                startedAtUtc = startedAt,
                completedAtUtc = completedAt,
                detail = $"Visually located target ({x},{y}); confidence {stableIntro.LocatorConfidence:F3}."
            });
            frames.Clear();
        }
        else if (decision == AppraisalContinuationOutcome.FAIL_CLOSED)
        {
            await WriteAuditAsync(decision.ToString());
            Console.WriteLine($"RESULT: FAIL_CLOSED; PHONE_ACTIONS: {introTapActions}");
            return 2;
        }

        await Task.Delay(250, cancellationToken);
    }

    await WriteAuditAsync("STABILITY_TIMEOUT");
    Console.WriteLine($"RESULT: STABILITY_TIMEOUT; PHONE_ACTIONS: {introTapActions}");
    return 1;

    Task WriteAuditAsync(string result) => File.WriteAllTextAsync(
        Path.Combine(output, "input-audit.json"),
        JsonSerializer.Serialize(new
        {
            result,
            phoneActions = introTapActions,
            actions = audit
        }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

    static object FrameDiagnostic(RecoveryFrame frame) => new
    {
        state = frame.Detection.State.ToString(),
        kind = frame.Kind.ToString(),
        confidence = frame.Detection.Confidence,
        evidence = frame.Detection.Evidence,
        screenshotSha256 = frame.Detection.ScreenshotSha256,
        introAnchor = frame.HasIntroAnchor,
        barsAnchor = frame.HasBarsAnchor,
        conflictingAnchor = frame.HasConflictingAnchor,
        locatorConfidence = frame.LocatorConfidence,
        locatorTarget = frame.LocatorTarget,
        stableRegions = frame.StableRegions.Select(region => region.Features),
        bars = frame.Bars
    };
}

static async Task<int> CloseInventoryAsync(string[] args, CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    Directory.CreateDirectory(output);
    var transport = CreateRealAndroidTransport(options);
    var selected = DeviceSnapshotService.SelectDevice(
        await transport.ListDevicesAsync(cancellationToken), Optional(options, "serial"));
    var detector = new PokemonGoGameStateDetector();
    var before = await CaptureCloseFrameAsync("before", null);
    if (!GuardedInventoryClose.CanAct(before.Detection))
    {
        Console.WriteLine($"Close inventory result: FAIL_CLOSED; state={before.Detection.State}; PHONE_ACTIONS: 0");
        return 2;
    }

    await transport.PressBackAsync(selected.Serial, cancellationToken);
    var post = await WaitForMapAsync();
    Console.WriteLine($"Close inventory result: {(GuardedInventoryClose.IsSuccessful(post.Detection) ? "PASS" : "FAIL")}; PHONE_ACTIONS: 1; state={post.Detection.State}");
    return GuardedInventoryClose.IsSuccessful(post.Detection) ? 0 : 1;

    async Task<(byte[] Screenshot, PokemonGoGameStateDetection Detection)> CaptureCloseFrameAsync(
        string name, PokemonGoGameState? expected)
    {
        var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
        var detection = detector.Detect(screenshot);
        var directory = Path.Combine(output, name);
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, name == "before" ? "before-screen.png" : "after-screen.png"), screenshot, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, "state.json"), JsonSerializer.Serialize(new
        {
            state = detection.State.ToString(), confidence = detection.Confidence,
            evidence = detection.Evidence, screenshotSha256 = detection.ScreenshotSha256,
            expected = expected?.ToString(), actionCount = name == "before" ? 0 : 1
        }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return (screenshot, detection);
    }

    async Task<(byte[] Screenshot, PokemonGoGameStateDetection Detection)> WaitForMapAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        (byte[] Screenshot, PokemonGoGameStateDetection Detection) latest = default;
        do
        {
            await Task.Delay(350, cancellationToken);
            latest = await CaptureCloseFrameAsync("after", PokemonGoGameState.GameplayMap);
            if (latest.Detection.State == PokemonGoGameState.GameplayMap || latest.Detection.State == PokemonGoGameState.Unknown)
                return latest;
        }
        while (DateTimeOffset.UtcNow < deadline);
        return latest;
    }
}

static async Task<int> DetectGameStateAsync(string[] args, CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    Directory.CreateDirectory(output);
    var transport = CreateRealAndroidTransport(options);
    var selected = DeviceSnapshotService.SelectDevice(
        await transport.ListDevicesAsync(cancellationToken), Optional(options, "serial"));
    var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
    var profile = Optional(options, "appraisal-profile") is { } profilePath
        ? await AppraisalProfileLoader.LoadAsync(profilePath, cancellationToken)
        : null;
    var detection = new PokemonGoGameStateDetector().Detect(screenshot, profile);
    await File.WriteAllBytesAsync(Path.Combine(output, "screen.png"), screenshot, cancellationToken);
    await File.WriteAllTextAsync(Path.Combine(output, "game-state.json"),
        JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            state = detection.State.ToString(),
            confidence = detection.Confidence,
            evidence = detection.Evidence,
            screenshotSha256 = detection.ScreenshotSha256,
            capturedAtUtc = DateTimeOffset.UtcNow,
            deviceSerial = selected.Serial
        }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    Console.WriteLine($"State: {detection.State}; confidence: {detection.Confidence:F3}; evidence: {string.Join(",", detection.Evidence)}");
    return 0;
}

static async Task<int> ExtractPokemonIdentityFingerprintAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var imagePaths = Require(options, "images")
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (imagePaths.Length == 0 || imagePaths.Length > 20)
        throw new ArgumentException("--images must contain between 1 and 20 semicolon-separated PNG paths.");
    var frames = new List<PokemonIdentityFrame>();
    foreach (var path in imagePaths)
        frames.Add(new PokemonIdentityFrame
        {
            ScreenshotPng = await File.ReadAllBytesAsync(path, cancellationToken)
        });
    var analyzer = new PokemonDetailsIdentityAnalyzer();
    var consensus = analyzer.Consensus(frames);
    var outputPath = Path.GetFullPath(Require(options, "out"));
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(
        new { schemaVersion = "1.0", consensus },
        ScreenProfileLoader.CreateJsonOptions(writeIndented: true)), cancellationToken);
    Console.WriteLine($"Identity fingerprint written to: {outputPath}");
    Console.WriteLine($"Status: {consensus.Status}; confidence: {consensus.Confidence:F4}; " +
        $"stable fingerprint: {consensus.StableFingerprintSha256}");
    return consensus.Status switch
    {
        PokemonIdentityObservationStatus.Complete => 0,
        PokemonIdentityObservationStatus.Partial => 2,
        PokemonIdentityObservationStatus.Unavailable => 3,
        _ => 1
    };
}

static async Task<int> DetectGameStateImageAsync(string[] args, CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var imagePath = Require(options, "image");
    var output = Path.GetFullPath(Require(options, "out"));
    Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");
    var profile = Optional(options, "appraisal-profile") is { } profilePath
        ? await AppraisalProfileLoader.LoadAsync(profilePath, cancellationToken)
        : null;
    var detection = new PokemonGoGameStateDetector().Detect(
        await File.ReadAllBytesAsync(imagePath, cancellationToken), profile);
    await File.WriteAllTextAsync(output, JsonSerializer.Serialize(detection,
        new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    Console.WriteLine($"State: {detection.State}; confidence: {detection.Confidence:F3}");
    return 0;
}

static async Task<int> RecoverInventoryAsync(string[] args, CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    Directory.CreateDirectory(output);
    var transport = CreateRealAndroidTransport(options);
    var selected = DeviceSnapshotService.SelectDevice(
        await transport.ListDevicesAsync(cancellationToken), Optional(options, "serial"));
    var profile = Optional(options, "appraisal-profile") is { } profilePath
        ? await AppraisalProfileLoader.LoadAsync(profilePath, cancellationToken)
        : null;
    var recovery = new GuardedInventoryRecovery();
    var audit = new List<object>();
    var initialFrames = await CaptureStableWindowAsync("step-00", null, null);
    var outcome = recovery.Begin(initialFrames);
    while (outcome == RecoveryOutcome.PROGRESSED)
    {
        var authorization = recovery.AuthorizeNextAction();
        if (authorization is null)
        {
            outcome = RecoveryOutcome.UNEXPECTED_STOP;
            break;
        }

        var startedAt = DateTimeOffset.UtcNow;
        int? targetX = null;
        int? targetY = null;
        if (authorization.Action == RecoveryInputAction.ExitAppraisal &&
            authorization.Target is { } target && recovery.Current is { } current)
        {
            var image = PngDecoder.Decode(current.Screenshot);
            (targetX, targetY) = target.ToPixels(image.Width, image.Height);
            await transport.TapAsync(
                selected.Serial, targetX.Value, targetY.Value, cancellationToken);
        }
        else if (authorization.Action == RecoveryInputAction.PressBack)
        {
            await transport.PressBackAsync(selected.Serial, cancellationToken);
        }
        else
        {
            outcome = RecoveryOutcome.UNEXPECTED_STOP;
            break;
        }
        var completedAt = DateTimeOffset.UtcNow;
        var postFrames = await CaptureStableWindowAsync(
            $"step-{authorization.Sequence:00}",
            authorization.ExpectedState,
            authorization.ExpectedFrameKind);
        outcome = recovery.ObservePostAction(postFrames);
        audit.Add(new
        {
            sequence = authorization.Sequence,
            action = authorization.Action.ToString(),
            stateBefore = authorization.StateBefore.ToString(),
            expectedState = authorization.ExpectedState.ToString(),
            expectedKind = authorization.ExpectedFrameKind?.ToString(),
            stateAfter = recovery.Current?.Detection.State.ToString(),
            stateAfterKind = recovery.Current?.Kind.ToString(),
            outcome = outcome.ToString(),
            targetX,
            targetY,
            startedAtUtc = startedAt,
            completedAtUtc = completedAt,
            detail = authorization.Detail
        });
    }

    await File.WriteAllTextAsync(Path.Combine(output, "input-audit.json"),
        JsonSerializer.Serialize(new
        {
            result = outcome.ToString(),
            inputActions = recovery.InputActions,
            backActions = recovery.BackActions,
            appraisalTapActions = recovery.AppraisalTapActions,
            actions = audit
        }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    Console.WriteLine(
        $"Recovery result: {outcome}; input actions: {recovery.InputActions}; " +
        $"appraisal taps: {recovery.AppraisalTapActions}; Back actions: {recovery.BackActions}.");
    return outcome == RecoveryOutcome.SUCCEEDED
        ? 0
        : outcome == RecoveryOutcome.UNKNOWN_STOP ? 2 : 1;

    async Task<IReadOnlyList<RecoveryFrame>> CaptureStableWindowAsync(
        string name,
        PokemonGoGameState? expected,
        RecoveryFrameKind? expectedKind)
    {
        var directory = Path.Combine(output, name);
        Directory.CreateDirectory(directory);
        var frames = new List<RecoveryFrame>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(12);
        var poll = 0;
        do
        {
            var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
            var frame = recovery.Observe(screenshot, profile);
            frames.Add(frame);
            await File.WriteAllBytesAsync(
                Path.Combine(directory, $"poll-{poll:00}.png"), screenshot, cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(directory, $"poll-{poll:00}.json"),
                JsonSerializer.Serialize(new
                {
                    state = frame.Detection.State.ToString(),
                    kind = frame.Kind.ToString(),
                    confidence = frame.Detection.Confidence,
                    evidence = frame.Detection.Evidence,
                    screenshotSha256 = frame.Detection.ScreenshotSha256,
                    expected = expected?.ToString(),
                    expectedKind = expectedKind?.ToString(),
                    introAnchor = frame.HasIntroAnchor,
                    barsAnchor = frame.HasBarsAnchor,
                    conflictingAnchor = frame.HasConflictingAnchor,
                    locatorConfidence = frame.LocatorConfidence,
                    locatorTarget = frame.LocatorTarget,
                    stableRegions = frame.StableRegions.Select(region => region.Features),
                    bars = frame.Bars
                }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            poll++;
            if (GuardedInventoryRecovery.IsStable(frames))
            {
                return frames;
            }
            await Task.Delay(350, cancellationToken);
        }
        while (DateTimeOffset.UtcNow < deadline);

        return frames;
    }
}

static async Task<int> OpenPokemonGoMainMenuAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
    var control = new VisualControlLocator().LocateMainMenuPokeball(screenshot) ??
        throw new InvalidOperationException("The Pokémon GO main-menu Poké Ball was not visually verified; no action was taken.");
    var image = PngDecoder.Decode(screenshot);
    var (x, y) = control.Target.ToPixels(image.Width, image.Height);
    await transport.TapAsync(selected.Serial, x, y, cancellationToken);
    Console.WriteLine($"Opened the Pokémon GO main menu at visually detected ({x},{y}), confidence {control.Confidence:F3}.");
    return 0;
}

static async Task<int> OpenPokemonInventoryFromMainMenuAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
    var control = new VisualControlLocator().LocatePokemonInventory(screenshot) ??
        throw new InvalidOperationException("The Pokémon inventory control was not visually verified; no action was taken.");
    var image = PngDecoder.Decode(screenshot);
    var (x, y) = control.Target.ToPixels(image.Width, image.Height);
    await transport.TapAsync(selected.Serial, x, y, cancellationToken);
    Console.WriteLine($"Opened Pokémon inventory at visually detected ({x},{y}), confidence {control.Confidence:F3}.");
    return 0;
}

static async Task<int> SearchInventoryAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    var query = InventorySearchQuery.Validate(Require(options, "query"));
    var clearAfter = ParseBoolean(options, "clear-after", false);
    Directory.CreateDirectory(output);

    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var metadata = await transport.ReadMetadataAsync(selected.Serial, cancellationToken);
    var width = metadata.Screen.EffectiveWidth ??
        throw new InvalidOperationException("Screen width unavailable.");
    var height = metadata.Screen.EffectiveHeight ??
        throw new InvalidOperationException("Screen height unavailable.");
    var analyzer = new InventorySearchVisualAnalyzer();
    var detector = new PokemonGoGameStateDetector();
    var workflow = new GuardedInventorySearch();
    var audit = new List<object>();

    var before = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
    var beforeState = detector.Detect(before);
    var current = analyzer.Analyze(before);
    await File.WriteAllBytesAsync(Path.Combine(output, "before.png"), before, cancellationToken);
    await WriteJsonAsync("state-before.json", new
    {
        gameState = beforeState.State.ToString(),
        beforeState.Confidence,
        search = current
    });
    if (beforeState.State != PokemonGoGameState.Inventory ||
        workflow.Begin(current, query) != InventorySearchOutcome.Progressed)
    {
        throw new InvalidOperationException(
            "Inventory or InventorySearch was not visually verified; no search input was sent.");
    }

    InventorySearchOutcome outcome = InventorySearchOutcome.Progressed;
    while (outcome == InventorySearchOutcome.Progressed)
    {
        var authorization = workflow.AuthorizeNextAction() ??
            throw new InvalidOperationException("Search workflow could not authorize its next bounded action.");
        var actionBefore = current;
        int? x = null;
        int? y = null;
        switch (authorization.Action)
        {
            case InventorySearchAction.OpenSearch:
                _ = new VisualControlLocator().LocateInventoryCard(before) ??
                    throw new InvalidOperationException("Inventory search field was not visually grounded.");
                (x, y) = new NormalizedPoint { X = 0.5005, Y = 0.1881 }.ToPixels(width, height);
                await transport.TapAsync(selected.Serial, x.Value, y.Value, cancellationToken);
                break;
            case InventorySearchAction.ClearSearch:
                if (!current.ClearControlVisible)
                {
                    throw new InvalidOperationException("Search clear control was not visually verified.");
                }
                (x, y) = new NormalizedPoint { X = 0.9175, Y = 0.1881 }.ToPixels(width, height);
                await transport.TapAsync(selected.Serial, x.Value, y.Value, cancellationToken);
                break;
            case InventorySearchAction.EnterQuery:
                await transport.EnterInventorySearchQueryAsync(
                    selected.Serial, query, cancellationToken);
                break;
            case InventorySearchAction.SubmitQuery:
                await transport.SubmitInventorySearchQueryAsync(selected.Serial, cancellationToken);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported authorized search action '{authorization.Action}'.");
        }

        current = await WaitForSearchPostconditionAsync(authorization.Action);
        outcome = workflow.ObservePostAction(current);
        audit.Add(new
        {
            authorization.Sequence,
            action = authorization.Action.ToString(),
            authorization.ExpectedPostcondition,
            targetX = x,
            targetY = y,
            expectedQuery = query,
            verificationMode = "device-encoded-input-plus-visual-field-and-result",
            before = actionBefore,
            after = current,
            outcome = outcome.ToString()
        });
        if (outcome is not (InventorySearchOutcome.Progressed or InventorySearchOutcome.Succeeded))
        {
            throw new InvalidOperationException($"Inventory search stopped fail-closed: {outcome}.");
        }
    }

    var after = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
    current = analyzer.Analyze(after);
    await File.WriteAllBytesAsync(Path.Combine(output, "after.png"), after, cancellationToken);
    await WriteJsonAsync("state-after.json", new
    {
        gameState = detector.Detect(after).State.ToString(),
        expectedQuery = query,
        queryDeliveryContractSatisfied = true,
        visualFieldPopulated = current.QueryVisible,
        visualLengthCompatible = GuardedInventorySearch.IsQueryLengthCompatible(current, query),
        visibleResultCount = (int?)null,
        search = current
    });

    var cleared = false;
    if (clearAfter)
    {
        if (!current.ClearControlVisible)
        {
            throw new InvalidOperationException("Final search clear control was not visually verified.");
        }
        var clearTarget = new NormalizedPoint { X = 0.9175, Y = 0.1881 }.ToPixels(width, height);
        await transport.TapAsync(
            selected.Serial, clearTarget.X, clearTarget.Y, cancellationToken);
        var clearedEvidence = await WaitUntilAsync(value => !value.QueryVisible);
        var clearedPng = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(output, "cleared.png"), clearedPng, cancellationToken);
        audit.Add(new
        {
            sequence = workflow.InputActions + 1,
            action = "ClearCompletedSearch",
            targetX = clearTarget.X,
            targetY = clearTarget.Y,
            expectedQuery = query,
            before = current,
            after = clearedEvidence,
            outcome = "Succeeded"
        });
        current = clearedEvidence;
        cleared = true;
    }

    await WriteJsonAsync("action.json", new
    {
        schemaVersion = "1.0",
        selected.Serial,
        query,
        clearAfter,
        inputActions = audit.Count,
        actions = audit
    });
    await File.WriteAllTextAsync(
        Path.Combine(output, "result.txt"),
        $"SUCCESS query={query}; inputActions={audit.Count}; visualQuery=true; " +
        $"visibleResultCount=unknown; cleared={cleared}",
        cancellationToken);
    Console.WriteLine(
        $"Verified inventory search '{query}' with {audit.Count} bounded input actions; " +
        $"visible result count unavailable; cleared={cleared}.");
    return 0;

    async Task<InventorySearchVisualEvidence> WaitForSearchPostconditionAsync(
        InventorySearchAction action) => await WaitUntilAsync(value => action switch
        {
            InventorySearchAction.OpenSearch => value.KeyboardVisible && !value.QueryVisible,
            InventorySearchAction.ClearSearch => !value.QueryVisible,
            InventorySearchAction.EnterQuery => value.KeyboardVisible && value.QueryVisible,
            InventorySearchAction.SubmitQuery => !value.KeyboardVisible && value.QueryVisible,
            _ => false
        });

    async Task<InventorySearchVisualEvidence> WaitUntilAsync(
        Func<InventorySearchVisualEvidence, bool> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        InventorySearchVisualEvidence? latest = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var png = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
            latest = analyzer.Analyze(png);
            if (predicate(latest))
            {
                return latest;
            }
            await Task.Delay(300, cancellationToken);
        }
        throw new InvalidOperationException(
            $"Timed out waiting for verified inventory-search postcondition; latest={latest}.");
    }

    Task WriteJsonAsync(string name, object value) => File.WriteAllTextAsync(
        Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
}

static async Task<int> SetPokemonTagAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Path.GetFullPath(Require(options, "out"));
    var tagName = Require(options, "tag");
    var selectedState = ParseBoolean(options, "selected", false);
    var profilePath = Path.GetFullPath(Require(options, "profile"));
    var profile = await TagSelectorProfileLoader.LoadAsync(profilePath, cancellationToken);
    Directory.CreateDirectory(output);

    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var metadata = await transport.ReadMetadataAsync(selected.Serial, cancellationToken);
    if (!string.Equals(profile.DeviceSerial, selected.Serial, StringComparison.Ordinal) ||
        metadata.Screen.EffectiveWidth != profile.ScreenWidth ||
        metadata.Screen.EffectiveHeight != profile.ScreenHeight)
    {
        throw new InvalidOperationException(
            "Tag selector profile does not match the selected device and geometry.");
    }

    var detector = new PokemonGoGameStateDetector();
    var locator = new VisualControlLocator();
    var tagSelector = new TagSelector();
    var actions = new List<object>();
    var before = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
    var beforeState = detector.Detect(before);
    var detailsTagPillsBefore = tagSelector.CountDetailsTagPills(before);
    await File.WriteAllBytesAsync(Path.Combine(output, "before.png"), before, cancellationToken);
    await WriteJsonAsync("state-before.json", new
    {
        state = beforeState.State.ToString(),
        beforeState.Confidence,
        requestedTag = tagName,
        requestedSelected = selectedState
    });
    if (beforeState.State != PokemonGoGameState.PokemonDetails)
    {
        throw new InvalidOperationException(
            "PokemonDetails was not verified; no tag operation was sent.");
    }

    var menuControl = locator.LocateDetailsMenu(before) ??
        throw new InvalidOperationException("Details menu control was not visually verified.");
    var menuTarget = menuControl.Target.ToPixels(profile.ScreenWidth, profile.ScreenHeight);
    await transport.TapAsync(selected.Serial, menuTarget.X, menuTarget.Y, cancellationToken);
    actions.Add(ActionRecord("OpenPokemonMenu", menuTarget.X, menuTarget.Y,
        "PokemonDetails", "PokemonMenu"));
    _ = await WaitForAsync(png => detector.Detect(png).State == PokemonGoGameState.PokemonMenu,
        "PokemonMenu");

    var openTagTarget = profile.OpenTagMenu.ToPixels(profile.ScreenWidth, profile.ScreenHeight);
    await transport.TapAsync(selected.Serial, openTagTarget.X, openTagTarget.Y, cancellationToken);
    actions.Add(ActionRecord("OpenPokemonTagSelector", openTagTarget.X, openTagTarget.Y,
        "PokemonMenu", "TagSelector"));
    var selectorPng = await WaitForAsync(
        png => tagSelector.IsSelectorVisible(png, profile), "TagSelector");
    await File.WriteAllBytesAsync(
        Path.Combine(output, "selector-before.png"), selectorPng, cancellationToken);

    TagSelectionMatch? match = null;
    var scrolls = 0;
    while (scrolls <= profile.MaximumScrolls)
    {
        match = tagSelector.FindByName(selectorPng, tagName, profile, profilePath);
        if (match is not null)
        {
            break;
        }
        if (scrolls == profile.MaximumScrolls)
        {
            break;
        }
        var start = profile.ScrollStart.ToPixels(profile.ScreenWidth, profile.ScreenHeight);
        var end = profile.ScrollEnd.ToPixels(profile.ScreenWidth, profile.ScreenHeight);
        await transport.SwipeAsync(
            selected.Serial, start.X, start.Y, end.X, end.Y, 320, cancellationToken);
        scrolls++;
        actions.Add(new
        {
            sequence = actions.Count + 1,
            action = "ScrollTagSelector",
            target = new
            {
                startX = start.X,
                startY = start.Y,
                endX = end.X,
                endY = end.Y
            },
            expectedState = "TagSelector",
            maximumScrolls = profile.MaximumScrolls
        });
        await Task.Delay(700, cancellationToken);
        selectorPng = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
        if (!tagSelector.IsSelectorVisible(selectorPng, profile))
        {
            throw new InvalidOperationException("Tag selector disappeared after bounded scroll.");
        }
    }

    if (match is null)
    {
        await File.WriteAllBytesAsync(
            Path.Combine(output, "after.png"), selectorPng, cancellationToken);
        await WriteJsonAsync("action.json", new
        {
            schemaVersion = "1.0",
            requestedTag = tagName,
            requestedSelected = selectedState,
            scrolls,
            rowMutationActions = 0,
            actions,
            outcome = "TAG_NOT_FOUND_NO_MUTATION"
        });
        await File.WriteAllTextAsync(
            Path.Combine(output, "result.txt"),
            "TAG_NOT_FOUND_NO_MUTATION",
            cancellationToken);
        throw new InvalidOperationException(
            $"Tag '{tagName}' was not matched confidently; no tag row was tapped.");
    }

    var initialSelected = match.Row.IsSelected;
    var rowMutationActions = 0;
    if (initialSelected != selectedState)
    {
        var rowTarget = match.Row.Target.ToPixels(profile.ScreenWidth, profile.ScreenHeight);
        await transport.TapAsync(selected.Serial, rowTarget.X, rowTarget.Y, cancellationToken);
        rowMutationActions = 1;
        actions.Add(new
        {
            sequence = actions.Count + 1,
            action = "SetExistingPokemonTag",
            tagName,
            selected = selectedState,
            targetX = rowTarget.X,
            targetY = rowTarget.Y,
            match.Confidence,
            match.VisibleRowCount,
            rowIndexUsed = false,
            expectedState = "TagSelector"
        });
        selectorPng = await WaitForAsync(png =>
        {
            var updated = tagSelector.FindByName(png, tagName, profile, profilePath);
            return updated is not null && updated.Row.IsSelected == selectedState;
        }, "requested tag checkmark state");
        match = tagSelector.FindByName(selectorPng, tagName, profile, profilePath) ??
            throw new InvalidOperationException("Matched tag disappeared after its guarded tap.");
    }

    await File.WriteAllBytesAsync(
        Path.Combine(output, "selector-after.png"), selectorPng, cancellationToken);
    if (!tagSelector.IsDoneVisible(selectorPng, profile))
    {
        throw new InvalidOperationException("Tag selector Done control was not visually verified.");
    }
    var doneTarget = profile.Done.ToPixels(profile.ScreenWidth, profile.ScreenHeight);
    await transport.TapAsync(selected.Serial, doneTarget.X, doneTarget.Y, cancellationToken);
    actions.Add(ActionRecord("CommitPokemonTagSelection", doneTarget.X, doneTarget.Y,
        "TagSelector", "PokemonDetails"));
    var after = await WaitForAsync(
        png => detector.Detect(png).State == PokemonGoGameState.PokemonDetails,
        "PokemonDetails");
    var detailsTagPillsAfter = tagSelector.CountDetailsTagPills(after);
    var expectedPillCount = rowMutationActions == 0
        ? detailsTagPillsBefore
        : detailsTagPillsBefore + (selectedState ? 1 : -1);
    if (detailsTagPillsAfter != expectedPillCount)
    {
        await File.WriteAllBytesAsync(Path.Combine(output, "after.png"), after, cancellationToken);
        throw new InvalidOperationException(
            $"Details tag-pill count verification failed; expected {expectedPillCount}, " +
            $"observed {detailsTagPillsAfter}.");
    }

    await File.WriteAllBytesAsync(Path.Combine(output, "after.png"), after, cancellationToken);
    await WriteJsonAsync("state-after.json", new
    {
        state = detector.Detect(after).State.ToString(),
        requestedTag = tagName,
        requestedSelected = selectedState,
        initialSelected,
        selectorSelected = match.Row.IsSelected,
        detailsTagPillsBefore,
        detailsTagPillsAfter,
        match.Confidence,
        match.VisibleRowCount,
        scrolls,
        rowMutationActions
    });
    await WriteJsonAsync("action.json", new
    {
        schemaVersion = "1.0",
        selected.Serial,
        requestedTag = tagName,
        requestedSelected = selectedState,
        rowIndexUsed = false,
        fixedRowCoordinateUsed = false,
        rowMutationActions,
        actions,
        outcome = "SUCCEEDED"
    });
    await File.WriteAllTextAsync(
        Path.Combine(output, "result.txt"),
        $"SUCCEEDED tag={tagName}; selected={selectedState}; " +
        $"initialSelected={initialSelected}; rowMutationActions={rowMutationActions}; " +
        $"confidence={match.Confidence:F4}; scrolls={scrolls}",
        cancellationToken);
    Console.WriteLine(
        $"Verified tag '{tagName}' selected={selectedState}; initial={initialSelected}; " +
        $"row mutations={rowMutationActions}; match={match.Confidence:F3}; scrolls={scrolls}.");
    return 0;

    object ActionRecord(string action, int x, int y, string beforeStateName, string afterStateName) =>
        new
        {
            sequence = actions.Count + 1,
            action,
            targetX = x,
            targetY = y,
            stateBefore = beforeStateName,
            expectedState = afterStateName
        };

    async Task<byte[]> WaitForAsync(Func<byte[], bool> predicate, string expected)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        byte[]? latest = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            latest = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
            if (predicate(latest))
            {
                return latest;
            }
            await Task.Delay(300, cancellationToken);
        }
        if (latest is not null)
        {
            await File.WriteAllBytesAsync(Path.Combine(output, "failure.png"), latest, cancellationToken);
        }
        throw new InvalidOperationException($"Timed out waiting for {expected}.");
    }

    Task WriteJsonAsync(string name, object value) => File.WriteAllTextAsync(
        Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
}

static async Task<int> DetectTagSelectorImageAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var imagePath = Path.GetFullPath(Require(options, "image"));
    var profilePath = Path.GetFullPath(Require(options, "profile"));
    var tagName = Require(options, "tag");
    var outputPath = Path.GetFullPath(Require(options, "out"));
    var profile = await TagSelectorProfileLoader.LoadAsync(profilePath, cancellationToken);
    var png = await File.ReadAllBytesAsync(imagePath, cancellationToken);
    var selector = new TagSelector();
    var rows = selector.FindVisibleRows(png);
    var best = selector.FindBestByName(png, tagName, profile, profilePath);
    var candidates = selector.FindCandidatesByName(png, tagName, profile, profilePath);
    var accepted = selector.FindByName(png, tagName, profile, profilePath);
    var result = new
    {
        selectorVisible = selector.IsSelectorVisible(png, profile),
        detailsTagPill = selector.HasDetailsTagPill(png),
        detailsTagPillCount = selector.CountDetailsTagPills(png),
        visibleRowCount = rows.Count,
        rows,
        requestedTag = tagName,
        best,
        candidates,
        accepted = accepted is not null,
        minimumConfidence = profile.MinimumMatchConfidence,
        minimumMargin = profile.MinimumMatchMargin
    };
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(
        outputPath,
        JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);
    Console.WriteLine(
        $"Selector visible={result.selectorVisible}; rows={rows.Count}; " +
        $"best={best?.Confidence:F4}; accepted={result.accepted}.");
    return result.selectorVisible && accepted is not null ? 0 : 1;
}

static async Task<int> OpenAppraisalFromInventoryAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var output = Require(options, "out");
    var profile = await AutomationProfileLoader.LoadAsync(Require(options, "profile"), cancellationToken);
    var appraisalProfile = await AppraisalProfileLoader.LoadAsync(
        Require(options, "appraisal-profile"),
        cancellationToken);
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var metadata = await transport.ReadMetadataAsync(selected.Serial, cancellationToken);
    var width = metadata.Screen.EffectiveWidth ?? throw new InvalidOperationException("Screen width unavailable.");
    var height = metadata.Screen.EffectiveHeight ?? throw new InvalidOperationException("Screen height unavailable.");
    var snapshotService = new DeviceSnapshotService(transport, DeviceHarnessOptions.CurrentVersion);

    var locator = new VisualControlLocator();
    var inventory = await WaitForControlAsync(
        locator.LocateInventoryCard,
        "InventoryList");
    await snapshotService.CaptureAsync(Path.Combine(output, "01-inventory"), selected.Serial, cancellationToken);
    await TapControlAsync(inventory, "visible inventory card");

    var details = await WaitForControlAsync(
        locator.LocateDetailsMenu,
        "PokemonDetails");
    await snapshotService.CaptureAsync(Path.Combine(output, "02-details"), selected.Serial, cancellationToken);
    await TapControlAsync(details, "details menu");

    var appraise = await WaitForControlAsync(
        locator.LocateAppraiseMenuItem,
        "PokemonMenuOpen");
    await snapshotService.CaptureAsync(Path.Combine(output, "03-menu"), selected.Serial, cancellationToken);
    await TapControlAsync(appraise, "appraise");

    var appraisalRecovery = new GuardedInventoryRecovery();
    var appraisalFrames = new List<RecoveryFrame>();
    var introTapActions = 0;
    var introTapWasSent = false;
    // AppraisalIntro animation can consume most of the ordinary state timeout.
    // Keep the same bounded ROI consensus window used by the dedicated command.
    var deadline = DateTimeOffset.UtcNow.AddSeconds(
        Math.Max(12, profile.StateTimeoutSeconds));
    double appraisalConfidence = 0;
    var stableAppraisalBars = false;
    while (DateTimeOffset.UtcNow < deadline)
    {
        var screenshot = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
        var frame = appraisalRecovery.Observe(screenshot, appraisalProfile);
        appraisalFrames.Add(frame);
        var decision = GuardedInventoryRecovery.DecideAppraisalContinuation(
            appraisalFrames, introTapActions, introTapWasSent);
        if (decision is AppraisalContinuationOutcome.SUCCESS_ALREADY_ADVANCED or
            AppraisalContinuationOutcome.SUCCESS_TAPPED)
        {
            GuardedInventoryRecovery.TryGetStableFrame(appraisalFrames, out var stable);
            appraisalConfidence = stable?.Detection.Confidence ?? 0;
            stableAppraisalBars = true;
            break;
        }

        if (decision == AppraisalContinuationOutcome.TAP_INTRO_ONCE &&
            GuardedInventoryRecovery.TryGetStableFrame(appraisalFrames, out var stableIntro) &&
            stableIntro?.LocatorTarget is { } target)
        {
            var image = PngDecoder.Decode(stableIntro.Screenshot);
            var (x, y) = target.ToPixels(image.Width, image.Height);
            await transport.TapAsync(selected.Serial, x, y, cancellationToken);
            Console.WriteLine(
                $"Executed stable ROI-grounded appraisal intro continue at ({x},{y}), confidence {stableIntro.LocatorConfidence:F3}.");
            introTapActions++;
            introTapWasSent = true;
            appraisalFrames.Clear();
        }
        else if (decision == AppraisalContinuationOutcome.FAIL_CLOSED)
        {
            break;
        }
        await Task.Delay(Math.Max(250, profile.StatePollMilliseconds), cancellationToken);
    }
    if (!stableAppraisalBars || appraisalConfidence < 0.90)
    {
        throw new InvalidOperationException(
            "The GAME navigation skill did not reach semantically verified AppraisalOpen.");
    }

    await snapshotService.CaptureAsync(Path.Combine(output, "04-appraisal"), selected.Serial, cancellationToken);
    Console.WriteLine($"Verified Inventory→Details→Menu→Appraisal at semantic confidence {appraisalConfidence:F3}.");
    return 0;

    async Task<LocatedControl> WaitForControlAsync(
        Func<byte[], LocatedControl?> locate,
        string expectedState)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(profile.StateTimeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var current = await transport.CaptureScreenshotPngAsync(selected.Serial, cancellationToken);
            var control = locate(current);
            if (control is not null && control.Confidence >= 0.80)
            {
                return control;
            }
            await Task.Delay(Math.Max(250, profile.StatePollMilliseconds), cancellationToken);
        }

        throw new InvalidOperationException(
            $"Expected GAME state {expectedState} was not visually verified; skill stopped.");
    }

    async Task TapControlAsync(LocatedControl control, string name)
    {
        var (x, y) = control.Target.ToPixels(width, height);
        await transport.TapAsync(selected.Serial, x, y, cancellationToken);
        Console.WriteLine(
            $"Executed visually grounded {name} at ({x},{y}), confidence {control.Confidence:F3}.");
    }
}

static async Task<int> CaptureDeviceUiSnapshotAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var outputDirectory = Require(options, "out");
    var transport = CreateRealAndroidTransport(options);
    var snapshot = await new DeviceSnapshotService(
        transport,
        DeviceHarnessOptions.CurrentVersion,
        new ConsoleDeviceLog()).CaptureAsync(
            outputDirectory,
            Optional(options, "serial"),
            cancellationToken);

    var hierarchy = await transport.CaptureUiHierarchyAsync(
        snapshot.Device.Serial,
        cancellationToken);
    var fullOutputDirectory = Path.GetFullPath(outputDirectory);
    var hierarchyPath = Path.Combine(fullOutputDirectory, "ui-hierarchy.xml");
    var manifestPath = Path.Combine(fullOutputDirectory, "ui-snapshot.json");
    await File.WriteAllTextAsync(hierarchyPath, hierarchy, cancellationToken);

    var manifest = new
    {
        schemaVersion = "1.0",
        capturedAtUtc = DateTimeOffset.UtcNow,
        deviceSerial = snapshot.Device.Serial,
        model = snapshot.Metadata.Model,
        screenshot = Path.GetFileName(snapshot.ScreenshotPath),
        screenshotSha256 = snapshot.ScreenshotSha256,
        uiHierarchy = Path.GetFileName(hierarchyPath),
        uiHierarchySha256 = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(hierarchy))).ToLowerInvariant()
    };
    await File.WriteAllTextAsync(
        manifestPath,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
        cancellationToken);

    Console.WriteLine($"Read-only UI snapshot written to: {fullOutputDirectory}");
    Console.WriteLine($"Device: {snapshot.Device.Serial} ({snapshot.Metadata.Model ?? "unknown"})");
    Console.WriteLine($"Screenshot SHA-256: {snapshot.ScreenshotSha256}");
    return 0;
}

static async Task<int> LaunchPokemonGoAsync(
    string[] args,
    CancellationToken cancellationToken)
{
    var options = ParseOptions(args);
    var outputDirectory = Require(options, "out");
    var transport = CreateRealAndroidTransport(options);
    var devices = await transport.ListDevicesAsync(cancellationToken);
    var selected = DeviceSnapshotService.SelectDevice(devices, Optional(options, "serial"));
    var hierarchy = await transport.CaptureUiHierarchyAsync(selected.Serial, cancellationToken);
    var node = XDocument.Parse(hierarchy)
        .Descendants("node")
        .FirstOrDefault(item =>
            string.Equals((string?)item.Attribute("text"), "Pokémon GO", StringComparison.Ordinal) ||
            string.Equals((string?)item.Attribute("content-desc"), "Pokémon GO", StringComparison.Ordinal));
    if (node is null)
    {
        throw new InvalidOperationException(
            "Could not find the exact visible Pokémon GO launcher control; no action was taken.");
    }

    var bounds = (string?)node.Attribute("bounds") ??
        throw new InvalidOperationException("The Pokémon GO control had no bounds; no action was taken.");
    var match = System.Text.RegularExpressions.Regex.Match(
        bounds, "^\\[(\\d+),(\\d+)\\]\\[(\\d+),(\\d+)\\]$");
    if (!match.Success)
    {
        throw new InvalidOperationException("The Pokémon GO control bounds were invalid; no action was taken.");
    }

    var left = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    var top = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
    var right = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
    var bottom = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
    await transport.TapAsync(
        selected.Serial,
        left + ((right - left) / 2),
        top + 80,
        cancellationToken);

    Console.WriteLine("Clicked the exact visible Pokémon GO launcher control.");
    return await CaptureDeviceUiSnapshotAsync(args, cancellationToken);
}

static int? ParseOptionalPositiveInt(
    IReadOnlyDictionary<string, string> options,
    string key)
{
    if (!options.TryGetValue(key, out var raw))
    {
        return null;
    }
    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
        value <= 0)
    {
        throw new ArgumentException($"--{key} must be a positive integer.");
    }
    return value;
}

static bool ParseBoolean(
    IReadOnlyDictionary<string, string> options,
    string key,
    bool defaultValue)
{
    if (!options.TryGetValue(key, out var raw))
    {
        return defaultValue;
    }
    if (!bool.TryParse(raw, out var value))
    {
        throw new ArgumentException($"--{key} must be true or false.");
    }
    return value;
}

static int ParseNonNegativeInt(
    IReadOnlyDictionary<string, string> options,
    string key,
    int defaultValue)
{
    if (!options.TryGetValue(key, out var raw))
    {
        return defaultValue;
    }

    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ||
        value < 0)
    {
        throw new ArgumentException($"--{key} must be a non-negative integer.");
    }

    return value;
}


static double ParseUnitDouble(
    IReadOnlyDictionary<string, string> options,
    string key,
    double defaultValue)
{
    if (!options.TryGetValue(key, out var raw))
    {
        return defaultValue;
    }

    if (!double.TryParse(
            raw,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value) ||
        !double.IsFinite(value) ||
        value is < 0 or > 1)
    {
        throw new ArgumentException($"--{key} must be a number between 0 and 1.");
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

static int AutomationExitCode(AutomationErrorCode code) =>
    code switch
    {
        AutomationErrorCode.InvalidProfile => 60,
        AutomationErrorCode.DeviceMismatch => 61,
        AutomationErrorCode.GeometryMismatch => 62,
        AutomationErrorCode.InvalidStartingState => 63,
        AutomationErrorCode.StateTimeout => 64,
        AutomationErrorCode.UnsafeScreenState => 65,
        AutomationErrorCode.ResumeMismatch => 66,
        AutomationErrorCode.CheckpointCorrupt => 67,
        AutomationErrorCode.FileSystemFailure => 68,
        AutomationErrorCode.TransportFailure => 69,
        _ => 1
    };

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
        CalibrationErrorCode.InvalidCapturePlan => 47,
        CalibrationErrorCode.InvalidCaptureSession => 48,
        CalibrationErrorCode.CaptureGeometryMismatch => 49,
        CalibrationErrorCode.CaptureDeviceMismatch => 50,
        CalibrationErrorCode.CaptureNotFound => 51,
        CalibrationErrorCode.CaptureNotReviewable => 52,
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
    Console.WriteLine("  inventory-scan --profile <automation.json> --screen-profile <screen-profile.json> --out <directory>");
    Console.WriteLine("                 [--adb <adb.exe>] [--serial <serial>] [--max-items <n>]");
    Console.WriteLine("                 [--max-runtime-minutes <n>]");
    Console.WriteLine("                 [--observation-provider <none|fake|appraisal>]");
    Console.WriteLine("                 [--appraisal-profile <appraisal.json>]");
    Console.WriteLine("  real-scan-export --checkpoint <inventory-scan-checkpoint.json>");
    Console.WriteLine("                   --appraisal-profile <appraisal.json> --out <directory>");
    Console.WriteLine("                   --calibration-out <directory>");
    Console.WriteLine("                   [--expected-items <n> | --minimum-items <n>]");
    Console.WriteLine("                   [--requested-maximum-items <n>] [--generate-overlays <true|false>]");
    Console.WriteLine("                   [--copy-screenshots <true|false>] [--generate-checkpoint-evidence <true|false>]");
    Console.WriteLine("  device-stop-known-app --app calcy [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  device-open-inventory [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  device-press-back [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  device-close-inventory --adb <adb.exe> --out <directory> [--serial <serial>]");
    Console.WriteLine("  device-continue-appraisal-intro --adb <adb.exe> --appraisal-profile <profile.json> --out <directory>");
    Console.WriteLine("  device-detect-game-state --adb <adb.exe> --out <directory> [--appraisal-profile <profile.json>]");
    Console.WriteLine("  device-recover-inventory --adb <adb.exe> --out <directory> [--appraisal-profile <profile.json>]");
    Console.WriteLine("  device-open-main-menu [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  device-open-pokemon-inventory [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  device-search-inventory --query <query> --out <directory> [--clear-after <true|false>]");
    Console.WriteLine("  device-run-index-sequence --query <query> --item-limit <n> --out <directory>");
    Console.WriteLine("  device-run-cleanup-proof --species <query> --item-limit <6-20> --database <sqlite> --out <directory> --continue-on-partial");
    Console.WriteLine("                            [--adb <adb.exe>] [--serial <serial>] [--profile <automation.json>]");
    Console.WriteLine("                            [--appraisal-profile <appraisal.json>] [--resume] [--controlled-stop-after <n>]");
    Console.WriteLine("                            [--apply-index-tag --index-tag AI-Indexed]");
    Console.WriteLine("                            [--apply-classification-tag --classification-tag <AI-Keep|AI-Review>]");
    Console.WriteLine("  device-validate-navigation-safety --adb <adb.exe> --out <directory> [--serial <serial>] [--cycles <1|2|3>]");
    Console.WriteLine("  device-set-pokemon-tag --tag <name> --selected <true|false> --profile <tag-profile.json> --out <directory>");
    Console.WriteLine("  tag-selector-detect-image --image <screen.png> --profile <tag-profile.json> --tag <name> --out <result.json>");
    Console.WriteLine("  device-open-appraisal --profile <automation.json> --appraisal-profile <appraisal.json> --out <directory>");
    Console.WriteLine("                        [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  inventory-db-init [--db <pogo-inventory.db>]");
    Console.WriteLine("  inventory-db-summary [--db <pogo-inventory.db>]");
    Console.WriteLine("  inventory-scan --fake --profile <automation.json> --screen-profile <screen-profile.json>");
    Console.WriteLine("                 --out <directory> [--fixtures <directory>] [--max-items <n>]");
    Console.WriteLine("                 [--observation-provider <fake|none|appraisal>]");
    Console.WriteLine("                 [--appraisal-profile <appraisal.json>]");
    Console.WriteLine();
    Console.WriteLine("  profile-bootstrap --profile <automation.json> --anchors <anchor-plan.json> --out <directory>");
    Console.WriteLine("                    [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  profile-bootstrap --fake --profile <automation.json> --anchors <anchor-plan.json>");
    Console.WriteLine("                    --out <directory> [--fixtures <directory>]");
    Console.WriteLine();
    Console.WriteLine("  calcy-probe --out <directory> [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("              [--package <package-name>] [--max-log-lines <n>]");
    Console.WriteLine("  calcy-probe --fake --out <directory> [--fixtures <directory>]");
    Console.WriteLine("  calcy-live-check --profile <automation.json> --screen-profile <screen-profile.json>");
    Console.WriteLine("                   --out <directory> [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("                   [--parser-profile <parser.json>] [--settle-ms <n>]");
    Console.WriteLine("  calcy-live-check --fake --profile <automation.json> --screen-profile <screen-profile.json>");
    Console.WriteLine("                   --out <directory> [--screen-fixtures <dir>] [--probe-fixtures <dir>]");
    Console.WriteLine("                   [--parser-profile <parser.json>] [--settle-ms <n>]");
    Console.WriteLine("  calcy-parse --input <raw.txt> --profile <parser.json> --out <observation.json>");
    Console.WriteLine("              [--source-name <name>]");
    Console.WriteLine("  calcy-verification-init --out <local-directory> [--cases <n>]");
    Console.WriteLine("  calcy-verification-run --manifest <file> --evidence-root <dir> --out <dir>");
    Console.WriteLine("                         [--parser-profile <parser.json>]");
    Console.WriteLine("  calcy-provider-select --report <verification-report.json>");
    Console.WriteLine("                        --mechanism <PidWindowedLogcat|LocalText|VisualOverlay>");
    Console.WriteLine("                        --version <version> --out <selection.json>");
    Console.WriteLine("                        [--parser-profile <parser.json>]");
    Console.WriteLine();
    Console.WriteLine("  image-pretest --input <directory> --out <directory> [--min-images <n>] [--min-decode-rate <0-1>]");
    Console.WriteLine("  image-region-discovery --input <directory> --out <directory> [--min-images <n>] [--min-decode-rate <0-1>] [--cluster-threshold <0-1>] [--grid-columns <n>] [--grid-rows <n>]");
    Console.WriteLine("  image-crop-atlas --input <directory> --region-report <file> --out <directory>");
    Console.WriteLine("                   [--max-candidates <n>] [--representatives-per-cluster <n>]");
    Console.WriteLine("                   [--max-crop-width <n>] [--max-crop-height <n>]");
    Console.WriteLine("  image-semantic-evidence --input <directory> --region-report <file>");
    Console.WriteLine("                          --crop-atlas-report <file> --out <directory>");
    Console.WriteLine("                          [--min-cases <n>] [--min-cases-per-cluster <n>]");
    Console.WriteLine("  appraisal-pretest --input <directory> --profile <file> --out <directory>");
    Console.WriteLine("                    [--region-report <file>] [--min-images <n>] [--min-candidates <n>]");
    Console.WriteLine("  phone-prepare --profile <file> --out <directory> [--serial <id>] [--adb <path>]");
    Console.WriteLine("                [--near-duplicate-threshold <0..1>] [--cluster-threshold <0..1>]");
    Console.WriteLine();
    Console.WriteLine("  device-snapshot --out <directory> [--adb <adb.exe>] [--serial <serial>] [--timeout-seconds <n>]");
    Console.WriteLine("  device-snapshot --fake --out <directory>");
    Console.WriteLine("  device-ui-snapshot --out <directory> [--adb <adb.exe>] [--serial <serial>] [--timeout-seconds <n>]");
    Console.WriteLine("  device-launch-pokemon-go --out <directory> [--adb <adb.exe>] [--serial <serial>] [--timeout-seconds <n>]");
    Console.WriteLine();
    Console.WriteLine("  screen-detect --image <screen.png> --profile <profile.json> --out <evidence.json>");
    Console.WriteLine("  screen-fingerprint --image <screen.png> --region <x,y,w,h> --out <fingerprint.json>");
    Console.WriteLine("  identity-fingerprint --images <a.png;b.png;c.png> --out <identity.json>");
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
    Console.WriteLine("  calibration-capture --workspace <private-local-directory> --state <ScreenState>");
    Console.WriteLine("                      [--adb <adb.exe>] [--serial <serial>] [--notes <text>]");
    Console.WriteLine("  calibration-capture-session --workspace <private-local-directory>");
    Console.WriteLine("                              [--adb <adb.exe>] [--serial <serial>]");
    Console.WriteLine("  calibration-capture-status --workspace <private-local-directory>");
    Console.WriteLine("  calibration-capture-approve --workspace <private-local-directory> --id <capture-id>");
    Console.WriteLine("                              --reviewed-by <name> --confirm-private-review");
    Console.WriteLine();
    Console.WriteLine("Inventory scan uses only the allow-listed taps and swipe from the automation profile. It never transfers, powers up, evolves, purifies, catches or changes location.");
}
