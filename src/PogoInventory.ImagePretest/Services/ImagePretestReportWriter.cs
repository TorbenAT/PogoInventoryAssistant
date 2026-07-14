using System.Globalization;
using System.Text;
using System.Text.Json;
using PogoInventory.ImagePretest.Models;

namespace PogoInventory.ImagePretest.Services;

public static class ImagePretestReportWriter
{
    public static async Task WriteAsync(
        ImagePretestReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        report.Validate();
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);

        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-image-pretest.json"),
            JsonSerializer.Serialize(
                report,
                ImagePretestJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-image-pretest.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-images.csv"),
            ImagesCsv(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-similarity.csv"),
            SimilarityCsv(report),
            cancellationToken);
    }

    private static string Markdown(ImagePretestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# iPhone image pretest");
        builder.AppendLine();
        builder.AppendLine($"- Images: {report.ImageCount}");
        builder.AppendLine($"- Decoded: {report.DecodedCount}");
        builder.AppendLine($"- Failed: {report.FailedCount}");
        builder.AppendLine($"- Portrait: {report.PortraitCount}");
        builder.AppendLine($"- Geometry groups: {report.GeometryGroupCount}");
        builder.AppendLine($"- Distinct file hashes: {report.DistinctFileHashCount}");
        builder.AppendLine($"- Exact duplicate pairs: {report.ExactDuplicatePairCount}");
        builder.AppendLine($"- Near-duplicate pairs: {report.NearDuplicatePairCount}");
        builder.AppendLine($"- Visual clusters: {report.ClusterCount}");
        builder.AppendLine($"- Accepted: {report.Accepted}");
        builder.AppendLine($"- Gate: {report.GateDetail}");
        builder.AppendLine();

        if (report.Warnings.Count > 0)
        {
            builder.AppendLine("## Warnings");
            builder.AppendLine();
            foreach (var warning in report.Warnings)
            {
                builder.AppendLine($"- {EscapeMarkdown(warning)}");
            }
            builder.AppendLine();
        }

        builder.AppendLine("## Images");
        builder.AppendLine();
        builder.AppendLine("| # | File | Geometry | Bytes | SHA-256 | Status |");
        builder.AppendLine("|---:|---|---|---:|---|---|");
        foreach (var image in report.Images)
        {
            builder.AppendLine(
                $"| {image.SequenceNumber} | {EscapeMarkdown(image.FileName)} | " +
                $"{image.GeometryKey} | {image.LengthBytes} | " +
                $"{ShortHash(image.Sha256)} | " +
                $"{(image.Decoded ? "Decoded" : EscapeMarkdown(image.ErrorCode ?? "Failed"))} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Visual clusters");
        builder.AppendLine();
        builder.AppendLine("| Cluster | Representative | Count | Members |");
        builder.AppendLine("|---|---|---:|---|");
        foreach (var cluster in report.Clusters)
        {
            builder.AppendLine(
                $"| {cluster.Id} | {EscapeMarkdown(cluster.RepresentativeFileName)} | " +
                $"{cluster.Count} | {EscapeMarkdown(string.Join(", ", cluster.Members))} |");
        }

        return builder.ToString();
    }

    private static string ImagesCsv(ImagePretestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "sequence,fileName,relativePath,sha256,lengthBytes,width,height,aspectRatio,orientation,geometry,visualFingerprintSha256,errorCode,errorDetail");
        foreach (var item in report.Images)
        {
            var values = new[]
            {
                item.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                item.FileName,
                item.RelativePath,
                item.Sha256,
                item.LengthBytes.ToString(CultureInfo.InvariantCulture),
                item.Width.ToString(CultureInfo.InvariantCulture),
                item.Height.ToString(CultureInfo.InvariantCulture),
                item.AspectRatio.ToString("F6", CultureInfo.InvariantCulture),
                item.Orientation.ToString(),
                item.GeometryKey,
                item.VisualFingerprintSha256,
                item.ErrorCode ?? string.Empty,
                item.ErrorDetail ?? string.Empty
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string SimilarityCsv(ImagePretestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "firstFile,secondFile,similarity,exactDuplicate,nearDuplicate,consecutive");
        foreach (var pair in report.SimilarityPairs)
        {
            var values = new[]
            {
                pair.FirstFileName,
                pair.SecondFileName,
                pair.Similarity.ToString("F6", CultureInfo.InvariantCulture),
                pair.ExactDuplicate.ToString(),
                pair.NearDuplicate.ToString(),
                pair.Consecutive.ToString()
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static string EscapeMarkdown(string value) =>
        value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string ShortHash(string hash) =>
        hash.Length >= 12 ? hash[..12] : hash;
}
