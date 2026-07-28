using System.Text;
using System.Text.Json;

namespace PogoInventory.Streaming.Semantics.Shadow;

public sealed class ShadowReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<(string JsonPath, string MarkdownPath)> WriteAsync(
        ShadowSessionReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, "semantic-shadow-session.json");
        var markdownPath = Path.Combine(outputDirectory, "semantic-shadow-session.md");
        await WriteAtomicAsync(jsonPath, JsonSerializer.Serialize(report, JsonOptions), cancellationToken).ConfigureAwait(false);
        await WriteAtomicAsync(markdownPath, CreateMarkdown(report), cancellationToken).ConfigureAwait(false);
        return (jsonPath, markdownPath);
    }

    private static string CreateMarkdown(ShadowSessionReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Streaming Vision Phase 6B shadow report");
        builder.AppendLine();
        builder.AppendLine($"- Session: `{report.SessionId}`");
        builder.AppendLine($"- Status: `{report.FinalStatus}`");
        builder.AppendLine($"- Frames: `{report.Frames.Count}`");
        builder.AppendLine($"- Analyzer faults: `{report.AnalyzerFaults}`");
        builder.AppendLine($"- Analyzer timeouts: `{report.AnalyzerTimeouts}`");
        builder.AppendLine($"- Known candidates: `{report.KnownCandidates}`");
        builder.AppendLine($"- Comparison conflicts: `{report.ComparisonConflicts}`");
        builder.AppendLine($"- Authorizes phone input: `{report.AuthorizesPhoneInput}`");
        builder.AppendLine($"- Input commands sent: `{report.InputCommandsSent}`");
        builder.AppendLine();
        builder.AppendLine("| Frame | Roles | Field | Result | Candidates | Reference | Reason |");
        builder.AppendLine("|---:|---|---|---|---|---|---|");

        foreach (var frame in report.Frames.OrderBy(x => x.FrameId))
        {
            foreach (var comparison in frame.Comparisons.OrderBy(x => x.FieldName, StringComparer.Ordinal))
            {
                builder.AppendLine(
                    $"| {frame.FrameId} | {Escape(string.Join(", ", frame.Roles))} | {Escape(comparison.FieldName)} | " +
                    $"{comparison.Kind} | {Escape(string.Join(", ", comparison.CandidateValues))} | " +
                    $"{Escape(comparison.ReferenceValue ?? "")} | `{Escape(comparison.ReasonCode)}` |");
            }
        }

        return builder.ToString();
    }

    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
