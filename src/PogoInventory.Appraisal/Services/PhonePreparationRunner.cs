using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Device;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Transport;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Appraisal.Services;

public sealed class PhonePreparationRunner
{
    private readonly IAndroidDeviceTransport _transport;
    private readonly IDeviceLog _log;

    public PhonePreparationRunner(
        IAndroidDeviceTransport transport,
        IDeviceLog? log = null)
    {
        _transport = transport ??
            throw new ArgumentNullException(nameof(transport));
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<PhonePreparationReport> RunAsync(
        string outputDirectory,
        AppraisalVisualProfile baseProfile,
        string? requestedSerial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(baseProfile);
        baseProfile.Validate();

        var root = Path.GetFullPath(outputDirectory);
        var snapshotRoot = Path.Combine(root, "snapshot");
        var snapshotService = new DeviceSnapshotService(
            _transport,
            DeviceHarnessOptions.CurrentVersion,
            _log);
        var snapshot = await snapshotService.CaptureAsync(
            snapshotRoot,
            requestedSerial,
            cancellationToken);

        var screenshotBytes = await File.ReadAllBytesAsync(
            snapshot.ScreenshotPath,
            cancellationToken);
        var image = PngDecoder.Decode(screenshotBytes);
        var analysis = new AppraisalAnalyzer().Analyze(
            image,
            baseProfile);

        string? generatedProfileFile = null;
        if (analysis.IsAppraisal)
        {
            var generated = AppraisalProfileFitter.CreateDeviceProfile(
                baseProfile,
                analysis,
                snapshot.ScreenshotSha256,
                image.Width,
                image.Height);
            generatedProfileFile =
                "appraisal-profile.device.generated.json";
            await AppraisalProfileLoader.WriteAsync(
                generated,
                Path.Combine(root, generatedProfileFile),
                cancellationToken);

            var overlay = AppraisalImageDiagnostics.DrawOverlay(
                image,
                analysis);
            await File.WriteAllBytesAsync(
                Path.Combine(root, "appraisal-overlay.png"),
                PngEncoder.Encode(overlay),
                cancellationToken);
        }

        var aspectRatio = image.Width / (double)image.Height;
        var aspectDifference = Math.Abs(
            aspectRatio - baseProfile.ReferenceAspectRatio) /
            baseProfile.ReferenceAspectRatio;
        var actions = new List<string>();

        if (!analysis.IsAppraisal)
        {
            actions.Add(
                "Open Pokémon GO manually on a Pokémon appraisal screen and run phone-prepare again.");
        }
        else
        {
            actions.Add(
                "Keep the generated device profile local and rerun phone-prepare on at least three different appraisal screens.");
            actions.Add(
                "Do not enable Complete IV observations until a twenty-case verification report locks the generated profile hash.");
        }

        actions.Add(
            "Run calcy-probe and calcy-live-check before selecting any production observation provider.");
        actions.Add(
            "Automatic inventory navigation remains disabled by this readiness command.");

        var report = new PhonePreparationReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Device = snapshot.Metadata,
            ScreenshotFile = Path.GetRelativePath(
                    root,
                    snapshot.ScreenshotPath)
                .Replace(Path.DirectorySeparatorChar, '/'),
            ScreenshotSha256 = snapshot.ScreenshotSha256,
            ScreenshotWidth = image.Width,
            ScreenshotHeight = image.Height,
            Portrait = image.Height > image.Width,
            BaseProfileId = baseProfile.ProfileId,
            ScreenshotAspectRatio = aspectRatio,
            ReferenceAspectRatio = baseProfile.ReferenceAspectRatio,
            AspectRatioDifference = aspectDifference,
            Appraisal = analysis,
            GeneratedProfileFile = generatedProfileFile,
            AdbReady = true,
            PassiveCaptureReady = image.Height > image.Width,
            AppraisalCalibrationReady = analysis.IsAppraisal,
            VerifiedIvExtractionReady = analysis.IsComplete,
            AutomaticNavigationReady = false,
            NextActions = actions
        };
        report.Validate();

        await File.WriteAllTextAsync(
            Path.Combine(root, "phone-readiness.json"),
            JsonSerializer.Serialize(
                report,
                AppraisalJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "phone-readiness.md"),
            PhonePreparationReportWriter.Markdown(report),
            cancellationToken);

        return report;
    }
}
