using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.CropAtlas.Models;
using PogoInventory.CropAtlas.Semantic.Models;
using PogoInventory.CropAtlas.Services;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.RegionDiscovery.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.CropAtlas.Semantic.Services;

public static class SemanticEvidenceRunner
{
    public static async Task<SemanticEvidenceReport> RunAsync(
        string inputDirectory,
        string regionReportPath,
        string cropAtlasReportPath,
        string outputDirectory,
        SemanticEvidenceOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(cropAtlasReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var inputRoot = Path.GetFullPath(inputDirectory);
        var regionPath = Path.GetFullPath(regionReportPath);
        var atlasPath = Path.GetFullPath(cropAtlasReportPath);
        var atlasRoot = Path.GetDirectoryName(atlasPath)
            ?? throw new InvalidOperationException(
                "The crop-atlas report path has no parent directory.");
        var outputRoot = Path.GetFullPath(outputDirectory);

        if (!Directory.Exists(inputRoot))
        {
            throw new DirectoryNotFoundException(inputRoot);
        }

        var regionReport = await ReadRegionReportAsync(
            regionPath,
            cancellationToken);
        var atlasReport = await ReadAtlasReportAsync(
            atlasPath,
            cancellationToken);

        if (!regionReport.Accepted || !atlasReport.Accepted)
        {
            throw new InvalidOperationException(
                "Semantic evidence requires accepted region and crop-atlas reports.");
        }

        if (atlasReport.SelectedRegions.Count == 0)
        {
            throw new InvalidOperationException(
                "The crop atlas contains no selected regions.");
        }

        Directory.CreateDirectory(outputRoot);
        var cropsRoot = Path.Combine(outputRoot, "crops");
        var atlasCopyRoot = Path.Combine(outputRoot, "atlas");
        Directory.CreateDirectory(cropsRoot);
        Directory.CreateDirectory(atlasCopyRoot);

        CopyAtlasEvidence(
            atlasRoot,
            atlasCopyRoot,
            atlasReport);

        var cases = new List<SemanticEvidenceCase>();
        foreach (var image in regionReport.Images
                     .OrderBy(item => item.SequenceNumber)
                     .ThenBy(item => item.FileName, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = ResolveInside(inputRoot, image.FileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"A semantic-evidence source image is missing: {image.FileName}.",
                    sourcePath);
            }

            var decoded = PngDecoder.Decode(
                await File.ReadAllBytesAsync(sourcePath, cancellationToken));
            var caseId = Sanitize(
                Path.GetFileNameWithoutExtension(image.FileName));
            var caseRoot = Path.Combine(cropsRoot, caseId);
            Directory.CreateDirectory(caseRoot);

            var crops = new List<SemanticEvidenceCrop>();
            foreach (var region in atlasReport.SelectedRegions
                         .OrderBy(item => item.Kind)
                         .ThenBy(item => item.CandidateId, StringComparer.Ordinal))
            {
                var rectangle = region.Region.ToPixels(
                    decoded.Width,
                    decoded.Height);
                var crop = PixelImageTransforms.Crop(
                    decoded,
                    rectangle);
                crop = PixelImageTransforms.ResizeToFit(
                    crop,
                    options.MaximumCropWidth,
                    options.MaximumCropHeight);

                var cropFileName =
                    $"{Sanitize(region.CandidateId)}.png";
                var relativePath = Path.Combine(
                        "crops",
                        caseId,
                        cropFileName)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var bytes = PngEncoder.Encode(crop);
                await File.WriteAllBytesAsync(
                    Path.Combine(
                        outputRoot,
                        relativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)),
                    bytes,
                    cancellationToken);

                crops.Add(new SemanticEvidenceCrop
                {
                    CandidateId = region.CandidateId,
                    Kind = region.Kind,
                    File = relativePath,
                    Width = crop.Width,
                    Height = crop.Height,
                    Sha256 = Convert.ToHexString(
                            SHA256.HashData(bytes))
                        .ToLowerInvariant()
                });
            }

            cases.Add(new SemanticEvidenceCase
            {
                CaseId = caseId,
                SequenceNumber = image.SequenceNumber,
                SourceFile = image.FileName,
                SourceSha256 = image.Sha256,
                ClusterId = image.ClusterId,
                Crops = crops
            });
        }

        var clusters = cases
            .GroupBy(item => item.ClusterId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SemanticEvidenceCluster
            {
                ClusterId = group.Key,
                CaseCount = group.Count(),
                Underrepresented =
                    group.Count() < options.MinimumCasesPerCluster,
                CaseIds = group
                    .OrderBy(item => item.SequenceNumber)
                    .ThenBy(item => item.CaseId, StringComparer.Ordinal)
                    .Select(item => item.CaseId)
                    .ToArray()
            })
            .ToArray();

        var underrepresented = clusters
            .Where(cluster => cluster.Underrepresented)
            .Select(cluster => cluster.ClusterId)
            .ToArray();
        var candidateKinds = atlasReport.SelectedRegions
            .Select(region => region.Kind)
            .Distinct()
            .OrderBy(kind => kind)
            .ToArray();

        var requiredKinds = new[]
        {
            RegionCandidateKind.ScreenStateDiscriminator,
            RegionCandidateKind.DynamicContent,
            RegionCandidateKind.TextDense
        };
        var hasRequiredKinds = requiredKinds.All(
            kind => candidateKinds.Contains(kind));
        var cropCount = cases.Sum(item => item.Crops.Count);
        var accepted =
            cases.Count >= options.MinimumCaseCount &&
            clusters.Length >= 2 &&
            hasRequiredKinds &&
            cropCount == cases.Count * atlasReport.SelectedRegions.Count;

        var reasons = new List<string>
        {
            accepted
                ? "Every decoded screenshot has a crop for every selected candidate region."
                : "The semantic evidence pack does not meet the required case, cluster or candidate-kind coverage."
        };

        if (underrepresented.Length == 0)
        {
            reasons.Add(
                "Every visual cluster has at least the requested number of cases.");
        }
        else
        {
            reasons.Add(
                "Additional screenshots would strengthen these visual clusters: " +
                string.Join(", ", underrepresented) + ".");
        }

        reasons.Add(
            "Automated extraction remains disabled until a truth manifest is populated and a zero-false-Complete acceptance test passes.");

        var report = new SemanticEvidenceReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = inputRoot,
            RegionReportPath = regionPath,
            CropAtlasReportPath = atlasPath,
            CaseCount = cases.Count,
            ClusterCount = clusters.Length,
            SelectedRegionCount = atlasReport.SelectedRegions.Count,
            CropCount = cropCount,
            Accepted = accepted,
            GateDetail = accepted
                ? "Accepted: the review pack contains every decoded screenshot, every selected region and every visual cluster."
                : "Rejected: the review pack is missing required cases, clusters or candidate kinds.",
            ReviewPackFile = "semantic-review-pack.zip",
            TruthTemplateFile = "semantic-truth-template.json",
            Readiness = new SemanticEvidenceReadiness
            {
                ReadyForExternalVisualReview = accepted,
                ReadyForAutomatedExtraction = false,
                NeedsMoreImages = underrepresented.Length > 0,
                UnderrepresentedClusters = underrepresented,
                Reasons = reasons,
                RecommendedNextAction = underrepresented.Length > 0
                    ? "Review the pack first, then capture additional screenshots only for the named underrepresented clusters."
                    : "Upload semantic-review-pack.zip for visual review before selecting the first extraction field."
            },
            CandidateKinds = candidateKinds,
            Clusters = clusters,
            Cases = cases
        };
        report.Validate();
        return report;
    }

    private static async Task<RegionDiscoveryReport> ReadRegionReportAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(
            path,
            cancellationToken);
        var report = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            json,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "The region-discovery report could not be read.");
        report.Validate();
        return report;
    }

    private static async Task<CropAtlasReport> ReadAtlasReportAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(
            path,
            cancellationToken);
        var report = JsonSerializer.Deserialize<CropAtlasReport>(
            json,
            CropAtlasJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "The crop-atlas report could not be read.");
        report.Validate();
        return report;
    }

    private static void CopyAtlasEvidence(
        string atlasRoot,
        string targetRoot,
        CropAtlasReport report)
    {
        CopyRelative(
            atlasRoot,
            targetRoot,
            report.OverviewFile);

        foreach (var region in report.SelectedRegions)
        {
            CopyRelative(
                atlasRoot,
                targetRoot,
                region.SheetFile);
        }
    }

    private static void CopyRelative(
        string sourceRoot,
        string targetRoot,
        string relativePath)
    {
        var source = ResolveInside(sourceRoot, relativePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                $"Crop-atlas evidence is missing: {relativePath}.",
                source);
        }

        var target = ResolveInside(targetRoot, relativePath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(target)
            ?? throw new InvalidOperationException(
                "The atlas evidence target has no parent directory."));
        File.Copy(source, target, overwrite: true);
    }

    private static string ResolveInside(
        string root,
        string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                "Semantic evidence paths must be relative.");
        }

        var normalizedRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(
            normalizedRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Semantic evidence path escapes its root directory.");
        }

        return fullPath;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var characters = value
            .Select(character =>
                invalid.Contains(character) ||
                char.IsWhiteSpace(character)
                    ? '-'
                    : character)
            .ToArray();
        var sanitized = new string(characters).Trim('-', '.');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "case"
            : sanitized;
    }
}
