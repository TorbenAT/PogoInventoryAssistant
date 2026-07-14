using PogoInventory.ImagePretest.Models;
using PogoInventory.ImagePretest.Services;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.RegionDiscovery.Services;

public static class RegionDiscoveryRunner
{
    public static async Task<RegionDiscoveryReport> RunAsync(
        string inputDirectory,
        RegionDiscoveryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RegionDiscoveryOptions();
        options.Validate();

        var pretest = await ImagePretestRunner.RunAsync(
            inputDirectory,
            new ImagePretestOptions
            {
                MinimumImageCount = options.MinimumDecodedImages,
                MinimumDecodeRate = options.MinimumDecodeRate,
                NearDuplicateThreshold = options.NearDuplicateThreshold,
                ClusterThreshold = options.ClusterThreshold
            },
            cancellationToken);

        var root = Path.GetFullPath(inputDirectory);
        var clusterByFile = pretest.Clusters
            .SelectMany(cluster => cluster.Members.Select(member => new
            {
                FileName = member,
                cluster.Id
            }))
            .ToDictionary(
                item => item.FileName,
                item => item.Id,
                StringComparer.Ordinal);

        var decoded = new List<WorkingImage>();
        foreach (var item in pretest.Images
                     .Where(item => item.Decoded)
                     .OrderBy(item => item.SequenceNumber))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(
                root,
                item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var image = PngDecoder.Decode(bytes);
            decoded.Add(new WorkingImage(
                new RegionDiscoveryImage
                {
                    SequenceNumber = item.SequenceNumber,
                    FileName = item.FileName,
                    Sha256 = item.Sha256,
                    ClusterId = clusterByFile.TryGetValue(item.FileName, out var clusterId)
                        ? clusterId
                        : "unclustered",
                    Width = image.Width,
                    Height = image.Height
                },
                image));
        }

        var cells = BuildCells(decoded, options, cancellationToken);
        var candidates = RegionCandidateBuilder.Build(cells, options);
        var accepted = pretest.Accepted &&
            decoded.Count >= options.MinimumDecodedImages &&
            pretest.GeometryGroupCount == 1 &&
            pretest.ClusterCount >= 2 &&
            cells.Count == options.GridColumns * options.GridRows;

        var warnings = pretest.Warnings
            .Concat(BuildWarnings(pretest, candidates))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var report = new RegionDiscoveryReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = root,
            ImageCount = pretest.ImageCount,
            DecodedCount = pretest.DecodedCount,
            FailedCount = pretest.FailedCount,
            DecodeRate = pretest.DecodeRate,
            GeometryGroupCount = pretest.GeometryGroupCount,
            ClusterCount = pretest.ClusterCount,
            GridColumns = options.GridColumns,
            GridRows = options.GridRows,
            CellCount = cells.Count,
            Accepted = accepted,
            GateDetail = GateDetail(pretest, options, accepted),
            Warnings = warnings,
            Images = decoded.Select(item => item.Metadata).ToArray(),
            Cells = cells,
            Candidates = candidates
        };
        report.Validate();
        return report;
    }

    private static IReadOnlyList<RegionDiscoveryCell> BuildCells(
        IReadOnlyList<WorkingImage> images,
        RegionDiscoveryOptions options,
        CancellationToken cancellationToken)
    {
        var cells = new List<RegionDiscoveryCell>(
            checked(options.GridColumns * options.GridRows));

        for (var row = 0; row < options.GridRows; row++)
        {
            for (var column = 0; column < options.GridColumns; column++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var region = new NormalizedRegion
                {
                    X = (double)column / options.GridColumns,
                    Y = (double)row / options.GridRows,
                    Width = 1.0 / options.GridColumns,
                    Height = 1.0 / options.GridRows
                };

                var samples = images.Select(image => new CellSample(
                    image.Metadata,
                    FingerprintExtractor.Extract(
                        image.Image,
                        region,
                        FingerprintMode.Grayscale,
                        options.CellFingerprintWidth,
                        options.CellFingerprintHeight),
                    FingerprintExtractor.Extract(
                        image.Image,
                        region,
                        FingerprintMode.Edge,
                        options.CellFingerprintWidth,
                        options.CellFingerprintHeight)))
                    .ToArray();

                var globalVariation = PairwiseVariation(samples.Select(sample => sample.Grayscale));
                var consecutiveVariation = ConsecutiveVariation(samples);
                var withinClusterVariation = WithinClusterVariation(samples);
                var betweenClusterSeparation = BetweenClusterSeparation(samples);
                var meanLuminance = MeanByte(samples.Select(sample => sample.Grayscale));
                var edgeDensity = MeanByte(samples.Select(sample => sample.Edge));

                var edgeWeight = 0.25 + 0.75 * edgeDensity;
                var stableScore = Clamp01(
                    (1 - globalVariation) * edgeWeight);
                var screenStateScore = Clamp01(
                    betweenClusterSeparation *
                    (1 - withinClusterVariation) *
                    (0.35 + 0.65 * edgeDensity));
                var dynamicScore = Clamp01(
                    Math.Max(globalVariation, consecutiveVariation) * edgeWeight);
                var textScore = Clamp01(
                    edgeDensity * (0.35 + 0.65 * Math.Max(
                        globalVariation,
                        betweenClusterSeparation)));

                cells.Add(new RegionDiscoveryCell
                {
                    Row = row,
                    Column = column,
                    Region = region,
                    MeanLuminance = meanLuminance,
                    MeanEdgeDensity = edgeDensity,
                    GlobalVariation = globalVariation,
                    ConsecutiveVariation = consecutiveVariation,
                    WithinClusterVariation = withinClusterVariation,
                    BetweenClusterSeparation = betweenClusterSeparation,
                    StableChromeScore = stableScore,
                    ScreenStateScore = screenStateScore,
                    DynamicContentScore = dynamicScore,
                    TextDensityScore = textScore
                });
            }
        }

        return cells;
    }

    private static double PairwiseVariation(IEnumerable<byte[]> fingerprints)
    {
        var values = fingerprints.ToArray();
        if (values.Length < 2)
        {
            return 0;
        }

        var sum = 0.0;
        var count = 0;
        for (var first = 0; first < values.Length; first++)
        {
            for (var second = first + 1; second < values.Length; second++)
            {
                sum += Distance(values[first], values[second]);
                count++;
            }
        }

        return count == 0 ? 0 : sum / count;
    }

    private static double ConsecutiveVariation(IReadOnlyList<CellSample> samples)
    {
        if (samples.Count < 2)
        {
            return 0;
        }

        var sum = 0.0;
        var count = 0;
        for (var index = 1; index < samples.Count; index++)
        {
            sum += Distance(
                samples[index - 1].Grayscale,
                samples[index].Grayscale);
            count++;
        }

        return sum / count;
    }

    private static double WithinClusterVariation(IReadOnlyList<CellSample> samples)
    {
        var values = new List<double>();
        foreach (var group in samples.GroupBy(sample => sample.Metadata.ClusterId))
        {
            var members = group.Select(sample => sample.Grayscale).ToArray();
            for (var first = 0; first < members.Length; first++)
            {
                for (var second = first + 1; second < members.Length; second++)
                {
                    values.Add(Distance(members[first], members[second]));
                }
            }
        }

        return values.Count == 0 ? 0 : values.Average();
    }

    private static double BetweenClusterSeparation(IReadOnlyList<CellSample> samples)
    {
        var centroids = samples
            .GroupBy(sample => sample.Metadata.ClusterId)
            .Select(group => Centroid(group.Select(sample => sample.Grayscale)))
            .ToArray();
        return PairwiseVariation(centroids);
    }

    private static byte[] Centroid(IEnumerable<byte[]> fingerprints)
    {
        var values = fingerprints.ToArray();
        if (values.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var length = values[0].Length;
        var result = new byte[length];
        for (var index = 0; index < length; index++)
        {
            result[index] = (byte)Math.Round(
                values.Average(value => value[index]),
                MidpointRounding.AwayFromZero);
        }
        return result;
    }

    private static double MeanByte(IEnumerable<byte[]> values)
    {
        var sum = 0L;
        var count = 0L;
        foreach (var value in values)
        {
            foreach (var item in value)
            {
                sum += item;
                count++;
            }
        }

        return count == 0 ? 0 : (double)sum / count / byte.MaxValue;
    }

    private static double Distance(byte[] first, byte[] second)
    {
        if (first.Length != second.Length)
        {
            throw new InvalidOperationException(
                "Region discovery fingerprint lengths do not match.");
        }

        if (first.Length == 0)
        {
            return 0;
        }

        var difference = 0L;
        for (var index = 0; index < first.Length; index++)
        {
            difference += Math.Abs(first[index] - second[index]);
        }

        return (double)difference / first.Length / byte.MaxValue;
    }

    private static IReadOnlyList<string> BuildWarnings(
        ImagePretestReport pretest,
        IReadOnlyList<RegionDiscoveryCandidate> candidates)
    {
        var warnings = new List<string>
        {
            "All candidate meanings are provisional. The report measures visual behaviour and does not perform OCR or identify Pokémon data fields.",
            "iPhone regions are normalised cross-platform evidence only and do not validate Android tap coordinates, Android timing or Calcy overlay geometry."
        };

        if (pretest.ClusterCount < 3)
        {
            warnings.Add(
                "Fewer than three visual clusters were found; additional screen-state variation may improve screen-state region discovery.");
        }

        foreach (var kind in Enum.GetValues<RegionCandidateKind>())
        {
            if (!candidates.Any(candidate => candidate.Kind == kind))
            {
                warnings.Add($"No provisional {kind} candidate was produced.");
            }
        }

        return warnings;
    }

    private static string GateDetail(
        ImagePretestReport pretest,
        RegionDiscoveryOptions options,
        bool accepted)
    {
        if (accepted)
        {
            return $"Accepted: {pretest.DecodedCount} decoded images, " +
                $"{pretest.ClusterCount} visual clusters and a " +
                $"{options.GridColumns}x{options.GridRows} normalised grid.";
        }

        var reasons = new List<string>();
        if (!pretest.Accepted)
        {
            reasons.Add("image pretest was not accepted");
        }
        if (pretest.DecodedCount < options.MinimumDecodedImages)
        {
            reasons.Add(
                $"requires at least {options.MinimumDecodedImages} decoded images");
        }
        if (pretest.GeometryGroupCount != 1)
        {
            reasons.Add("requires exactly one screenshot geometry group");
        }
        if (pretest.ClusterCount < 2)
        {
            reasons.Add("requires at least two visual clusters");
        }
        return "Rejected: " + string.Join("; ", reasons.Distinct(StringComparer.Ordinal)) + ".";
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

    private sealed record WorkingImage(
        RegionDiscoveryImage Metadata,
        PixelImage Image);

    private sealed record CellSample(
        RegionDiscoveryImage Metadata,
        byte[] Grayscale,
        byte[] Edge);
}
