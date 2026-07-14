using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PogoInventory.Appraisal.Models;

namespace PogoInventory.Appraisal.Services;

public static class AppraisalPretestReportWriter
{
    public static async Task WriteAsync(
        AppraisalPretestReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        report.Validate();

        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);

        await File.WriteAllTextAsync(
            Path.Combine(root, "appraisal-pretest.json"),
            JsonSerializer.Serialize(
                report,
                AppraisalJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "appraisal-pretest.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "appraisal-measurements.csv"),
            Csv(report),
            cancellationToken);

        var zipPath = Path.Combine(root, "appraisal-review-pack.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(
            zipPath,
            ZipArchiveMode.Create);
        foreach (var path in Directory
                     .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => !path.Equals(
                         zipPath,
                         StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, path)
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
            using var source = File.OpenRead(path);
            using var target = entry.Open();
            source.CopyTo(target);
        }
    }

    private static string Markdown(AppraisalPretestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Appraisal visual pretest");
        builder.AppendLine();
        builder.AppendLine($"- Images: {report.ImageCount}");
        builder.AppendLine($"- Decoded: {report.DecodedCount}");
        builder.AppendLine($"- Appraisal candidates: {report.CandidateCount}");
        builder.AppendLine($"- Complete observations: {report.CompleteCount}");
        builder.AppendLine(
            $"- Dominant candidate cluster: " +
            $"{report.DominantCandidateCluster ?? "Unavailable"}");
        builder.AppendLine(
            $"- Dominant cluster share: " +
            $"{report.DominantCandidateClusterShare:P1}");
        builder.AppendLine($"- Accepted: {report.Accepted}");
        builder.AppendLine($"- Gate: {Escape(report.GateDetail)}");
        builder.AppendLine();
        builder.AppendLine(
            "Estimated IV values are diagnostic candidates only. " +
            "An unverified profile cannot produce Complete observations.");
        builder.AppendLine();

        if (report.Warnings.Count > 0)
        {
            builder.AppendLine("## Warnings");
            builder.AppendLine();
            foreach (var warning in report.Warnings)
            {
                builder.AppendLine($"- {Escape(warning)}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Candidate images");
        builder.AppendLine();
        builder.AppendLine(
            "| File | Cluster | Score | Attack | Defense | HP | Overlay |");
        builder.AppendLine("|---|---|---:|---:|---:|---:|---|");
        foreach (var item in report.Images.Where(item =>
                     item.Analysis?.IsAppraisal == true))
        {
            var analysis = item.Analysis!;
            builder.AppendLine(
                $"| {Escape(item.FileName)} | " +
                $"{Escape(item.ClusterId ?? "Unknown")} | " +
                $"{analysis.CandidateScore:F3} | " +
                $"{analysis.AttackIv?.ToString(CultureInfo.InvariantCulture) ?? "?"} | " +
                $"{analysis.DefenseIv?.ToString(CultureInfo.InvariantCulture) ?? "?"} | " +
                $"{analysis.HpIv?.ToString(CultureInfo.InvariantCulture) ?? "?"} | " +
                $"{(item.OverlayFile is null ? string.Empty : $"[view]({item.OverlayFile})")} |");
        }

        return builder.ToString();
    }

    private static string Csv(AppraisalPretestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "fileName,decoded,clusterId,status,score,confidence,attackIv,attackFill,attackConfidence,defenseIv,defenseFill,defenseConfidence,hpIv,hpFill,hpConfidence,errorCode,errorDetail");
        foreach (var item in report.Images)
        {
            var analysis = item.Analysis;
            var attack = analysis?.Bars.SingleOrDefault(bar =>
                bar.Kind == AppraisalBarKind.Attack);
            var defense = analysis?.Bars.SingleOrDefault(bar =>
                bar.Kind == AppraisalBarKind.Defense);
            var hp = analysis?.Bars.SingleOrDefault(bar =>
                bar.Kind == AppraisalBarKind.Hp);
            var values = new[]
            {
                item.FileName,
                item.Decoded.ToString(CultureInfo.InvariantCulture),
                item.ClusterId ?? string.Empty,
                analysis?.Status.ToString() ?? string.Empty,
                analysis?.CandidateScore.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                analysis?.Confidence.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                attack?.EstimatedIv?.ToString(
                    CultureInfo.InvariantCulture) ?? string.Empty,
                attack?.FillFraction.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                attack?.Confidence.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                defense?.EstimatedIv?.ToString(
                    CultureInfo.InvariantCulture) ?? string.Empty,
                defense?.FillFraction.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                defense?.Confidence.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                hp?.EstimatedIv?.ToString(
                    CultureInfo.InvariantCulture) ?? string.Empty,
                hp?.FillFraction.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                hp?.Confidence.ToString(
                    "F6",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                item.ErrorCode ?? string.Empty,
                item.ErrorDetail ?? string.Empty
            };
            builder.AppendLine(
                string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string Escape(string value) =>
        value.Replace("|", "\\|")
            .Replace("\r", " ")
            .Replace("\n", " ");

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";
}
