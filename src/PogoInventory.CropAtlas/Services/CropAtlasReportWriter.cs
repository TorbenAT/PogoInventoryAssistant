using System.Globalization;
using System.Text;
using System.Text.Json;
using PogoInventory.CropAtlas.Models;

namespace PogoInventory.CropAtlas.Services;

public static class CropAtlasReportWriter
{
    public static async Task WriteAsync(
        CropAtlasReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        report.Validate();

        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);

        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-crop-atlas.json"),
            JsonSerializer.Serialize(
                report,
                CropAtlasJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-crop-atlas.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-crop-atlas-regions.csv"),
            RegionsCsv(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-crop-atlas-crops.csv"),
            CropsCsv(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-crop-atlas-clusters.csv"),
            ClustersCsv(report),
            cancellationToken);
    }

    private static string Markdown(CropAtlasReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# iPhone crop atlas");
        builder.AppendLine();
        builder.AppendLine($"- Source images: {report.SourceImageCount}");
        builder.AppendLine($"- Visual clusters: {report.ClusterCount}");
        builder.AppendLine($"- Selected regions: {report.SelectedRegionCount}");
        builder.AppendLine($"- Generated crops: {report.CropCount}");
        builder.AppendLine($"- Accepted: {report.Accepted}");
        builder.AppendLine($"- Semantic experiments ready: {report.Readiness.ReadyForSemanticExperiments}");
        builder.AppendLine($"- More images indicated: {report.Readiness.NeedsMoreImages}");
        builder.AppendLine($"- Gate: {Escape(report.GateDetail)}");
        builder.AppendLine();
        builder.AppendLine(
            "The images below are derived evidence. Candidate labels remain provisional and are not OCR or IV results.");
        builder.AppendLine();
        builder.AppendLine("## Cluster overview");
        builder.AppendLine();
        builder.AppendLine($"![Cluster overview]({report.OverviewFile})");
        builder.AppendLine();

        builder.AppendLine("## Cluster coverage");
        builder.AppendLine();
        builder.AppendLine("| Cluster | Images | Representatives |");
        builder.AppendLine("|---|---:|---|");
        foreach (var cluster in report.Clusters)
        {
            builder.AppendLine(
                $"| {Escape(cluster.ClusterId)} | {cluster.ImageCount} | " +
                $"{Escape(string.Join(", ", cluster.RepresentativeFiles))} |");
        }
        builder.AppendLine();

        builder.AppendLine("## Readiness");
        builder.AppendLine();
        foreach (var reason in report.Readiness.Reasons)
        {
            builder.AppendLine($"- {Escape(reason)}");
        }
        builder.AppendLine();

        builder.AppendLine("## Candidate sheets");
        builder.AppendLine();
        foreach (var region in report.SelectedRegions)
        {
            builder.AppendLine(
                $"### {Escape(region.CandidateId)} — {region.Kind}");
            builder.AppendLine();
            builder.AppendLine(
                $"Region: `{F(region.Region.X)},{F(region.Region.Y)}," +
                $"{F(region.Region.Width)},{F(region.Region.Height)}`  ");
            builder.AppendLine($"Score: `{region.Score:F4}`  ");
            builder.AppendLine($"Reason: {Escape(region.SourceReason)}");
            builder.AppendLine();
            builder.AppendLine(
                $"![{Escape(region.CandidateId)}]({region.SheetFile})");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string RegionsCsv(CropAtlasReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "candidateId,kind,x,y,width,height,score,sheetFile,sourceReason");
        foreach (var region in report.SelectedRegions)
        {
            var values = new[]
            {
                region.CandidateId,
                region.Kind.ToString(),
                F(region.Region.X),
                F(region.Region.Y),
                F(region.Region.Width),
                F(region.Region.Height),
                F(region.Score),
                region.SheetFile,
                region.SourceReason
            };
            builder.AppendLine(
                string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string CropsCsv(CropAtlasReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "candidateId,kind,clusterId,representativeIndex,sourceFile,cropFile,width,height,sha256");
        foreach (var crop in report.Crops)
        {
            var values = new[]
            {
                crop.CandidateId,
                crop.Kind.ToString(),
                crop.ClusterId,
                crop.RepresentativeIndex.ToString(
                    CultureInfo.InvariantCulture),
                crop.SourceFile,
                crop.CropFile,
                crop.Width.ToString(CultureInfo.InvariantCulture),
                crop.Height.ToString(CultureInfo.InvariantCulture),
                crop.Sha256
            };
            builder.AppendLine(
                string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string ClustersCsv(CropAtlasReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "clusterId,imageCount,representativeCount,representativeFiles,needsMoreImages");
        foreach (var cluster in report.Clusters)
        {
            var values = new[]
            {
                cluster.ClusterId,
                cluster.ImageCount.ToString(CultureInfo.InvariantCulture),
                cluster.RepresentativeFiles.Count.ToString(
                    CultureInfo.InvariantCulture),
                string.Join("|", cluster.RepresentativeFiles),
                report.Readiness.UnderrepresentedClusters.Contains(
                    cluster.ClusterId,
                    StringComparer.Ordinal)
                    .ToString(CultureInfo.InvariantCulture)
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

    private static string F(double value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);
}
