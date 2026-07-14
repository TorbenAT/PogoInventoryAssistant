using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PogoInventory.CropAtlas.Semantic.Models;

namespace PogoInventory.CropAtlas.Semantic.Services;

public static class SemanticEvidenceReportWriter
{
    public static async Task WriteAsync(
        SemanticEvidenceReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        report.Validate();

        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);

        var reportPath = Path.Combine(
            root,
            "semantic-evidence.json");
        var markdownPath = Path.Combine(
            root,
            "semantic-evidence.md");
        var casesPath = Path.Combine(
            root,
            "semantic-evidence-cases.csv");
        var truthPath = Path.Combine(
            root,
            report.TruthTemplateFile);

        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(
                report,
                SemanticEvidenceJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            markdownPath,
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            casesPath,
            CasesCsv(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            truthPath,
            JsonSerializer.Serialize(
                BuildTruthTemplate(report),
                SemanticEvidenceJson.CreateOptions(writeIndented: true)),
            cancellationToken);

        WriteReviewPack(
            root,
            Path.Combine(root, report.ReviewPackFile));
    }

    private static SemanticTruthTemplate BuildTruthTemplate(
        SemanticEvidenceReport report) =>
        new()
        {
            GeneratedAtUtc = report.GeneratedAtUtc,
            Cases = report.Cases
                .OrderBy(item => item.SequenceNumber)
                .ThenBy(item => item.CaseId, StringComparer.Ordinal)
                .Select(item => new SemanticTruthCase
                {
                    CaseId = item.CaseId,
                    SourceFile = item.SourceFile,
                    ClusterId = item.ClusterId,
                    ExpectedScreenState = null,
                    ExpectedSpecies = null,
                    ExpectedCp = null,
                    ExpectedAttackIv = null,
                    ExpectedDefenseIv = null,
                    ExpectedHpIv = null,
                    Notes = null,
                    Reviewed = false
                })
                .ToArray()
        };

    private static string Markdown(SemanticEvidenceReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# iPhone semantic evidence pack");
        builder.AppendLine();
        builder.AppendLine($"- Cases: {report.CaseCount}");
        builder.AppendLine($"- Visual clusters: {report.ClusterCount}");
        builder.AppendLine($"- Selected regions: {report.SelectedRegionCount}");
        builder.AppendLine($"- Derived crops: {report.CropCount}");
        builder.AppendLine($"- Accepted: {report.Accepted}");
        builder.AppendLine(
            $"- Ready for external visual review: " +
            $"{report.Readiness.ReadyForExternalVisualReview}");
        builder.AppendLine(
            $"- Ready for automated extraction: " +
            $"{report.Readiness.ReadyForAutomatedExtraction}");
        builder.AppendLine(
            $"- More images indicated: " +
            $"{report.Readiness.NeedsMoreImages}");
        builder.AppendLine($"- Gate: {Escape(report.GateDetail)}");
        builder.AppendLine();
        builder.AppendLine(
            "No species, CP, IV or screen-state value is asserted by this pack. " +
            "The truth template is intentionally empty.");
        builder.AppendLine();

        builder.AppendLine("## Cluster overview");
        builder.AppendLine();
        builder.AppendLine(
            "![Cluster overview](atlas/cluster-overview.png)");
        builder.AppendLine();

        builder.AppendLine("## Cluster coverage");
        builder.AppendLine();
        builder.AppendLine("| Cluster | Cases | Underrepresented |");
        builder.AppendLine("|---|---:|---|");
        foreach (var cluster in report.Clusters)
        {
            builder.AppendLine(
                $"| {Escape(cluster.ClusterId)} | {cluster.CaseCount} | " +
                $"{cluster.Underrepresented} |");
        }
        builder.AppendLine();

        builder.AppendLine("## Readiness");
        builder.AppendLine();
        foreach (var reason in report.Readiness.Reasons)
        {
            builder.AppendLine($"- {Escape(reason)}");
        }
        builder.AppendLine(
            $"- Next action: {Escape(report.Readiness.RecommendedNextAction)}");
        builder.AppendLine();

        builder.AppendLine("## Candidate contact sheets");
        builder.AppendLine();
        var sheetFiles = Directory
            .EnumerateFiles(
                Path.Combine(
                    Path.GetDirectoryName(report.CropAtlasReportPath)
                    ?? string.Empty,
                    "sheets"),
                "*.png",
                SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null)
            .Cast<string>()
            .ToArray();

        foreach (var sheet in sheetFiles)
        {
            builder.AppendLine($"### {Escape(sheet)}");
            builder.AppendLine();
            builder.AppendLine(
                $"![{Escape(sheet)}](atlas/sheets/{sheet})");
            builder.AppendLine();
        }

        builder.AppendLine("## Review cases");
        builder.AppendLine();
        foreach (var cluster in report.Clusters)
        {
            builder.AppendLine($"### {Escape(cluster.ClusterId)}");
            builder.AppendLine();
            foreach (var caseId in cluster.CaseIds.Take(2))
            {
                var item = report.Cases.Single(value =>
                    value.CaseId.Equals(
                        caseId,
                        StringComparison.Ordinal));
                builder.AppendLine(
                    $"- `{Escape(item.CaseId)}` from " +
                    $"`{Escape(item.SourceFile)}`");
                foreach (var crop in item.Crops)
                {
                    builder.AppendLine(
                        $"  - {crop.Kind}: " +
                        $"[{Escape(crop.CandidateId)}]({crop.File})");
                }
            }
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string CasesCsv(SemanticEvidenceReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "caseId,sequenceNumber,sourceFile,sourceSha256,clusterId,cropCount,cropFiles");
        foreach (var item in report.Cases
                     .OrderBy(value => value.SequenceNumber)
                     .ThenBy(value => value.CaseId, StringComparer.Ordinal))
        {
            var values = new[]
            {
                item.CaseId,
                item.SequenceNumber.ToString(
                    CultureInfo.InvariantCulture),
                item.SourceFile,
                item.SourceSha256,
                item.ClusterId,
                item.Crops.Count.ToString(
                    CultureInfo.InvariantCulture),
                string.Join("|", item.Crops.Select(crop => crop.File))
            };
            builder.AppendLine(
                string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static void WriteReviewPack(
        string root,
        string zipPath)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        var includedFiles = Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path =>
                !path.Equals(zipPath, StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var relative = Path.GetRelativePath(root, path)
                    .Replace(Path.DirectorySeparatorChar, '/');
                return relative.StartsWith(
                           "crops/",
                           StringComparison.Ordinal) ||
                       relative.StartsWith(
                           "atlas/",
                           StringComparison.Ordinal) ||
                       relative.Equals(
                           "semantic-evidence.json",
                           StringComparison.Ordinal) ||
                       relative.Equals(
                           "semantic-evidence.md",
                           StringComparison.Ordinal) ||
                       relative.Equals(
                           "semantic-evidence-cases.csv",
                           StringComparison.Ordinal) ||
                       relative.Equals(
                           "semantic-truth-template.json",
                           StringComparison.Ordinal);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        using var archive = ZipFile.Open(
            zipPath,
            ZipArchiveMode.Create);
        foreach (var file in includedFiles)
        {
            var relative = Path.GetRelativePath(root, file)
                .Replace(Path.DirectorySeparatorChar, '/');
            var entry = archive.CreateEntry(
                relative,
                CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(
                1980,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero);
            using var input = File.OpenRead(file);
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ");

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";
}
