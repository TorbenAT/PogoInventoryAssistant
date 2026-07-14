using System.Security.Cryptography;
using System.Text;
using PogoInventory.CalcyProbe.Models;
using PogoInventory.CalcyProbe.Reporting;
using PogoInventory.Device;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Transport;

namespace PogoInventory.CalcyProbe.Services;

public sealed class CalcyProbeRunner
{
    public const string CurrentVersion = "0.14.0";

    private readonly IAndroidAppInspectionTransport _transport;
    private readonly IDeviceLog _log;

    public CalcyProbeRunner(
        IAndroidAppInspectionTransport transport,
        IDeviceLog? log = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<CalcyProbeResult> RunAsync(
        string outputDirectory,
        CalcyProbeOptions options,
        string? requestedSerial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var devices = await _transport.ListDevicesAsync(cancellationToken);
        var selected = DeviceSnapshotService.SelectDevice(devices, requestedSerial);
        var metadata = await _transport.ReadMetadataAsync(selected.Serial, cancellationToken);
        if (!string.Equals(metadata.Serial, selected.Serial, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Inspection metadata serial '{metadata.Serial}' did not match selected device '{selected.Serial}'.");
        }

        var packageDump = await _transport.ReadPackageDumpAsync(
            selected.Serial,
            options.PackageName,
            cancellationToken);
        var packagePath = await _transport.ReadPackagePathAsync(
            selected.Serial,
            options.PackageName,
            cancellationToken);
        var package = CalcyPackageDumpParser.Parse(
            options.PackageName,
            packageDump,
            packagePath);

        var screenshot = await _transport.CaptureScreenshotPngAsync(
            selected.Serial,
            cancellationToken);

        var processId = string.Empty;
        var logcat = string.Empty;
        var accessibility = string.Empty;
        var appOps = string.Empty;
        var activityServices = string.Empty;
        if (package.IsInstalled)
        {
            processId = await _transport.ReadProcessIdAsync(
                selected.Serial,
                options.PackageName,
                cancellationToken);
            logcat = await _transport.ReadRecentLogcatAsync(
                selected.Serial,
                options.MaximumLogcatLines,
                cancellationToken);
            accessibility = await _transport.ReadAccessibilityStateAsync(
                selected.Serial,
                cancellationToken);
            appOps = await _transport.ReadAppOpsAsync(
                selected.Serial,
                options.PackageName,
                cancellationToken);
            activityServices = await _transport.ReadActivityServicesAsync(
                selected.Serial,
                options.PackageName,
                cancellationToken);
        }

        var filteredLogLines = CalcyLogcatFilter.Filter(
            logcat,
            options.PackageName,
            processId);
        var filteredLogcat = string.Join(Environment.NewLine, filteredLogLines);

        var textEvidence = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["package-dump.txt"] = packageDump,
            ["package-path.txt"] = packagePath,
            ["process-id.txt"] = processId,
            ["logcat-full.txt"] = logcat,
            ["logcat-filtered.txt"] = filteredLogcat,
            ["accessibility.txt"] = accessibility,
            ["appops.txt"] = appOps,
            ["activity-services.txt"] = activityServices
        };

        var hashes = textEvidence.ToDictionary(
            pair => pair.Key,
            pair => ProbeHash.Sha256(pair.Value),
            StringComparer.Ordinal);
        hashes["screen.png"] = Convert.ToHexString(SHA256.HashData(screenshot))
            .ToLowerInvariant();

        var capabilities = BuildCapabilities(
            package,
            processId,
            filteredLogLines,
            accessibility,
            appOps,
            activityServices);
        var decision = !package.IsInstalled
            ? CalcyProbeDecision.PackageMissing
            : capabilities.Any(capability =>
                (capability.Status == CalcyProbeCapabilityStatus.Proven ||
                 capability.Status == CalcyProbeCapabilityStatus.Candidate) &&
                (capability.Name == "Filtered log evidence" ||
                 capability.Name == "Accessibility state"))
                ? CalcyProbeDecision.CandidateEvidenceFound
                : CalcyProbeDecision.InstalledNeedsLiveEvidence;

        var warnings = BuildWarnings(package, processId, filteredLogLines);
        var report = new CalcyProbeReport
        {
            ProbeVersion = CurrentVersion,
            PackageName = options.PackageName,
            Device = metadata,
            Package = package,
            ProcessId = string.IsNullOrWhiteSpace(processId) ? null : processId.Trim(),
            Decision = decision,
            Capabilities = capabilities,
            FilteredLogLineCount = filteredLogLines.Count,
            Warnings = warnings,
            EvidenceSha256 = hashes
        };

        var fullOutput = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(fullOutput);
        var evidenceDirectory = Path.Combine(fullOutput, "evidence");
        Directory.CreateDirectory(evidenceDirectory);

        var evidenceFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in textEvidence)
        {
            var path = Path.Combine(evidenceDirectory, pair.Key);
            await WriteTextAtomicAsync(path, pair.Value, cancellationToken);
            evidenceFiles[pair.Key] = path;
        }

        var screenshotPath = Path.Combine(evidenceDirectory, "screen.png");
        await WriteBytesAtomicAsync(screenshotPath, screenshot, cancellationToken);
        evidenceFiles["screen.png"] = screenshotPath;

        var reportPaths = await CalcyProbeReportWriter.WriteAsync(
            report,
            fullOutput,
            cancellationToken);

        _log.Write(
            DeviceLogLevel.Information,
            "calcy.probe.complete",
            "Calcy inspection probe completed.",
            new Dictionary<string, string>
            {
                ["serial"] = selected.Serial,
                ["package"] = options.PackageName,
                ["installed"] = package.IsInstalled.ToString(),
                ["decision"] = decision.ToString(),
                ["filteredLogLines"] = filteredLogLines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            });

        return new CalcyProbeResult
        {
            Report = report,
            OutputDirectory = fullOutput,
            JsonReportPath = reportPaths.JsonPath,
            MarkdownReportPath = reportPaths.MarkdownPath,
            EvidenceFiles = evidenceFiles
        };
    }

