using PogoInventory.Calibration.Errors;

namespace PogoInventory.Calibration.Services;

public static class CalibrationPathSafety
{
    public static string ResolveInsideRoot(
        string rootPath,
        string relativePath,
        string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        if (Path.IsPathRooted(relativePath))
        {
            throw new CalibrationException(
                CalibrationErrorCode.UnsafePath,
                $"The {description} path must be relative: '{relativePath}'.");
        }

        var normalisedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, normalisedRelative));
        var prefix = root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new CalibrationException(
                CalibrationErrorCode.UnsafePath,
                $"The {description} path escapes its root: '{relativePath}'.");
        }

        return candidate;
    }

    public static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}
