using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;

namespace PogoInventory.Core.Reporting;

public sealed class DecisionReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task WriteAsync(
        InventoryAnalysisResult result,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);

        var jsonPath = Path.Combine(outputDirectory, "decision-plan.json");
        var markdownPath = Path.Combine(outputDirectory, "decision-plan.md");

        var json = JsonSerializer.Serialize(result, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, json, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(result), cancellationToken);
    }

    private static string BuildMarkdown(InventoryAnalysisResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Decision plan");
        builder.AppendLine();
        builder.AppendLine($"- KEEP: {result.KeepCount}");
        builder.AppendLine($"- REVIEW: {result.ReviewCount}");
        builder.AppendLine($"- DELETE: {result.DeleteCount}");
        builder.AppendLine();

        foreach (var category in Enum.GetValues<DecisionCategory>())
        {
            builder.AppendLine($"## {category.ToString().ToUpperInvariant()}");
            builder.AppendLine();

            foreach (var decision in result.Decisions.Where(x => x.Category == category))
            {
                builder.AppendLine($"### {decision.SequenceNumber}. {decision.Species} ({decision.ExternalKey})");
                foreach (var reason in decision.Reasons)
                {
                    builder.AppendLine($"- `{reason.Code}`: {reason.Message}");
                }

                if (!string.IsNullOrWhiteSpace(decision.BetterDuplicateExternalKey))
                {
                    builder.AppendLine($"- Better retained duplicate: `{decision.BetterDuplicateExternalKey}`");
                }

                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
