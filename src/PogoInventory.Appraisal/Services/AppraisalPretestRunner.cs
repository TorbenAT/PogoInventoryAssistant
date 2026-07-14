using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.RegionDiscovery.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Appraisal.Services;

public static class AppraisalPretestRunner
{
    public static async Task<AppraisalPretestReport> RunAsync(
        string inputDirectory,
        string profilePath,
        string outputDirectory,
        AppraisalPretestOptions options,
        string? regionReportPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var inputRoot = Path.GetFullPath(inputDirectory);
        var fullProfilePath = Path.GetFullPath(profilePath);
        var outputRoot = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(inputRoot))
        {
            throw new DirectoryNotFoundException(inputRoot);
        }

        var profile = await AppraisalProfileLoader.LoadAsync(
            fullProfilePath,
            cancellationToken);
        var clusterMap = await LoadClusterMapAsync(
            regionReportPath,
            cancellationToken);
        var analyzer = new AppraisalAnalyzer();
        var imagePaths = Directory
            .EnumerateFiles(
                inputRoot,
                "*.png",
                SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        var diagnosticsRoot = Path.Combine(outputRoot, "diagnostics");
        Directory.CreateDirectory(diagnosticsRoot);

        var results = new List<AppraisalImageResult>();
        for (var index = 0; index < imagePaths.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = imagePaths[index];
            var fileName = Path.GetFileName(path);
            var clusterId = clusterMap.TryGetValue(
                fileName,
                out var mappedClusterId)
                ? mappedClusterId
                : null;
            var bytes = await File.ReadAllBytesAsync(
                path,
                cancellationToken);
            var hash = Convert.ToHexString(
                    SHA256.HashData(bytes))
                .ToLowerInvariant();

            try
            {
                var image = PngDecoder.Decode(bytes);
                var analysis = analyzer.Analyze(image, profile);
                string? overlayFile = null;

                if (analysis.IsAppraisal)
                {
                    overlayFile =
                        $"diagnostics/{Path.GetFileNameWithoutExtension(fileName)}-overlay.png";
                    var overlay = AppraisalImageDiagnostics.DrawOverlay(
                        image,
                        analysis);
                    await File.WriteAllBytesAsync(
                        Path.Combine(
                            outputRoot,
                            overlayFile.Replace(
                                '/',
                                Path.DirectorySeparatorChar)),
                        PngEncoder.Encode(overlay),
                        cancellationToken);

                    foreach (var bar in analysis.Bars)
                    {
                        var crop = AppraisalImageDiagnostics.Crop(
                            image,
                            bar.Region);
                        var cropFile =
                            $"{Path.GetFileNameWithoutExtension(fileName)}-" +
                            $"{bar.Kind.ToString().ToLowerInvariant()}.png";
                        await File.WriteAllBytesAsync(
                            Path.Combine(diagnosticsRoot, cropFile),
                            PngEncoder.Encode(crop),
                            cancellationToken);
                    }
                }

                results.Add(new AppraisalImageResult
                {
                    FileName = fileName,
                    SequenceNumber = index,
                    Sha256 = hash,
                    Decoded = true,
                    Width = image.Width,
                    Height = image.Height,
                    ClusterId = clusterId,
                    Analysis = analysis,
                    OverlayFile = overlayFile
                });
            }
            catch (Exception exception) when (
                exception is InvalidDataException or
                NotSupportedException or
                ArgumentException or
                OverflowException)
            {
                results.Add(new AppraisalImageResult
                {
                    FileName = fileName,
                    SequenceNumber = index,
                    Sha256 = hash,
                    Decoded = false,
                    ErrorCode = exception.GetType().Name,
                    ErrorDetail = exception.Message,
                    ClusterId = clusterId
                });
            }
        }

        var candidates = results
            .Where(item => item.Analysis?.IsAppraisal == true)
            .ToArray();
        var clusterGroups = candidates
            .Where(item => !string.IsNullOrWhiteSpace(item.ClusterId))
            .GroupBy(item => item.ClusterId!, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var dominantCluster = clusterGroups.FirstOrDefault();
        var dominantShare = dominantCluster is null
            ? 0
            : dominantCluster.Count() /
              (double)Math.Max(1, candidates.Count(item =>
                  !string.IsNullOrWhiteSpace(item.ClusterId)));
        var distinctCandidateClusters = clusterGroups.Length;

        var warnings = new List<string>();
        if (results.Any(item => !item.Decoded))
        {
            warnings.Add(
                $"{results.Count(item => !item.Decoded)} PNG file(s) were retained as decoder diagnostics.");
        }

        if (regionReportPath is null)
        {
            warnings.Add(
                "No region report was supplied, so candidate cluster concentration was not evaluated.");
        }

        if (clusterGroups.Length > 0 &&
            dominantShare < options.MinimumDominantClusterShare)
        {
            warnings.Add(
                "Appraisal candidates are spread across visual clusters; inspect diagnostic overlays before changing the profile.");
        }

        var decodedCount = results.Count(item => item.Decoded);
        var completeCount = results.Count(item =>
            item.Analysis?.IsComplete == true);
        var clusterGate =
            regionReportPath is null ||
            clusterGroups.Length == 0 ||
            dominantShare >= options.MinimumDominantClusterShare;
        var accepted =
            decodedCount >= options.MinimumDecodedImages &&
            candidates.Length >= options.MinimumCandidateImages &&
            completeCount == 0 &&
            clusterGate;

        var report = new AppraisalPretestReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = inputRoot,
            ProfilePath = fullProfilePath,
            RegionReportPath = regionReportPath is null
                ? null
                : Path.GetFullPath(regionReportPath),
            ImageCount = results.Count,
            DecodedCount = decodedCount,
            CandidateCount = candidates.Length,
            CompleteCount = completeCount,
            DistinctCandidateClusterCount = distinctCandidateClusters,
            DominantCandidateCluster = dominantCluster?.Key,
            DominantCandidateClusterShare = dominantShare,
            Accepted = accepted,
            GateDetail = accepted
                ? "Accepted: the unverified normalised profile consistently found appraisal candidates without producing Complete IV observations."
                : "Rejected: decoded-image count, appraisal-candidate count, cluster concentration or zero-Complete safety did not pass.",
            Warnings = warnings,
            Images = results
        };
        report.Validate();
        return report;
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadClusterMapAsync(
        string? regionReportPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(regionReportPath))
        {
            return new Dictionary<string, string>(
                StringComparer.Ordinal);
        }

        var fullPath = Path.GetFullPath(regionReportPath);
        var json = await File.ReadAllTextAsync(
            fullPath,
            cancellationToken);
        var report = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            json,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "Could not read the region-discovery report.");
        report.Validate();
        return report.Images.ToDictionary(
            item => item.FileName,
            item => item.ClusterId,
            StringComparer.Ordinal);
    }
}
