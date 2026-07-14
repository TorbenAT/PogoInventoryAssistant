using System.Globalization;
using System.Text;
using PogoInventory.Appraisal.Models;

namespace PogoInventory.Appraisal.Services;

public static class PhonePreparationReportWriter
{
    public static string Markdown(PhonePreparationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        report.Validate();

        var builder = new StringBuilder();
        builder.AppendLine("# Android phone readiness");
        builder.AppendLine();
        builder.AppendLine($"- Serial: `{Escape(report.Device.Serial)}`");
        builder.AppendLine(
            $"- Device: {Escape(report.Device.Manufacturer ?? "Unknown")} " +
            $"{Escape(report.Device.Model ?? "Unknown")}");
        builder.AppendLine(
            $"- Android: {Escape(report.Device.AndroidVersion ?? "Unknown")}");
        builder.AppendLine(
            $"- Screenshot: {report.ScreenshotWidth}x{report.ScreenshotHeight}");
        builder.AppendLine($"- Portrait: {report.Portrait}");
        builder.AppendLine($"- ADB ready: {report.AdbReady}");
        builder.AppendLine(
            $"- Passive capture ready: {report.PassiveCaptureReady}");
        builder.AppendLine(
            $"- Appraisal calibration ready: " +
            $"{report.AppraisalCalibrationReady}");
        builder.AppendLine(
            $"- Verified IV extraction ready: " +
            $"{report.VerifiedIvExtractionReady}");
        builder.AppendLine(
            $"- Automatic navigation ready: " +
            $"{report.AutomaticNavigationReady}");
        builder.AppendLine(
            $"- Appraisal status: {report.Appraisal.Status}");
        builder.AppendLine(
            $"- Candidate score: " +
            $"{report.Appraisal.CandidateScore.ToString("F3", CultureInfo.InvariantCulture)}");
        builder.AppendLine(
            $"- Device profile: " +
            $"{report.GeneratedProfileFile ?? "Not generated"}");
        builder.AppendLine();
        builder.AppendLine(
            "This command is read only. It captures one screenshot and does not tap or swipe.");
        builder.AppendLine();

        if (report.Appraisal.IsAppraisal)
        {
            builder.AppendLine("## Candidate IV measurements");
            builder.AppendLine();
            builder.AppendLine("| Bar | Estimate | Fill | Confidence |");
            builder.AppendLine("|---|---:|---:|---:|");
            foreach (var bar in report.Appraisal.Bars)
            {
                builder.AppendLine(
                    $"| {bar.Kind} | " +
                    $"{bar.EstimatedIv?.ToString(CultureInfo.InvariantCulture) ?? "?"} | " +
                    $"{bar.FillFraction:P1} | {bar.Confidence:P1} |");
            }
            builder.AppendLine();
            builder.AppendLine(
                "The values above remain candidates unless the generated profile later passes the verification gate.");
            builder.AppendLine();
        }

        builder.AppendLine("## Next actions");
        builder.AppendLine();
        foreach (var action in report.NextActions)
        {
            builder.AppendLine($"- {Escape(action)}");
        }

        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ");
}
