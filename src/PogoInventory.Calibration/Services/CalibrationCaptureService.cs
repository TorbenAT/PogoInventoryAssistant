using System.Security.Cryptography;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Workspace;
using PogoInventory.Device;
using PogoInventory.Device.Logging;
using PogoInventory.Device.Transport;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Services;

public sealed class CalibrationCaptureService
{
    private readonly IAndroidDeviceTransport _transport;
    private readonly IDeviceLog _log;

    public CalibrationCaptureService(
        IAndroidDeviceTransport transport,
        IDeviceLog? log = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _log = log ?? NullDeviceLog.Instance;
    }

    public async Task<CalibrationCaptureResult> CaptureAsync(
        CalibrationWorkspace workspace,
        CalibrationCapturePlan plan,
        ScreenState expectedState,
        string? requestedSerial = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        CalibrationCapturePlanLoader.Validate(plan);

        if (!plan.Requirements.Any(x => x.State == expectedState))
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidCapturePlan,
                $"State '{expectedState}' is not part of capture plan '{plan.Name}'.");
        }

        var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
            workspace.CaptureSessionPath,
            plan,
            cancellationToken);
        await VerifyExistingCaptureFilesAsync(workspace, session, cancellationToken);

        var devices = await _transport.ListDevicesAsync(cancellationToken);
        var selectedDevice = DeviceSnapshotService.SelectDevice(devices, requestedSerial);
        var metadata = await _transport.ReadMetadataAsync(selectedDevice.Serial, cancellationToken);
        if (!string.Equals(metadata.Serial, selectedDevice.Serial, StringComparison.Ordinal))
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureDeviceMismatch,
                $"Device metadata serial '{metadata.Serial}' did not match selected serial " +
                $"'{selectedDevice.Serial}'.");
        }

        EnforceDeviceLock(plan, session, selectedDevice.Serial);

        var png = await _transport.CaptureScreenshotPngAsync(
            selectedDevice.Serial,
            cancellationToken);
        var image = PngDecoder.Decode(png);
        var orientation = GetOrientation(image.Width, image.Height);
        EnforceGeometry(plan, session, image.Width, image.Height, orientation);

        var hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        var existingByHash = session.Captures.FirstOrDefault(x =>
            x.Sha256.Equals(hash, StringComparison.OrdinalIgnoreCase));

        if (existingByHash is not null && existingByHash.ExpectedState != expectedState)
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureNotReviewable,
                $"The screenshot is pixel-identical to capture '{existingByHash.Id}', which is labelled " +
                $"'{existingByHash.ExpectedState}', not '{expectedState}'. Resolve the expected state before continuing.");
        }

        var now = DateTimeOffset.UtcNow;
        var sequence = session.Captures.Count == 0
            ? 1
            : session.Captures.Max(x => x.SequenceNumber) + 1;
        var id = CreateCaptureId(expectedState, sequence, now);
        var fileName = $"{id}.png";
        var relativePath = Portable(Path.Combine("incoming", expectedState.ToString(), fileName));
        var absolutePath = CalibrationPathSafety.ResolveInsideRoot(
            workspace.RootPath,
            relativePath,
            $"capture '{id}'");

        var capture = new CalibrationCaptureRecord
        {
            Id = id,
            SequenceNumber = sequence,
            ExpectedState = expectedState,
            RelativePath = relativePath,
            Sha256 = hash,
            CapturedAtUtc = now,
            DeviceSerial = selectedDevice.Serial,
            DeviceModel = metadata.Model ?? selectedDevice.Model,
            AndroidVersion = metadata.AndroidVersion,
            ImageWidth = image.Width,
            ImageHeight = image.Height,
            Orientation = orientation,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            DuplicateOfCaptureId = existingByHash?.Id
        };

        var updatedSession = session with
        {
            UpdatedAtUtc = now,
            LockedDeviceSerial = session.LockedDeviceSerial ?? selectedDevice.Serial,
            LockedDeviceModel = session.LockedDeviceModel ?? metadata.Model ?? selectedDevice.Model,
            LockedImageWidth = session.LockedImageWidth ?? image.Width,
            LockedImageHeight = session.LockedImageHeight ?? image.Height,
            Captures = session.Captures.Concat(new[] { capture }).ToArray()
        };

        try
        {
            await AtomicFile.WriteBytesAsync(absolutePath, png, cancellationToken);
            await CalibrationCaptureSessionRepository.SaveAsync(
                workspace.CaptureSessionPath,
                updatedSession,
                plan,
                cancellationToken);
        }
        catch
        {
            TryDelete(absolutePath);
            throw;
        }

        var status = CalibrationCaptureStatusBuilder.Build(plan, updatedSession);

        _log.Write(
            DeviceLogLevel.Information,
            "calibration.capture.complete",
            "Private read-only calibration screenshot captured.",
            new Dictionary<string, string>
            {
                ["captureId"] = capture.Id,
                ["state"] = capture.ExpectedState.ToString(),
                ["serial"] = selectedDevice.Serial,
                ["sha256"] = capture.Sha256,
                ["duplicate"] = capture.IsDuplicate.ToString()
            });

        return new CalibrationCaptureResult
        {
            Capture = capture,
            Status = status,
            AbsoluteImagePath = absolutePath,
            SessionPath = workspace.CaptureSessionPath
        };
    }

    public static async Task VerifyExistingCaptureFilesAsync(
        CalibrationWorkspace workspace,
        CalibrationCaptureSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(session);

        foreach (var capture in session.Captures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = CalibrationPathSafety.ResolveInsideRoot(
                workspace.RootPath,
                capture.RelativePath,
                $"capture '{capture.Id}'");
            if (!File.Exists(path))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.CaptureNotFound,
                    $"Capture file for '{capture.Id}' is missing: '{capture.RelativePath}'.");
            }

            var actualHash = await CalibrationHash.Sha256Async(path, cancellationToken);
            if (!actualHash.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FixtureHashMismatch,
                    $"Capture '{capture.Id}' changed after it was recorded. Expected " +
                    $"{capture.Sha256}, received {actualHash}.");
            }
        }

        var promoted = session.Captures.Where(x => x.IsPromoted).ToArray();
        if (promoted.Length == 0)
        {
            return;
        }

        var manifest = await FixtureManifestLoader.LoadAsync(
            workspace.ManifestPath,
            cancellationToken);
        foreach (var capture in promoted)
        {
            var fixture = manifest.Fixtures.FirstOrDefault(x =>
                string.Equals(
                    x.Id,
                    capture.PromotedFixtureId,
                    StringComparison.OrdinalIgnoreCase));
            if (fixture is null || fixture.ExpectedState != capture.ExpectedState)
            {
                throw new CalibrationException(
                    CalibrationErrorCode.InvalidCaptureSession,
                    $"Promoted fixture link for capture '{capture.Id}' is missing or has the wrong state.");
            }

            if (!fixture.Sha256.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FixtureHashMismatch,
                    $"Promoted fixture '{fixture.Id}' does not match capture '{capture.Id}'.");
            }

            var fixturePath = CalibrationPathSafety.ResolveInsideRoot(
                workspace.FixturesPath,
                fixture.RelativePath,
                $"fixture '{fixture.Id}'");
            if (!File.Exists(fixturePath))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FixtureMissing,
                    $"Promoted fixture file for capture '{capture.Id}' is missing.");
            }

            var fixtureHash = await CalibrationHash.Sha256Async(
                fixturePath,
                cancellationToken);
            if (!fixtureHash.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FixtureHashMismatch,
                    $"Promoted fixture file '{fixture.RelativePath}' changed after approval.");
            }
        }
    }

    private static void EnforceDeviceLock(
        CalibrationCapturePlan plan,
        CalibrationCaptureSession session,
        string serial)
    {
        if (plan.LockDeviceSerial &&
            !string.IsNullOrWhiteSpace(session.LockedDeviceSerial) &&
            !string.Equals(session.LockedDeviceSerial, serial, StringComparison.Ordinal))
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureDeviceMismatch,
                $"Capture session is locked to device '{session.LockedDeviceSerial}', not '{serial}'.");
        }
    }

    private static void EnforceGeometry(
        CalibrationCapturePlan plan,
        CalibrationCaptureSession session,
        int width,
        int height,
        ScreenOrientation orientation)
    {
        if (orientation != plan.RequiredOrientation)
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureGeometryMismatch,
                $"Capture orientation was '{orientation}', but plan requires '{plan.RequiredOrientation}'.");
        }

        if (width < plan.MinimumWidth || height < plan.MinimumHeight)
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureGeometryMismatch,
                $"Capture geometry {width}x{height} is below the minimum " +
                $"{plan.MinimumWidth}x{plan.MinimumHeight}.");
        }

        if (plan.LockExactGeometry &&
            session.LockedImageWidth is { } lockedWidth &&
            session.LockedImageHeight is { } lockedHeight &&
            (width != lockedWidth || height != lockedHeight))
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureGeometryMismatch,
                $"Capture session is locked to {lockedWidth}x{lockedHeight}, not {width}x{height}. " +
                "Restore the original phone resolution, display scaling and orientation.");
        }
    }

    private static ScreenOrientation GetOrientation(int width, int height) =>
        height > width
            ? ScreenOrientation.Portrait
            : width > height
                ? ScreenOrientation.Landscape
                : ScreenOrientation.Square;

    private static string CreateCaptureId(
        ScreenState state,
        int sequence,
        DateTimeOffset timestamp) =>
        $"{sequence:D4}-{state.ToString().ToLowerInvariant()}-{timestamp:yyyyMMdd-HHmmssfff}";

    private static string Portable(string path) => path.Replace('\\', '/');

    private static void TryDelete(string path)
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
