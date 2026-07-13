using System.Text.Json;
using PogoInventory.Vision.Errors;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;

namespace PogoInventory.Vision.Reporting;

public static class ScreenDetectionReportWriter
{
    public static async Task WriteAsync(
        ScreenDetectionResult result,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        try
        {
            var fullPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 16 * 1024,
                    useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        result,
                        ScreenProfileLoader.CreateJsonOptions(writeIndented: true),
                        cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ScreenVisionException(
                VisionErrorCode.FileSystemFailure,
                $"The screen detection report could not be written to '{outputPath}'.",
                exception);
        }
    }
}
