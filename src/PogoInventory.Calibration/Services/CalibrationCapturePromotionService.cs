using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Workspace;

namespace PogoInventory.Calibration.Services;

public static class CalibrationCapturePromotionService
{
    public static async Task<CalibrationCapturePromotionResult> PromoteAsync(
        CalibrationWorkspace workspace,
        CalibrationCapturePlan plan,
        string captureId,
        string reviewedBy,
        bool confirmedPrivateReview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(captureId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reviewedBy);

        if (!confirmedPrivateReview)
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureNotReviewable,
                "Promotion requires an explicit confirmation that account identity, location, " +
                "notifications and other personal data were reviewed and are safe for local calibration.");
        }

        var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
            workspace.CaptureSessionPath,
            plan,
            cancellationToken);
        await CalibrationCaptureService.VerifyExistingCaptureFilesAsync(
            workspace,
            session,
            cancellationToken);

        var capture = session.Captures.FirstOrDefault(x =>
            string.Equals(x.Id, captureId, StringComparison.OrdinalIgnoreCase));
        if (capture is null)
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureNotFound,
                $"Capture '{captureId}' was not found in the current session.");
        }

        if (capture.IsDuplicate)
        {
            throw new CalibrationException(
                CalibrationErrorCode.CaptureNotReviewable,
                $"Capture '{capture.Id}' is pixel-identical to '{capture.DuplicateOfCaptureId}' and " +
                "cannot add calibration variation. Review and promote the original capture instead.");
        }

        var sourcePath = CalibrationPathSafety.ResolveInsideRoot(
            workspace.RootPath,
            capture.RelativePath,
            $"capture '{capture.Id}'");
        var fixtureId = capture.PromotedFixtureId ?? $"guided-{capture.Id}";
        var relativeFixturePath = Portable(Path.Combine(
            capture.ExpectedState.ToString(),
            $"{fixtureId}.png"));
        var destinationPath = CalibrationPathSafety.ResolveInsideRoot(
            workspace.FixturesPath,
            relativeFixturePath,
            $"fixture '{fixtureId}'");

        var manifest = await FixtureManifestLoader.LoadAsync(
            workspace.ManifestPath,
            cancellationToken);
        var existingFixture = manifest.Fixtures.FirstOrDefault(x =>
            string.Equals(x.Id, fixtureId, StringComparison.OrdinalIgnoreCase));

        if (capture.IsPromoted)
        {
            await VerifyExistingPromotionAsync(
                capture,
                fixtureId,
                destinationPath,
                existingFixture,
                cancellationToken);
            return Result(
                capture.Id,
                fixtureId,
                destinationPath,
                workspace,
                alreadyPromoted: true);
        }

        if (existingFixture is not null)
        {
            var captureTag = $"capture:{capture.Id}";
            if (existingFixture.Tags.Contains(captureTag, StringComparer.OrdinalIgnoreCase) &&
                existingFixture.Sha256.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(destinationPath))
            {
                var existingHash = await CalibrationHash.Sha256Async(
                    destinationPath,
                    cancellationToken);
                if (!existingHash.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CalibrationException(
                        CalibrationErrorCode.FixtureHashMismatch,
                        $"Fixture '{fixtureId}' is linked to capture '{capture.Id}' but its file hash changed.");
                }

                var repairedCapture = capture with { PromotedFixtureId = fixtureId };
                var repairedSession = session with
                {
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Captures = session.Captures
                        .Select(x => string.Equals(x.Id, capture.Id, StringComparison.OrdinalIgnoreCase)
                            ? repairedCapture
                            : x)
                        .ToArray()
                };
                await CalibrationCaptureSessionRepository.SaveAsync(
                    workspace.CaptureSessionPath,
                    repairedSession,
                    plan,
                    cancellationToken);
                return Result(
                    capture.Id,
                    fixtureId,
                    destinationPath,
                    workspace,
                    alreadyPromoted: true);
            }

            throw new CalibrationException(
                CalibrationErrorCode.InvalidManifest,
                $"Fixture id '{fixtureId}' already exists but is not safely linked to capture '{capture.Id}'.");
        }

        if (manifest.Fixtures.Any(x =>
            x.RelativePath.Replace('\\', '/').Equals(
                relativeFixturePath,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidManifest,
                $"Fixture path '{relativeFixturePath}' already exists in the manifest.");
        }

        if (File.Exists(destinationPath))
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidManifest,
                $"Fixture file '{relativeFixturePath}' already exists outside the manifest. " +
                "Resolve the untracked file before promotion; it will not be overwritten.");
        }

        var bytes = await File.ReadAllBytesAsync(sourcePath, cancellationToken);
        var fixture = new ScreenFixtureDefinition
        {
            Id = fixtureId,
            RelativePath = relativeFixturePath,
            ExpectedState = capture.ExpectedState,
            Sha256 = capture.Sha256,
            SafetyReview = new FixtureSafetyReview
            {
                AccountIdentitySafe = true,
                LocationSafe = true,
                NotificationsSafe = true,
                OtherPersonalDataSafe = true,
                ApprovedForCalibration = true,
                ReviewedBy = reviewedBy.Trim(),
                ReviewedAtUtc = DateTimeOffset.UtcNow
            },
            Tags = new[]
            {
                "guided-capture",
                $"capture:{capture.Id}"
            },
            Notes = string.IsNullOrWhiteSpace(capture.Notes)
                ? "Captured through the guided read-only ADB workflow and explicitly reviewed."
                : capture.Notes
        };

        var updatedManifest = manifest with
        {
            Fixtures = manifest.Fixtures.Concat(new[] { fixture }).ToArray()
        };
        FixtureManifestLoader.Validate(updatedManifest);

        var updatedCapture = capture with { PromotedFixtureId = fixtureId };
        var updatedSession = session with
        {
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Captures = session.Captures
                .Select(x => string.Equals(x.Id, capture.Id, StringComparison.OrdinalIgnoreCase)
                    ? updatedCapture
                    : x)
                .ToArray()
        };

        var manifestCommitted = false;
        try
        {
            await AtomicFile.WriteBytesAsync(destinationPath, bytes, cancellationToken);
            await AtomicFile.WriteTextAsync(
                workspace.ManifestPath,
                JsonSerializer.Serialize(
                    updatedManifest,
                    CalibrationJson.CreateOptions(writeIndented: true)),
                cancellationToken);
            manifestCommitted = true;
            await CalibrationCaptureSessionRepository.SaveAsync(
                workspace.CaptureSessionPath,
                updatedSession,
                plan,
                cancellationToken);
        }
        catch
        {
            if (!manifestCommitted)
            {
                TryDelete(destinationPath);
            }
            throw;
        }

        return Result(
            capture.Id,
            fixtureId,
            destinationPath,
            workspace,
            alreadyPromoted: false);
    }


    private static async Task VerifyExistingPromotionAsync(
        CalibrationCaptureRecord capture,
        string fixtureId,
        string destinationPath,
        ScreenFixtureDefinition? existingFixture,
        CancellationToken cancellationToken)
    {
        if (existingFixture is null || !File.Exists(destinationPath))
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidCaptureSession,
                $"Capture '{capture.Id}' says it was promoted, but fixture '{fixtureId}' is incomplete.");
        }

        var existingHash = await CalibrationHash.Sha256Async(
            destinationPath,
            cancellationToken);
        if (!existingHash.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase) ||
            !existingFixture.Sha256.Equals(capture.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new CalibrationException(
                CalibrationErrorCode.FixtureHashMismatch,
                $"Promoted fixture '{fixtureId}' no longer matches capture '{capture.Id}'.");
        }
    }

    private static CalibrationCapturePromotionResult Result(
        string captureId,
        string fixtureId,
        string fixturePath,
        CalibrationWorkspace workspace,
        bool alreadyPromoted) =>
        new()
        {
            CaptureId = captureId,
            FixtureId = fixtureId,
            FixturePath = fixturePath,
            ManifestPath = workspace.ManifestPath,
            SessionPath = workspace.CaptureSessionPath,
            AlreadyPromoted = alreadyPromoted
        };

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
