using System.Text.Json;

internal static class AppraisalStreamingCalibrationTests
{
    public static Task RunAsync()
    {
        var root = FindRepositoryRoot();
        var cli = File.ReadAllText(Path.Combine(root, "src", "PogoInventory.Cli", "Program.cs"));
        Assert(cli.Contains("device-calibrate-appraisal-streaming-gates", StringComparison.Ordinal),
            "calibration command is registered");
        Assert(cli.Contains("CaptureAppraisalAsync", StringComparison.Ordinal) &&
               cli.Contains("UnknownOrUnstableInitialState", StringComparison.Ordinal) &&
               cli.Contains("calibrationInputCommandsSent = 0", StringComparison.Ordinal),
            "calibration orchestration is named and fail-closed with zero gate input");

        using var profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "profiles", "pokemon-go-appraisal-bars-oneplus6t-portrait.json")));
        var regions = profile.RootElement.GetProperty("regions").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("name").GetString()!,
                item => item.GetProperty("stabilityRole").GetString()!,
                StringComparer.Ordinal);
        foreach (var required in new[] { "Header", "AppraisalPanel", "AttackBar", "DefenseBar", "HpBar" })
            Assert(regions[required] == "Required", $"{required} is required");
        Assert(regions["Model"] == "Volatile" && regions["AnimatedBackground"] == "Volatile",
            "model and animated background remain volatile");
        Assert(regions["BottomControl"] == "DiagnosticOnly",
            "appraisal bottom control is diagnostic-only");
        return Task.CompletedTask;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
