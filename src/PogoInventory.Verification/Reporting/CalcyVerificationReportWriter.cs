using System.Globalization;
using System.Text;
using System.Text.Json;
using PogoInventory.Verification.Models;
using PogoInventory.Verification.Services;

namespace PogoInventory.Verification.Reporting;

public static class CalcyVerificationReportWriter
{
    public static async Task WriteAsync(
        CalcyVerificationReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        report.Validate();
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "verification-report.json"),
            JsonSerializer.Serialize(
                report,
                VerificationJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "verification-report.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "verification-cases.csv"),
            Csv(report),
            cancellationToken);
    }

    private static string Markdown(CalcyVerificationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Calcy provider verification");
        builder.AppendLine();
        builder.AppendLine($"- Mechanism: {report.Mechanism}");
        builder.AppendLine($"- Provider version: {report.ProviderVersion ?? "Unknown"}");
        builder.AppendLine($"- Cases: {report.CaseCount}");
        builder.AppendLine($"- Exact Complete: {report.ExactCompleteCount}");
        builder.AppendLine($"- Wrong Complete: {report.WrongCompleteCount}");
        builder.AppendLine($"- Exact Complete rate: {report.ExactCompleteRate:P1}");
        builder.AppendLine($"- Safe for long scan: {report.SafeForLongScan}");
        builder.AppendLine($"- Recommended for long scan: {report.RecommendedForLongScan}");
        builder.AppendLine($"- Gate: {report.GateDetail}");
        builder.AppendLine();
        builder.AppendLine("| Case | Outcome | Observed status | Mismatches |");
        builder.AppendLine("|---|---|---|---|");
        foreach (var item in report.Cases)
        {
            builder.AppendLine(
                $"| {EscapeMarkdown(item.Id)} | {item.Outcome} | " +
                $"{item.Observed?.Status.ToString() ?? "None"} | " +
                $"{EscapeMarkdown(string.Join("; ", item.Mismatches))} |");
        }
        return builder.ToString();
    }

    private static string Csv(CalcyVerificationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("id,outcome,observedStatus,species,cp,attackIv,defenseIv,hpIv,mismatches,error");
        foreach (var item in report.Cases)
        {
            var fields = new[]
            {
                item.Id,
                item.Outcome.ToString(),
                item.Observed?.Status.ToString() ?? string.Empty,
                item.Observed?.Species ?? string.Empty,
                item.Observed?.Cp?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.Observed?.AttackIv?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.Observed?.DefenseIv?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                item.Observed?.HpIv?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                string.Join("; ", item.Mismatches),
                item.ErrorDetail ?? string.Empty
            };
            builder.AppendLine(string.Join(",", fields.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static string EscapeMarkdown(string value) =>
        value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
