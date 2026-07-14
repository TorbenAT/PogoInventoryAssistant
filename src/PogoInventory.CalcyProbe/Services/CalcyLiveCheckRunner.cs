using System.Text.Json;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.CalcyProbe.Models;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Transport;
using PogoInventory.Observations.Models;
using PogoInventory.Observations.Parsing;
using PogoInventory.Observations.Providers;
using PogoInventory.Vision.Models;

namespace PogoInventory.CalcyProbe.Services;

public sealed class CalcyLiveCheckRunner
{
    private readonly IAndroidAutomationTransport _automationTransport;
    private readonly IAndroidAppInspectionTransport _inspectionTransport;
    private readonly IDeviceLog _log;

    public CalcyLiveCheckRunner(
        IAndroidAutomationTransport automationTransport,
        IAndroidAppInspectionTransport inspectionTransport,
        IDeviceLog? log = null)
    {
        _automationTransport = automationTransport ??
            throw new ArgumentNullException(nameof(automationTransport));
        _inspectionTransport = inspectionTransport ??
            throw new ArgumentNullException(nameof(inspectionTransport));
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<CalcyLiveCheckResult> RunAsync(
        string outputDirectory,
        InventoryAutomationProfile automationProfile,
        ScreenDetectionProfile screenProfile,
        CalcyProbeOptions probeOptions,
        CalcyTextParserProfile? parserProfile = null,
        string? requestedSerial = null,
        TimeSpan? settleTime = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(automationProfile);
        ArgumentNullException.ThrowIfNull(screenProfile);
        ArgumentNullException.ThrowIfNull(probeOptions);

        var fullOutput = Path.GetFullPath(outputDirectory);
        var navigationDirectory = Path.Combine(fullOutput, "navigation");
        var probeDirectory = Path.Combine(fullOutput, "probe");
        var existingCheckpoint = Path.Combine(
            navigationDirectory,
            "inventory-scan-checkpoint.json");
        if (File.Exists(existingCheckpoint))
        {
            throw new InvalidOperationException(
                "Calcy live check requires a fresh output directory so that navigation cannot be skipped by an old completed checkpoint.");
        }

        var navigation = await new InventoryAutomationRunner(
            _automationTransport,
            log: _log,
            observationProvider: new UnavailableCalcyObservationProvider()).RunAsync(
                navigationDirectory,
                automationProfile,
                screenProfile,
                requestedSerial,
                maximumItems: 1,
                cancellationToken: cancellationToken);

        if (navigation.Checkpoint.Status != AutomationRunStatus.Completed ||
            navigation.Checkpoint.Items.Count != 1)
        {
            throw new InvalidOperationException(
                "Calcy live check could not reach and capture exactly one appraisal screen.");
        }

        var delay = settleTime ?? TimeSpan.FromSeconds(2);
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(
                nameof(settleTime),
                "Calcy live-check settle time must be between zero and 30 seconds.");
        }

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        var probe = await new CalcyProbeRunner(_inspectionTransport, _log).RunAsync(
            probeDirectory,
            probeOptions,
            requestedSerial,
            cancellationToken);

        CalcyObservation? parsed = null;
        string? parsedPath = null;
        if (parserProfile is not null)
        {
            var rawPath = probe.EvidenceFiles["logcat-filtered.txt"];
            var raw = await File.ReadAllTextAsync(rawPath, cancellationToken);
            parsed = new CalcyRawTextParser().Parse(
                parserProfile,
                new CalcyRawOutputBundle
                {
                    Sources = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["logcat"] = raw
                    }
                },
                "CalcyLiveCheck");
            parsedPath = Path.Combine(fullOutput, "parsed-observation.json");
            Directory.CreateDirectory(fullOutput);
            await File.WriteAllTextAsync(
                parsedPath,
                JsonSerializer.Serialize(
                    parsed,
                    CalcyTextParserProfileLoader.CreateJsonOptions(writeIndented: true)),
                cancellationToken);
        }

        return new CalcyLiveCheckResult
        {
            Navigation = navigation,
            Probe = probe,
            ParsedObservation = parsed,
            ParsedObservationPath = parsedPath
        };
    }
}
