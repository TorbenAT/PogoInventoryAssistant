using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.Core.Reporting;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Transport;

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
            _ => UnknownCommand(args[0])
        };
    }
    catch (DeviceHarnessException exception)
    {
        Console.Error.WriteLine($"[{exception.Code}] {exception.Message}");
        return DeviceExitCode(exception.Code);
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
    Console.WriteLine("The device-snapshot command is read-only. It can list devices, read metadata and capture one screenshot.");
    Console.WriteLine("It contains no tap, swipe, text input or game-changing action.");
}
