namespace PogoInventory.Device.Adb;

/// <summary>
/// Pure decision logic for resolving the adb executable path from an optional
/// explicit `--adb` value and a caller-probed bundled-adb candidate. Kept free
/// of filesystem access so it is trivially unit-testable; callers do the
/// `Path.GetFullPath`/`File.Exists` probing and pass in the results.
/// </summary>
public static class AdbPathResolver
{
    /// <summary>
    /// Resolves the adb path to use.
    /// </summary>
    /// <param name="adbOption">The raw `--adb` option value, or null if omitted.</param>
    /// <param name="bundledCandidateFullPathIfExists">
    /// The absolute path to the bundled `tools/platform-tools/adb.exe`, already
    /// probed with <c>File.Exists</c> by the caller, or null when it is not present.
    /// </param>
    public static string ResolveAdbPath(string? adbOption, string? bundledCandidateFullPathIfExists)
    {
        if (adbOption is not null)
        {
            var hasDirectorySeparator =
                adbOption.Contains(Path.DirectorySeparatorChar) ||
                adbOption.Contains(Path.AltDirectorySeparatorChar);
            return hasDirectorySeparator ? Path.GetFullPath(adbOption) : adbOption;
        }

        return bundledCandidateFullPathIfExists ?? "adb";
    }
}
