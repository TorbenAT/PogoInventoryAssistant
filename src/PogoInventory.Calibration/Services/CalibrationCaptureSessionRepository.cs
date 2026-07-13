using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;

namespace PogoInventory.Calibration.Services;

public static class CalibrationCaptureSessionRepository
{
    public static async Task<CalibrationCaptureSession> LoadOrCreateAsync(
        string path,
        CalibrationCapturePlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(plan);

        if (!File.Exists(path))
        {
            var now = DateTimeOffset.UtcNow;
            return new CalibrationCaptureSession
            {
                Id = $"capture-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..32],
                PlanName = plan.Name,
                PlanSha256 = CalibrationCapturePlanLoader.ComputeFingerprint(plan),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var session = await JsonSerializer.DeserializeAsync<CalibrationCaptureSession>(
                stream,
                CalibrationJson.CreateOptions(),
                cancellationToken);
            if (session is null)
            {
                Invalid($"Capture session '{path}' contained no data.");
            }

            Validate(session!, plan);
            return session!;
        }
        catch (CalibrationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidCaptureSession,
                $"Capture session '{path}' is not valid JSON: {exception.Message}",
                exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CalibrationException(
                CalibrationErrorCode.FileSystemFailure,
                $"Capture session '{path}' could not be read.",
                exception);
        }
    }

    public static async Task SaveAsync(
        string path,
        CalibrationCaptureSession session,
        CalibrationCapturePlan plan,
        CancellationToken cancellationToken = default)
    {
        Validate(session, plan);
        await AtomicFile.WriteTextAsync(
            path,
            JsonSerializer.Serialize(
                session,
                CalibrationJson.CreateOptions(writeIndented: true)),
            cancellationToken);
    }

    public static void Validate(
        CalibrationCaptureSession session,
        CalibrationCapturePlan plan)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(session.Id))
        {
            Invalid("Capture session id is required.");
        }

        if (!string.Equals(session.PlanName, plan.Name, StringComparison.Ordinal))
        {
            Invalid(
                $"Capture session belongs to plan '{session.PlanName}', not '{plan.Name}'. " +
                "Start a new private workspace or restore the matching capture plan.");
        }

        var expectedPlanSha256 = CalibrationCapturePlanLoader.ComputeFingerprint(plan);
        if (!CalibrationPathSafety.IsSha256(session.PlanSha256) ||
            !session.PlanSha256.Equals(expectedPlanSha256, StringComparison.OrdinalIgnoreCase))
        {
            Invalid(
                "The capture plan changed after this session started. Restore the exact plan " +
                "or start a new private capture workspace; do not mix requirements in one session.");
        }

        if (session.UpdatedAtUtc < session.CreatedAtUtc)
        {
            Invalid("Capture session update time cannot be earlier than creation time.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sequenceNumbers = new HashSet<int>();
        var allowedStates = plan.Requirements.Select(x => x.State).ToHashSet();

        foreach (var capture in session.Captures)
        {
            if (string.IsNullOrWhiteSpace(capture.Id) || !ids.Add(capture.Id))
            {
                Invalid($"Capture session has a missing or duplicate capture id '{capture.Id}'.");
            }

            if (capture.SequenceNumber <= 0 || !sequenceNumbers.Add(capture.SequenceNumber))
            {
                Invalid($"Capture '{capture.Id}' has an invalid or duplicate sequence number.");
            }

            if (!allowedStates.Contains(capture.ExpectedState))
            {
                Invalid($"Capture '{capture.Id}' uses state '{capture.ExpectedState}' outside the plan.");
            }

            if (string.IsNullOrWhiteSpace(capture.RelativePath))
            {
                Invalid($"Capture '{capture.Id}' requires a relative path.");
            }

            _ = CalibrationPathSafety.ResolveInsideRoot(
                Path.GetTempPath(),
                capture.RelativePath,
                $"capture '{capture.Id}'");

            if (!paths.Add(capture.RelativePath.Replace('\\', '/')))
            {
                Invalid($"Capture session has duplicate path '{capture.RelativePath}'.");
            }

            if (!CalibrationPathSafety.IsSha256(capture.Sha256))
            {
                Invalid($"Capture '{capture.Id}' has an invalid SHA-256 value.");
            }

            if (capture.ImageWidth <= 0 || capture.ImageHeight <= 0)
            {
                Invalid($"Capture '{capture.Id}' has invalid image geometry.");
            }

            if (!string.IsNullOrWhiteSpace(capture.DuplicateOfCaptureId) &&
                !session.Captures.Any(x =>
                    string.Equals(
                        x.Id,
                        capture.DuplicateOfCaptureId,
                        StringComparison.OrdinalIgnoreCase)))
            {
                Invalid(
                    $"Capture '{capture.Id}' references missing duplicate source " +
                    $"'{capture.DuplicateOfCaptureId}'.");
            }
        }
    }

    private static void Invalid(string message) =>
        throw new CalibrationException(CalibrationErrorCode.InvalidCaptureSession, message);
}
