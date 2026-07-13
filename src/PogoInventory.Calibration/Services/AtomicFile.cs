using System.Text;
using PogoInventory.Calibration.Errors;

namespace PogoInventory.Calibration.Services;

internal static class AtomicFile
{
    public static Task WriteTextAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            path,
            temporaryPath => File.WriteAllTextAsync(
                temporaryPath,
                content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken),
            cancellationToken);

    public static Task WriteBytesAsync(
        string path,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return WriteAsync(
            path,
            temporaryPath => File.WriteAllBytesAsync(
                temporaryPath,
                content,
                cancellationToken),
            cancellationToken);
    }

    private static async Task WriteAsync(
        string path,
        Func<string, Task> writeTemporaryAsync,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(writeTemporaryAsync);

        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await writeTemporaryAsync(temporaryPath);
                cancellationToken.ThrowIfCancellationRequested();
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
            throw new CalibrationException(
                CalibrationErrorCode.FileSystemFailure,
                $"Could not write calibration file '{path}'.",
                exception);
        }
    }
}
