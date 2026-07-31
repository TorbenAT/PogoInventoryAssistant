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
        var streamReader = File.ReadAllText(Path.Combine(
            root, "src", "PogoInventory.Cli",
            "StreamPokemonReaderCommand.cs"));
        Assert(
            streamReader.Contains(
                "binarizeCpRegion: false",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "binarizeCpRegion: true",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "ResolveCpPreprocessingVariants",
                StringComparison.Ordinal) &&
            !streamReader.Contains(
                "preferSecondWhenFullySupported: true",
                StringComparison.Ordinal),
            "real-phone stream reader must use both deterministic CP preprocessing variants");
        Assert(
            streamReader.Contains(
                "ZeroIvBarConfidenceMinimum = .45",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "bar.EstimatedIv == 0",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "!bar.OrangeDetected",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "bar.FillFraction <= .02",
                StringComparison.Ordinal),
            "real-phone IV consensus must allow only a measured empty gray track as zero IV");
        Assert(
            streamReader.Contains(
                "ProgressionOnlyIvBarConfidenceMinimum = .65",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "PROGRESSION_ONLY_FIVE_FRAME_MODERATE_IV_EVIDENCE",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "progressionOnly.FrameIds.Distinct().Count() != frameCount",
                StringComparison.Ordinal) &&
            streamReader.Contains(
                "IsComplete = completeResult.IsComplete",
                StringComparison.Ordinal),
            "moderate-confidence IV evidence is restricted to five-frame progression proof and cannot complete a record");

        using var profile = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            root, "profiles", "pokemon-go-appraisal-bars-oneplus6t-portrait.json")));
        var regions = profile.RootElement.GetProperty("regions").EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("name").GetString()!,
                item => item.GetProperty("stabilityRole").GetString()!,
                StringComparer.Ordinal);
        foreach (var required in new[] { "AppraisalPanel", "AttackBar", "DefenseBar", "HpBar" })
            Assert(regions[required] == "Required", $"{required} is required");
        Assert(regions["Header"] == "DiagnosticOnly",
            "animated CP background keeps Header diagnostic while semantic CP remains frame-bound");
        Assert(regions["Model"] == "Volatile" && regions["AnimatedBackground"] == "Volatile",
            "model and animated background remain volatile");
        Assert(regions["BottomControl"] == "DiagnosticOnly",
            "appraisal bottom control is diagnostic-only");

        var header = profile.RootElement.GetProperty("regions").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "Header")
            .GetProperty("region");
        Assert(
            Math.Abs(header.GetProperty("x").GetDouble() - .32) < .0001 &&
            Math.Abs(header.GetProperty("y").GetDouble() - .08) < .0001 &&
            Math.Abs(header.GetProperty("width").GetDouble() - .36) < .0001 &&
            Math.Abs(header.GetProperty("height").GetDouble() - .05) < .0001,
            "required Header gate must stay on the validated CP ROI, outside volatile model animation");

        var sharpness = profile.RootElement.GetProperty("stable")
            .GetProperty("minimumSharpnessScoreByRegion");
        foreach (var bar in new[] { "AttackBar", "DefenseBar", "HpBar" })
            Assert(sharpness.GetProperty(bar).GetDouble() == 0,
                $"{bar} must allow a valid uniform zero-IV track");
        AssertRegion(profile.RootElement, "AttackBar", .10, .705, .40, .035);
        AssertRegion(profile.RootElement, "DefenseBar", .10, .748, .40, .035);
        AssertRegion(profile.RootElement, "HpBar", .10, .792, .40, .035);
        Assert(
            profile.RootElement.GetProperty("stable")
                .GetProperty("minimumStableFrames").GetInt32() == 5,
            "real-phone semantic evidence window must retain five stable frames");

        using var appraisalProfile = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(
                root, "profiles", "appraisal-normalized-v1.json")));
        Assert(
            Math.Abs(
                appraisalProfile.RootElement
                    .GetProperty("completeBarConfidenceMinimum")
                    .GetDouble() - .70) < .0001,
            "real-phone IV consensus must retain the measured 0.70 bar-confidence floor");
        return Task.CompletedTask;
    }

    private static void AssertRegion(
        JsonElement root,
        string name,
        double x,
        double y,
        double width,
        double height)
    {
        var region = root.GetProperty("regions").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name)
            .GetProperty("region");
        Assert(
            Math.Abs(region.GetProperty("x").GetDouble() - x) < .0001 &&
            Math.Abs(region.GetProperty("y").GetDouble() - y) < .0001 &&
            Math.Abs(region.GetProperty("width").GetDouble() - width) < .0001 &&
            Math.Abs(region.GetProperty("height").GetDouble() - height) < .0001,
            $"{name} must stay on the real-phone appraisal bar");
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
