using PogoInventory.Automation.Errors;

namespace PogoInventory.Automation.Services;

internal static class AutomationAtomicFile
{
    public static async Task WriteBytesAsync(
        string path,
        byte[] data,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(data);
        await WriteAsync(
            path,
            temporary => File.WriteAllBytesAsync(temporary, data, cancellationToken),
            cancellationToken);
    }

    public static async Task WriteTextAsync(
        string path,
        string data,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(data);
        await WriteAsync(
            path,
            temporary => File.WriteAllTextAsync(temporary, data, cancellationToken),
            cancellationToken);
    }

    private static async Task WriteAsync(
        string path,
        Func<string, Task> writer,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new AutomationException(
                AutomationErrorCode.FileSystemFailure,
                $"Output path '{path}' has no directory.");
        }

        Directory.CreateDirectory(directory);
        var temporary = fullPath + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await writer(temporary);
            cancellationToken.ThrowIfCancellationRequested();
            IOException? lastMoveException = null;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Move(temporary, fullPath, overwrite: true);
                    lastMoveException = null;
                    break;
                }
                catch (IOException exception) when (attempt < 4)
                {
                    lastMoveException = exception;
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
                }
            }

            if (lastMoveException is not null)
            {
                throw lastMoveException;
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new AutomationException(
                AutomationErrorCode.FileSystemFailure,
                $"Could not write '{fullPath}'.",
                exception);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
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
}
