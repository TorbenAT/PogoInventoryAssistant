using System.Globalization;
using System.Text;
using System.Text.Json;
using PogoInventory.RegionDiscovery.Models;

namespace PogoInventory.RegionDiscovery.Services;

public static class RegionDiscoveryReportWriter
{
    public static async Task WriteAsync(
        RegionDiscoveryReport report,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        report.Validate();
        var root = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(root);

        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-region-discovery.json"),
            JsonSerializer.Serialize(
                report,
                RegionDiscoveryJson.CreateOptions(writeIndented: true)),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-region-discovery.md"),
            Markdown(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-region-cells.csv"),
            CellsCsv(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-region-candidates.csv"),
            CandidatesCsv(report),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(root, "iphone-region-image-clusters.csv"),
            ImagesCsv(report),
            cancellationToken);
    }

    private static string Markdown(RegionDiscoveryReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# iPhone visual region discovery");
        builder.AppendLine();
        builder.AppendLine($"- Images: {report.ImageCount}");
        builder.AppendLine($"- Decoded: {report.DecodedCount}");
        builder.AppendLine($"- Failed: {report.FailedCount}");
        builder.AppendLine($"- Decode rate: {report.DecodeRate:P1}");
        builder.AppendLine($"- Geometry groups: {report.GeometryGroupCount}");
        builder.AppendLine($"- Visual clusters: {report.ClusterCount}");
        builder.AppendLine($"- Grid: {report.GridColumns} x {report.GridRows}");
        builder.AppendLine($"- Cells: {report.CellCount}");
        builder.AppendLine($"- Candidates: {report.Candidates.Count}");
        builder.AppendLine($"- Accepted: {report.Accepted}");
        builder.AppendLine($"- Gate: {EscapeMarkdown(report.GateDetail)}");
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

        builder.AppendLine("## Image clusters");
        builder.AppendLine();
        builder.AppendLine("| # | File | Cluster | Geometry | SHA-256 |");
        builder.AppendLine("|---:|---|---|---|---|");
        foreach (var image in report.Images)
        {
            builder.AppendLine(
                $"| {image.SequenceNumber} | {EscapeMarkdown(image.FileName)} | " +
                $"{EscapeMarkdown(image.ClusterId)} | {image.Width}x{image.Height} | " +
                $"{ShortHash(image.Sha256)} |");
        }
        builder.AppendLine();

        builder.AppendLine("## Provisional candidates");
        builder.AppendLine();
        builder.AppendLine(
            "These labels describe measured visual behaviour. They are not OCR results and are not confirmed Pokémon fields.");
        builder.AppendLine();
        builder.AppendLine("| Candidate | Kind | Region x,y,w,h | Avg | Max | Cells | Reason |");
        builder.AppendLine("|---|---|---|---:|---:|---:|---|");
        foreach (var candidate in report.Candidates)
        {
            builder.AppendLine(
                $"| {candidate.Id} | {candidate.Kind} | " +
                $"{FormatRegion(candidate.Region)} | " +
                $"{candidate.AverageScore:F4} | {candidate.MaximumScore:F4} | " +
                $"{candidate.CellCount} | {EscapeMarkdown(candidate.ProvisionalReason)} |");
        }
        builder.AppendLine();

        builder.AppendLine("## Top grid cells");
        builder.AppendLine();
        builder.AppendLine(
            "| Row | Col | Region | Stable | State | Dynamic | Text | Global var | Consecutive var | Within cluster | Between clusters | Edge |");
        builder.AppendLine(
            "|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var cell in report.Cells
                     .OrderByDescending(cell => Math.Max(
                         Math.Max(cell.StableChromeScore, cell.ScreenStateScore),
                         Math.Max(cell.DynamicContentScore, cell.TextDensityScore)))
                     .Take(24))
        {
            builder.AppendLine(
                $"| {cell.Row} | {cell.Column} | {FormatRegion(cell.Region)} | " +
                $"{cell.StableChromeScore:F4} | {cell.ScreenStateScore:F4} | " +
                $"{cell.DynamicContentScore:F4} | {cell.TextDensityScore:F4} | " +
                $"{cell.GlobalVariation:F4} | {cell.ConsecutiveVariation:F4} | " +
                $"{cell.WithinClusterVariation:F4} | {cell.BetweenClusterSeparation:F4} | " +
                $"{cell.MeanEdgeDensity:F4} |");
        }

        return builder.ToString();
    }

    private static string CellsCsv(RegionDiscoveryReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "row,column,x,y,width,height,meanLuminance,meanEdgeDensity,globalVariation,consecutiveVariation,withinClusterVariation,betweenClusterSeparation,stableChromeScore,screenStateScore,dynamicContentScore,textDensityScore");
        foreach (var cell in report.Cells)
        {
            var values = new[]
            {
                cell.Row.ToString(CultureInfo.InvariantCulture),
                cell.Column.ToString(CultureInfo.InvariantCulture),
                F(cell.Region.X),
                F(cell.Region.Y),
                F(cell.Region.Width),
                F(cell.Region.Height),
                F(cell.MeanLuminance),
                F(cell.MeanEdgeDensity),
                F(cell.GlobalVariation),
                F(cell.ConsecutiveVariation),
                F(cell.WithinClusterVariation),
                F(cell.BetweenClusterSeparation),
                F(cell.StableChromeScore),
                F(cell.ScreenStateScore),
                F(cell.DynamicContentScore),
                F(cell.TextDensityScore)
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string CandidatesCsv(RegionDiscoveryReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "id,kind,x,y,width,height,averageScore,maximumScore,cellCount,provisionalReason");
        foreach (var candidate in report.Candidates)
        {
            var values = new[]
            {
                candidate.Id,
                candidate.Kind.ToString(),
                F(candidate.Region.X),
                F(candidate.Region.Y),
                F(candidate.Region.Width),
                F(candidate.Region.Height),
                F(candidate.AverageScore),
                F(candidate.MaximumScore),
                candidate.CellCount.ToString(CultureInfo.InvariantCulture),
                candidate.ProvisionalReason
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string ImagesCsv(RegionDiscoveryReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("sequence,fileName,clusterId,width,height,sha256");
        foreach (var image in report.Images)
        {
            var values = new[]
            {
                image.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                image.FileName,
                image.ClusterId,
                image.Width.ToString(CultureInfo.InvariantCulture),
                image.Height.ToString(CultureInfo.InvariantCulture),
                image.Sha256
            };
            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }
        return builder.ToString();
    }

    private static string FormatRegion(PogoInventory.Vision.Models.NormalizedRegion region) =>
        $"{region.X:F4},{region.Y:F4},{region.Width:F4},{region.Height:F4}";

    private static string F(double value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    private static string EscapeCsv(string value) =>
        $"\"{value.Replace("\"", "\"\"")}\"";

    private static string EscapeMarkdown(string value) =>
        value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string ShortHash(string hash) =>
        hash.Length >= 12 ? hash[..12] : hash;
}
