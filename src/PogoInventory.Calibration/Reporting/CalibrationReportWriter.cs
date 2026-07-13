using System.Globalization;
using System.Text;
using System.Text.Json;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Services;

namespace PogoInventory.Calibration.Reporting;

public static class CalibrationReportWriter
{
    public static async Task WriteAsync(
        CalibrationAcceptanceReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);

        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "calibration-acceptance.json"),
            JsonSerializer.Serialize(
                report,
                CalibrationJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "calibration-acceptance.md"),
            BuildMarkdown(report),
            cancellationToken);
        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "confusion-matrix.csv"),
            BuildConfusionCsv(report),
            cancellationToken);
        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "fixture-results.csv"),
            BuildFixtureCsv(report),
            cancellationToken);
    }

    private static string BuildMarkdown(CalibrationAcceptanceReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# Screen calibration acceptance");
        text.AppendLine();
        text.AppendLine($"- Manifest: `{report.ManifestName}`");
        text.AppendLine($"- Profile: `{report.ProfileName}`");
        text.AppendLine($"- Result: **{(report.Accepted ? "ACCEPTED" : "REJECTED")}**");
        text.AppendLine($"- Approved fixtures: {report.ApprovedFixtureCount}");
        text.AppendLine($"- Correct: {report.CorrectCount}");
        text.AppendLine($"- False negatives: {report.FalseNegativeCount}");
        text.AppendLine($"- False positives: {report.FalsePositiveCount}");
        text.AppendLine($"- Misclassifications: {report.MisclassificationCount}");
        text.AppendLine($"- Weak anchors: {report.WeakAnchorCount}");
        text.AppendLine();

        text.AppendLine("## Acceptance failures");
        text.AppendLine();
        if (report.AcceptanceFailures.Count == 0)
        {
            text.AppendLine("None.");
        }
        else
        {
            foreach (var failure in report.AcceptanceFailures)
            {
                text.AppendLine($"- {failure}");
            }
        }

        text.AppendLine();
        text.AppendLine("## State metrics");
        text.AppendLine();
        text.AppendLine("| State | Fixtures | Correct | Unknown | Wrong known | Recall | Required | Accepted |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---|");
        foreach (var state in report.States)
        {
            text.AppendLine(
                $"| {state.State} | {state.ExpectedCount} | {state.CorrectCount} | " +
                $"{state.UnknownCount} | {state.WrongKnownStateCount} | {state.Recall:P2} | " +
                $"{state.MinimumApprovedFixtures} / {state.MinimumRecall:P0} | " +
                $"{(state.Accepted ? "Yes" : "No")} |");
        }

        text.AppendLine();
        text.AppendLine("## Weak anchors");
        text.AppendLine();
        var weak = report.Anchors.Where(x => x.Weak).ToArray();
        if (weak.Length == 0)
        {
            text.AppendLine("None.");
        }
        else
        {
            foreach (var anchor in weak)
            {
                text.AppendLine(
                    $"- `{anchor.State}/{anchor.AnchorName}`: " +
                    string.Join(", ", anchor.WeakReasons));
            }
        }

        text.AppendLine();
        text.AppendLine("## Fixture results");
        text.AppendLine();
        text.AppendLine("| Fixture | Expected | Actual | Outcome | Confidence | Margin |");
        text.AppendLine("|---|---|---|---|---:|---:|");
        foreach (var fixture in report.Fixtures)
        {
            text.AppendLine(
                $"| `{fixture.FixtureId}` | {fixture.ExpectedState} | {fixture.ActualState} | " +
                $"{fixture.Outcome} | {fixture.Confidence:F4} | " +
                $"{(fixture.WinnerMargin?.ToString("F4", CultureInfo.InvariantCulture) ?? "n/a")} |");
        }

        return text.ToString();
    }

    private static string BuildConfusionCsv(CalibrationAcceptanceReport report)
    {
        var text = new StringBuilder("expectedState,actualState,count\n");
        foreach (var cell in report.ConfusionMatrix)
        {
            text.AppendLine($"{cell.ExpectedState},{cell.ActualState},{cell.Count}");
        }

        return text.ToString();
    }

    private static string BuildFixtureCsv(CalibrationAcceptanceReport report)
    {
        var text = new StringBuilder(
            "fixtureId,relativePath,expectedState,actualState,outcome,confidence,winnerMargin,reasons\n");
        foreach (var fixture in report.Fixtures)
        {
            text.AppendLine(string.Join(",", new[]
            {
                Csv(fixture.FixtureId),
                Csv(fixture.RelativePath),
                Csv(fixture.ExpectedState.ToString()),
                Csv(fixture.ActualState.ToString()),
                Csv(fixture.Outcome.ToString()),
                fixture.Confidence.ToString("F6", CultureInfo.InvariantCulture),
                fixture.WinnerMargin?.ToString("F6", CultureInfo.InvariantCulture) ?? string.Empty,
                Csv(string.Join(";", fixture.Reasons))
            }));
        }

        return text.ToString();
    }

    private static string Csv(string value) =>
        "\"" + value.Replace("\"", "\"\"") + "\"";
}
