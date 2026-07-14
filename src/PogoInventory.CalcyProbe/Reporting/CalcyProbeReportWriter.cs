using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.CalcyProbe.Models;

namespace PogoInventory.CalcyProbe.Reporting;

public static class CalcyProbeReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<(string JsonPath, string MarkdownPath)> WriteAsync(
        CalcyProbeReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var jsonPath = Path.Combine(outputDirectory, "calcy-probe-report.json");
        var markdownPath = Path.Combine(outputDirectory, "calcy-probe-report.md");

        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(report, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            BuildMarkdown(report),
            cancellationToken);
        return (jsonPath, markdownPath);
    }

    private static string BuildMarkdown(CalcyProbeReport report)
    {
        var text = new StringBuilder();
        text.AppendLine("# Calcy probe report");
        text.AppendLine();
        text.AppendLine($"- Decision: **{report.Decision}**");
        text.AppendLine($"- Device: {report.Device.Model ?? "Unknown"} ({report.Device.Serial})");
        text.AppendLine($"- Package: `{report.PackageName}`");
        text.AppendLine($"- Installed: {report.Package.IsInstalled}");
        text.AppendLine($"- Version: {report.Package.VersionName ?? "Unknown"}");
        text.AppendLine($"- Process id: {report.ProcessId ?? "Not running"}");
        text.AppendLine($"- Filtered log lines: {report.FilteredLogLineCount}");
        text.AppendLine();
        text.AppendLine("## Capabilities");
        text.AppendLine();
        foreach (var capability in report.Capabilities)
        {
            text.AppendLine($"### {capability.Name}: {capability.Status}");
            text.AppendLine();
            text.AppendLine(capability.Detail);
            if (capability.Evidence.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("```text");
                foreach (var evidence in capability.Evidence)
                {
                    text.AppendLine(evidence);
                }
                text.AppendLine("```");
            }
            text.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            text.AppendLine("## Warnings");
            text.AppendLine();
            foreach (var warning in report.Warnings)
            {
                text.AppendLine($"- {warning}");
            }
        }

        return text.ToString();
    }
}
