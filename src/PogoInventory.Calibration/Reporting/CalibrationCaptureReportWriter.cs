using System.Text;
using System.Text.Json;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Services;

namespace PogoInventory.Calibration.Reporting;

public static class CalibrationCaptureReportWriter
{
    public static async Task WriteAsync(
        CalibrationCaptureStatus status,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);

        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "capture-status.json"),
            JsonSerializer.Serialize(
                status,
                CalibrationJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "capture-status.md"),
            BuildMarkdown(status),
            cancellationToken);
    }

    private static string BuildMarkdown(CalibrationCaptureStatus status)
    {
        var text = new StringBuilder();
        text.AppendLine("# Private screen capture status");
        text.AppendLine();
        text.AppendLine($"- Plan: `{status.PlanName}`");
        text.AppendLine($"- Session: `{status.SessionId}`");
        text.AppendLine($"- Captures: {status.TotalCaptureCount}");
        text.AppendLine($"- Unique: {status.UniqueCaptureCount}");
        text.AppendLine($"- Duplicates: {status.DuplicateCaptureCount}");
        text.AppendLine($"- Promoted after privacy review: {status.PromotedCaptureCount}");
        text.AppendLine($"- Required coverage: **{(status.RequiredCoverageComplete ? "COMPLETE" : "INCOMPLETE")}**");
        text.AppendLine($"- Next recommended state: {status.NextRecommendedState?.ToString() ?? "None"}");
        text.AppendLine();
        text.AppendLine("| State | Unique | Required | Duplicates | Promoted | Remaining | Optional | Complete |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---|---|");
        foreach (var state in status.States)
        {
            text.AppendLine(
                $"| {state.State} | {state.UniqueCaptureCount} | {state.RequiredUniqueCaptures} | " +
                $"{state.DuplicateCaptureCount} | {state.PromotedCaptureCount} | {state.Remaining} | " +
                $"{(state.OptionalWhenUnavailable ? "Yes" : "No")} | {(state.Complete ? "Yes" : "No")} |");
        }

        text.AppendLine();
        text.AppendLine("## Warnings");
        text.AppendLine();
        if (status.Warnings.Count == 0)
        {
            text.AppendLine("None.");
        }
        else
        {
            foreach (var warning in status.Warnings)
            {
                text.AppendLine($"- {warning}");
            }
        }

        return text.ToString();
    }
}
