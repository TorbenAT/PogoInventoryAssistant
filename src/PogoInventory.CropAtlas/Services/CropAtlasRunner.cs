using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.CropAtlas.Models;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.RegionDiscovery.Services;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.CropAtlas.Services;

public static class CropAtlasRunner
{
    public static async Task<CropAtlasReport> RunAsync(
        string inputDirectory,
        string regionReportPath,
        string outputDirectory,
        CropAtlasOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionReportPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var inputRoot = Path.GetFullPath(inputDirectory);
        var reportPath = Path.GetFullPath(regionReportPath);
        var outputRoot = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(inputRoot))
        {
            throw new DirectoryNotFoundException(inputRoot);
        }

        var regionJson = await File.ReadAllTextAsync(
            reportPath,
            cancellationToken);
        var regionReport = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            regionJson,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "The region-discovery report could not be read.");
        regionReport.Validate();
        if (!regionReport.Accepted)
        {
            throw new InvalidOperationException(
                "The crop atlas requires an accepted region-discovery report.");
        }

        var selectedCandidates = CropAtlasSelector.Select(
            regionReport.Candidates,
            options);
        var groupedImages = regionReport.Images
            .GroupBy(image => image.ClusterId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        Directory.CreateDirectory(outputRoot);
        var cropsRoot = Path.Combine(outputRoot, "crops");
        var sheetsRoot = Path.Combine(outputRoot, "sheets");
        Directory.CreateDirectory(cropsRoot);
        Directory.CreateDirectory(sheetsRoot);

        var representativeGroups = groupedImages.ToDictionary(
            group => group.Key,
            group => SelectRepresentatives(
                group.OrderBy(image => image.SequenceNumber).ToArray(),
                options.RepresentativesPerCluster),
            StringComparer.Ordinal);

        var decoded = new Dictionary<string, PixelImage>(
            StringComparer.Ordinal);
        foreach (var image in representativeGroups.Values.SelectMany(value => value))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = ResolveInside(inputRoot, image.FileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    $"A crop-atlas source image is missing: {image.FileName}.",
                    sourcePath);
            }

            decoded[image.FileName] = PngDecoder.Decode(
                await File.ReadAllBytesAsync(sourcePath, cancellationToken));
        }

        var clusters = groupedImages.Select(group =>
            new CropAtlasCluster
            {
                ClusterId = group.Key,
                ImageCount = group.Count(),
                RepresentativeFiles = representativeGroups[group.Key]
                    .Select(image => image.FileName)
                    .ToArray()
            }).ToArray();

        var overviewTiles = clusters
            .Select(cluster =>
            {
                var image = decoded[cluster.RepresentativeFiles[0]];
                return PixelImageTransforms.ResizeToFit(
                    image,
                    options.OverviewThumbnailWidth,
                    options.OverviewThumbnailHeight);
            })
            .Cast<PixelImage?>()
            .ToArray();
        var overviewColumns = Math.Min(4, overviewTiles.Length);
        var overviewRows = (int)Math.Ceiling(
            overviewTiles.Length / (double)overviewColumns);
        var paddedOverviewTiles = PadTiles(
            overviewTiles,
            overviewColumns * overviewRows);
        var overview = PixelImageTransforms.ComposeGrid(
            paddedOverviewTiles,
            overviewColumns,
            overviewRows);
        const string overviewFile = "cluster-overview.png";
        await File.WriteAllBytesAsync(
            Path.Combine(outputRoot, overviewFile),
            PngEncoder.Encode(overview),
            cancellationToken);

        var selectedRegions = new List<CropAtlasSelectedRegion>();
        var crops = new List<CropAtlasCrop>();

        foreach (var candidate in selectedCandidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidateFolder = Sanitize(candidate.Id);
            var candidateRoot = Path.Combine(cropsRoot, candidateFolder);
            Directory.CreateDirectory(candidateRoot);

            var sheetTiles = new List<PixelImage?>();
            foreach (var cluster in clusters)
            {
                var representatives = representativeGroups[cluster.ClusterId];
                for (var representativeIndex = 0;
                     representativeIndex < options.RepresentativesPerCluster;
                     representativeIndex++)
                {
                    if (representativeIndex >= representatives.Count)
                    {
                        sheetTiles.Add(null);
                        continue;
                    }

                    var image = representatives[representativeIndex];
                    var source = decoded[image.FileName];
                    var rectangle = candidate.Region.ToPixels(
                        source.Width,
                        source.Height);
                    var crop = PixelImageTransforms.Crop(source, rectangle);
                    crop = PixelImageTransforms.ResizeToFit(
                        crop,
                        options.MaximumCropWidth,
                        options.MaximumCropHeight);

                    var cropFileName =
                        $"{Sanitize(cluster.ClusterId)}-" +
                        $"{representativeIndex + 1:D2}-" +
                        $"{Path.GetFileNameWithoutExtension(image.FileName)}.png";
                    var cropRelativePath = Path.Combine(
                            "crops",
                            candidateFolder,
                            cropFileName)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var cropBytes = PngEncoder.Encode(crop);
                    await File.WriteAllBytesAsync(
                        Path.Combine(outputRoot, cropRelativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)),
                        cropBytes,
                        cancellationToken);

                    crops.Add(new CropAtlasCrop
                    {
                        CandidateId = candidate.Id,
                        Kind = candidate.Kind,
                        ClusterId = cluster.ClusterId,
                        RepresentativeIndex = representativeIndex,
                        SourceFile = image.FileName,
                        CropFile = cropRelativePath,
                        Width = crop.Width,
                        Height = crop.Height,
                        Sha256 = Convert.ToHexString(
                                SHA256.HashData(cropBytes))
                            .ToLowerInvariant()
                    });
                    sheetTiles.Add(crop);
                }
            }

            var sheet = PixelImageTransforms.ComposeGrid(
                sheetTiles,
                options.RepresentativesPerCluster,
                clusters.Length);
            var sheetRelativePath = $"sheets/{candidateFolder}.png";
            await File.WriteAllBytesAsync(
                Path.Combine(
                    outputRoot,
                    sheetRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)),
                PngEncoder.Encode(sheet),
                cancellationToken);

            selectedRegions.Add(new CropAtlasSelectedRegion
            {
                CandidateId = candidate.Id,
                Kind = candidate.Kind,
                Region = candidate.Region,
                Score = candidate.AverageScore,
                SourceReason = candidate.ProvisionalReason,
                SheetFile = sheetRelativePath
            });
        }

        var readiness = BuildReadiness(
            clusters,
            selectedRegions,
            options);
        var requiredKindsPresent =
            readiness.HasStateDiscriminatorEvidence &&
            readiness.HasDynamicContentEvidence &&
            readiness.HasTextDenseEvidence;
        var minimumCropCount = selectedRegions.Count * clusters.Length;
        var accepted =
            clusters.Length >= 2 &&
            requiredKindsPresent &&
            readiness.AllClustersRepresented &&
            crops.Count >= minimumCropCount;

        var report = new CropAtlasReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = inputRoot,
            RegionReportPath = reportPath,
            SourceImageCount = regionReport.DecodedCount,
            ClusterCount = clusters.Length,
            SelectedRegionCount = selectedRegions.Count,
            CropCount = crops.Count,
            OverviewFile = overviewFile,
            Accepted = accepted,
            GateDetail = accepted
                ? "Accepted: every visual cluster has representative evidence and the atlas contains state, dynamic-content and text-dense candidate crops."
                : "Rejected: the atlas is missing a required candidate kind, cluster representative or crop.",
            Readiness = readiness,
            Clusters = clusters,
            SelectedRegions = selectedRegions,
            Crops = crops
        };
        report.Validate();
        return report;
    }

    private static CropAtlasReadiness BuildReadiness(
        IReadOnlyList<CropAtlasCluster> clusters,
        IReadOnlyList<CropAtlasSelectedRegion> selectedRegions,
        CropAtlasOptions options)
    {
        var underrepresented = clusters
            .Where(cluster =>
                cluster.ImageCount < options.RepresentativesPerCluster)
            .Select(cluster => cluster.ClusterId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var hasState = selectedRegions.Any(region =>
            region.Kind == RegionCandidateKind.ScreenStateDiscriminator);
        var hasDynamic = selectedRegions.Any(region =>
            region.Kind == RegionCandidateKind.DynamicContent);
        var hasText = selectedRegions.Any(region =>
            region.Kind == RegionCandidateKind.TextDense);
        var allRepresented = clusters.All(cluster =>
            cluster.RepresentativeFiles.Count > 0);
        var reasons = new List<string>();

        if (underrepresented.Length > 0)
        {
            reasons.Add(
                "More screenshots are useful for these visual clusters: " +
                string.Join(", ", underrepresented) + ".");
        }
        else
        {
            reasons.Add(
                "Every visual cluster has the requested number of representative screenshots.");
        }

        if (hasState && hasDynamic && hasText)
        {
            reasons.Add(
                "The atlas contains candidate evidence for screen state, changing Pokémon content and text-dense areas.");
        }
        else
        {
            reasons.Add(
                "At least one required candidate evidence kind is missing.");
        }

        return new CropAtlasReadiness
        {
            HasStateDiscriminatorEvidence = hasState,
            HasDynamicContentEvidence = hasDynamic,
            HasTextDenseEvidence = hasText,
            AllClustersRepresented = allRepresented,
            ReadyForSemanticExperiments =
                hasState && hasDynamic && hasText && allRepresented,
            NeedsMoreImages = underrepresented.Length > 0,
            UnderrepresentedClusters = underrepresented,
            Reasons = reasons
        };
    }

    private static IReadOnlyList<RegionDiscoveryImage> SelectRepresentatives(
        IReadOnlyList<RegionDiscoveryImage> images,
        int maximum)
    {
        if (images.Count <= maximum)
        {
            return images.ToArray();
        }

        if (maximum == 1)
        {
            return new[] { images[0] };
        }

        var selected = new List<RegionDiscoveryImage>();
        for (var index = 0; index < maximum; index++)
        {
            var position = (int)Math.Round(
                index * (images.Count - 1d) / (maximum - 1d));
            var image = images[position];
            if (!selected.Any(item =>
                item.FileName.Equals(
                    image.FileName,
                    StringComparison.Ordinal)))
            {
                selected.Add(image);
            }
        }

        return selected;
    }

    private static PixelImage?[] PadTiles(
        IReadOnlyList<PixelImage?> tiles,
        int count)
    {
        var result = new PixelImage?[count];
        for (var index = 0; index < tiles.Count; index++)
        {
            result[index] = tiles[index];
        }

        return result;
    }

    private static string ResolveInside(
        string root,
        string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                "Crop atlas source paths must be relative.");
        }

        var fullPath = Path.GetFullPath(Path.Combine(
            root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Crop atlas source path escapes the input directory.");
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
            ? "region"
            : sanitized;
    }
}
