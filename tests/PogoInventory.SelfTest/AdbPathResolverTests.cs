using PogoInventory.Device.Adb;

namespace PogoInventory.SelfTest;

internal static class AdbPathResolverTests
{
    public static Task RunAsync()
    {
        var withSeparator = AdbPathResolver.ResolveAdbPath(
            @"tools\platform-tools\adb.exe", bundledCandidateFullPathIfExists: null);
        Assert(withSeparator == Path.GetFullPath(@"tools\platform-tools\adb.exe"),
            $"explicit path-separated --adb value is absolute-ified ({withSeparator})");

        var bareName = AdbPathResolver.ResolveAdbPath("adb", bundledCandidateFullPathIfExists: null);
        Assert(bareName == "adb",
            $"explicit bare --adb command name is returned unchanged ({bareName})");

        var bundledCandidate = Path.GetFullPath(Path.Combine("tools", "platform-tools", "adb.exe"));
        var withBundled = AdbPathResolver.ResolveAdbPath(
            adbOption: null, bundledCandidateFullPathIfExists: bundledCandidate);
        Assert(withBundled == bundledCandidate,
            $"omitted --adb resolves to the bundled adb when present ({withBundled})");

        var withoutBundled = AdbPathResolver.ResolveAdbPath(
            adbOption: null, bundledCandidateFullPathIfExists: null);
        Assert(withoutBundled == "adb",
            $"omitted --adb falls back to PATH lookup when no bundled adb exists ({withoutBundled})");

        return Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