    private static IReadOnlyList<CalcyProbeCapability> BuildCapabilities(
        CalcyPackageMetadata package,
        string processId,
        IReadOnlyList<string> filteredLogLines,
        string accessibility,
        string appOps,
        string activityServices)
    {
        if (!package.IsInstalled)
        {
            return new[]
            {
                new CalcyProbeCapability
                {
                    Name = "Package installation",
                    Status = CalcyProbeCapabilityStatus.Unavailable,
                    Detail = "The configured Calcy package was not found on the selected device."
                }
            };
        }

        var overlayPermission = package.RequestedPermissions.Contains(
            "android.permission.SYSTEM_ALERT_WINDOW",
            StringComparer.Ordinal);
        var overlayAllowed = appOps.Contains("SYSTEM_ALERT_WINDOW", StringComparison.OrdinalIgnoreCase) &&
            appOps.Contains("allow", StringComparison.OrdinalIgnoreCase);
        var accessibilityEnabled = accessibility.Contains(
            package.PackageName,
            StringComparison.OrdinalIgnoreCase);
        var declaredAccessibilityService = package.Services.Any(service =>
            service.Contains("access", StringComparison.OrdinalIgnoreCase) ||
            service.Contains("scan", StringComparison.OrdinalIgnoreCase));
        var runningService = activityServices.Contains(
            package.PackageName,
            StringComparison.OrdinalIgnoreCase);

        return new[]
        {
            new CalcyProbeCapability
            {
                Name = "Package installation",
                Status = CalcyProbeCapabilityStatus.Proven,
                Detail = $"Calcy package {package.PackageName} is installed as version {package.VersionName ?? "unknown"}."
            },
            new CalcyProbeCapability
            {
                Name = "Running process",
                Status = string.IsNullOrWhiteSpace(processId)
                    ? CalcyProbeCapabilityStatus.NotObserved
                    : CalcyProbeCapabilityStatus.Proven,
                Detail = string.IsNullOrWhiteSpace(processId)
                    ? "No current Calcy process id was reported."
                    : $"A current Calcy process id was reported: {processId.Trim()}."
            },
            new CalcyProbeCapability
            {
                Name = "Overlay permission",
                Status = overlayAllowed
                    ? CalcyProbeCapabilityStatus.Proven
                    : overlayPermission
                        ? CalcyProbeCapabilityStatus.Candidate
                        : CalcyProbeCapabilityStatus.NotObserved,
                Detail = overlayAllowed
                    ? "Android app-ops reports the overlay operation as allowed."
                    : overlayPermission
                        ? "The package requests overlay permission, but the probe did not prove that it is allowed."
                        : "No overlay permission evidence was found in the package dump."
            },
            new CalcyProbeCapability
            {
                Name = "Accessibility state",
                Status = accessibilityEnabled
                    ? CalcyProbeCapabilityStatus.Proven
                    : declaredAccessibilityService
                        ? CalcyProbeCapabilityStatus.Candidate
                        : CalcyProbeCapabilityStatus.NotObserved,
                Detail = accessibilityEnabled
                    ? "The package name appears in the current Android accessibility state."
                    : declaredAccessibilityService
                        ? "A possible scan or accessibility service is declared, but it is not shown as enabled."
                        : "No Calcy accessibility service was observed."
            },
            new CalcyProbeCapability
            {
                Name = "Running service",
                Status = runningService
                    ? CalcyProbeCapabilityStatus.Proven
                    : CalcyProbeCapabilityStatus.NotObserved,
                Detail = runningService
                    ? "Android activity-services output contains the Calcy package."
                    : "No running Calcy service was observed."
            },
            new CalcyProbeCapability
            {
                Name = "Filtered log evidence",
                Status = filteredLogLines.Count > 0
                    ? CalcyProbeCapabilityStatus.Candidate
                    : CalcyProbeCapabilityStatus.NotObserved,
                Detail = filteredLogLines.Count > 0
                    ? $"The local probe found {filteredLogLines.Count} recent log lines related to Calcy or its process."
                    : "No recent Calcy-related log lines were found. This does not prove that log output is unsupported.",
                Evidence = filteredLogLines.Take(5).ToArray()
            },
            new CalcyProbeCapability
            {
                Name = "Clipboard output",
                Status = CalcyProbeCapabilityStatus.NotObserved,
                Detail = "Clipboard reading is intentionally not implemented until a real-device mechanism is proven and reviewed."
            }
        };
    }

    private static IReadOnlyList<string> BuildWarnings(
        CalcyPackageMetadata package,
        string processId,
        IReadOnlyList<string> filteredLogLines)
    {
        var warnings = new List<string>();
        if (!package.IsInstalled)
        {
            warnings.Add("Install or enable Calcy IV before attempting live observation extraction.");
            return warnings;
        }

        if (string.IsNullOrWhiteSpace(processId))
        {
            warnings.Add("Calcy was not running during the probe. Run the probe again while Calcy and Pokémon GO are active.");
        }

        if (filteredLogLines.Count == 0)
        {
            warnings.Add("No parseable provider output has been proven. Do not select a real Calcy observation provider yet.");
        }

        warnings.Add("This probe inspects only documented Android/ADB surfaces and does not call a hidden game API.");
        return warnings;
    }

    private static async Task WriteTextAtomicAsync(
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllTextAsync(temporary, content ?? string.Empty, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            DeleteIfExists(temporary);
        }
    }

    private static async Task WriteBytesAtomicAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(temporary, content, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            DeleteIfExists(temporary);
        }
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
