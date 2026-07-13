using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Calibration.Services;

public static class FixtureRepository
{
    public static async Task<IReadOnlyList<LoadedScreenFixture>> LoadApprovedAsync(
        ScreenFixtureManifest manifest,
        string fixturesRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturesRoot);

        var loaded = new List<LoadedScreenFixture>();
        foreach (var fixture in manifest.Fixtures.Where(x => x.SafetyReview.IsComplete))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = CalibrationPathSafety.ResolveInsideRoot(
                fixturesRoot,
                fixture.RelativePath,
                $"fixture '{fixture.Id}'");

            if (!File.Exists(fullPath))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FixtureMissing,
                    $"Fixture '{fixture.Id}' was not found at '{fullPath}'.");
            }

            var actualHash = await CalibrationHash.Sha256Async(fullPath, cancellationToken);
            if (!actualHash.Equals(fixture.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FixtureHashMismatch,
                    $"Fixture '{fixture.Id}' changed after approval. " +
                    $"Expected {fixture.Sha256}, got {actualHash}. Run calibration-index and review it again.");
            }

            byte[] png;
            try
            {
                png = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new CalibrationException(
                    CalibrationErrorCode.FileSystemFailure,
                    $"Fixture '{fixture.Id}' could not be read.",
                    exception);
            }

            loaded.Add(new LoadedScreenFixture
            {
                Definition = fixture,
                FullPath = fullPath,
                Image = PngDecoder.Decode(png)
            });
        }

        return loaded;
    }
}
