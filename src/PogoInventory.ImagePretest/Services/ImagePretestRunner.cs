using PogoInventory.ImagePretest.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.ImagePretest.Services;

public static class ImagePretestRunner
{
    private static readonly NormalizedRegion WholeScreen = new()
    {
        X = 0,
        Y = 0,
        Width = 1,
        Height = 1
    };

    public static async Task<ImagePretestReport> RunAsync(
        string inputDirectory,
        ImagePretestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ImagePretestOptions();
        options.Validate();

        var root = Path.GetFullPath(inputDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Image pretest input directory does not exist: {root}");
        }

        var files = Directory
            .EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .Where(IsPng)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var working = new List<WorkingImage>(files.Length);
        for (var index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            working.Add(await LoadAsync(
                root,
                files[index],
                index + 1,
                options,
                cancellationToken));
        }

        var decoded = working.Where(item => item.Item.Decoded).ToArray();
        var pairs = BuildPairs(decoded, options);
        var clusters = BuildClusters(decoded, pairs, options.ClusterThreshold);
        var images = working.Select(item => item.Item).ToArray();
        var warnings = BuildWarnings(images, pairs, clusters, options);

        var decodedCount = images.Count(item => item.Decoded);
        var failedCount = images.Length - decodedCount;
        var portraitCount = images.Count(item =>
            item.Decoded && item.Orientation == ScreenOrientation.Portrait);
        var landscapeCount = images.Count(item =>
            item.Decoded && item.Orientation == ScreenOrientation.Landscape);
        var distinctHashCount = images
            .Where(item => item.Decoded)
            .Select(item => item.Sha256)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var decodeRate = images.Length == 0
            ? 0
            : (double)decodedCount / images.Length;
        var accepted = decodedCount >= options.MinimumImageCount &&
            decodeRate >= options.MinimumDecodeRate &&
            portraitCount == decodedCount &&
            distinctHashCount >= 2;

        var gate = accepted
            ? AcceptedGate(
                images.Length,
                decodedCount,
                failedCount,
                decodeRate)
            : GateFailure(
                images.Length,
                decodedCount,
                portraitCount,
                distinctHashCount,
                options.MinimumImageCount,
                decodeRate,
                options.MinimumDecodeRate);

        var report = new ImagePretestReport
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            InputDirectory = root,
            MinimumImageCount = options.MinimumImageCount,
            MinimumDecodeRate = options.MinimumDecodeRate,
            DecodeRate = decodeRate,
            ImageCount = images.Length,
            DecodedCount = decodedCount,
            FailedCount = failedCount,
            PortraitCount = portraitCount,
            LandscapeCount = landscapeCount,
            GeometryGroupCount = images
                .Where(item => item.Decoded)
                .Select(item => item.GeometryKey)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            DistinctFileHashCount = distinctHashCount,
            ExactDuplicatePairCount = pairs.Count(pair => pair.ExactDuplicate),
            NearDuplicatePairCount = pairs.Count(pair => pair.NearDuplicate),
            ClusterCount = clusters.Count,
            Accepted = accepted,
            GateDetail = gate,
            Warnings = warnings,
            Images = images,
            SimilarityPairs = pairs,
            Clusters = clusters
        };
        report.Validate();
        return report;
    }

    private static async Task<WorkingImage> LoadAsync(
        string root,
        string path,
        int sequenceNumber,
        ImagePretestOptions options,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(path);
        var relative = Path.GetRelativePath(root, path)
            .Replace('\\', '/');
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return WorkingImage.Failed(
                sequenceNumber,
                fileName,
                relative,
                "ReadFailed",
                exception.Message);
        }

        var fileHash = ImagePretestHash.Sha256(bytes);
        try
        {
            var image = PngDecoder.Decode(bytes);
            var grayscale = FingerprintExtractor.Extract(
                image,
                WholeScreen,
                FingerprintMode.Grayscale,
                options.FingerprintWidth,
                options.FingerprintHeight);
            var edge = FingerprintExtractor.Extract(
                image,
                WholeScreen,
                FingerprintMode.Edge,
                options.FingerprintWidth,
                options.FingerprintHeight);
            var combined = new byte[grayscale.Length + edge.Length];
            Buffer.BlockCopy(grayscale, 0, combined, 0, grayscale.Length);
            Buffer.BlockCopy(edge, 0, combined, grayscale.Length, edge.Length);

            var orientation = image.Width <= image.Height
                ? ScreenOrientation.Portrait
                : ScreenOrientation.Landscape;
            var item = new ImagePretestItem
            {
                SequenceNumber = sequenceNumber,
                FileName = fileName,
                RelativePath = relative,
                Sha256 = fileHash,
                LengthBytes = bytes.LongLength,
                Width = image.Width,
                Height = image.Height,
                AspectRatio = (double)image.Width / image.Height,
                Orientation = orientation,
                GeometryKey = $"{image.Width}x{image.Height}",
                VisualFingerprintSha256 = ImagePretestHash.Sha256(combined)
            };
            return new WorkingImage(item, combined);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new WorkingImage(
                new ImagePretestItem
                {
                    SequenceNumber = sequenceNumber,
                    FileName = fileName,
                    RelativePath = relative,
                    Sha256 = fileHash,
                    LengthBytes = bytes.LongLength,
                    Width = 0,
                    Height = 0,
                    AspectRatio = 0,
                    Orientation = ScreenOrientation.Portrait,
                    GeometryKey = "unknown",
                    VisualFingerprintSha256 = string.Empty,
                    ErrorCode = exception.GetType().Name,
                    ErrorDetail = exception.Message
                },
                null);
        }
    }

    private static IReadOnlyList<ImageSimilarityPair> BuildPairs(
        IReadOnlyList<WorkingImage> images,
        ImagePretestOptions options)
    {
        var result = new List<ImageSimilarityPair>();
        for (var firstIndex = 0; firstIndex < images.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1;
                 secondIndex < images.Count;
                 secondIndex++)
            {
                var first = images[firstIndex];
                var second = images[secondIndex];
                var similarity = FingerprintComparer.Similarity(
                    first.Fingerprint!,
                    second.Fingerprint!);
                var exact = string.Equals(
                    first.Item.Sha256,
                    second.Item.Sha256,
                    StringComparison.Ordinal);
                result.Add(new ImageSimilarityPair
                {
                    FirstFileName = first.Item.FileName,
                    SecondFileName = second.Item.FileName,
                    Similarity = similarity,
                    ExactDuplicate = exact,
                    NearDuplicate = !exact &&
                        similarity >= options.NearDuplicateThreshold,
                    Consecutive = Math.Abs(
                        first.Item.SequenceNumber - second.Item.SequenceNumber) == 1
                });
            }
        }

        return result
            .OrderByDescending(pair => pair.Similarity)
            .ThenBy(pair => pair.FirstFileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.SecondFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ImagePretestCluster> BuildClusters(
        IReadOnlyList<WorkingImage> images,
        IReadOnlyList<ImageSimilarityPair> pairs,
        double threshold)
    {
        if (images.Count == 0)
        {
            return Array.Empty<ImagePretestCluster>();
        }

        var parent = Enumerable.Range(0, images.Count).ToArray();
        var indexByName = images
            .Select((item, index) => (FileName: item.Item.FileName, Index: index))
            .ToDictionary(
                pair => pair.FileName,
                pair => pair.Index,
                StringComparer.Ordinal);

        foreach (var pair in pairs.Where(pair => pair.Similarity >= threshold))
        {
            Union(
                parent,
                indexByName[pair.FirstFileName],
                indexByName[pair.SecondFileName]);
        }

        return images
            .Select((item, index) => new
            {
                Root = Find(parent, index),
                Item = item.Item
            })
            .GroupBy(item => item.Root)
            .Select(group => group
                .Select(item => item.Item)
                .OrderBy(item => item.SequenceNumber)
                .ToArray())
            .OrderBy(group => group[0].SequenceNumber)
            .Select((members, index) => new ImagePretestCluster
            {
                Id = $"cluster-{index + 1:D2}",
                RepresentativeFileName = members[0].FileName,
                Members = members.Select(member => member.FileName).ToArray()
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildWarnings(
        IReadOnlyList<ImagePretestItem> images,
        IReadOnlyList<ImageSimilarityPair> pairs,
        IReadOnlyList<ImagePretestCluster> clusters,
        ImagePretestOptions options)
    {
        var warnings = new List<string>();
        if (images.Count < options.MinimumImageCount)
        {
            warnings.Add(
                $"Only {images.Count} PNG files were found; at least " +
                $"{options.MinimumImageCount} are required for the pretest gate.");
        }

        var failed = images.Where(item => !item.Decoded).ToArray();
        if (failed.Length > 0)
        {
            warnings.Add($"{failed.Length} image(s) could not be decoded.");
        }

        var geometryGroups = images
            .Where(item => item.Decoded)
            .GroupBy(item => item.GeometryKey)
            .OrderByDescending(group => group.Count())
            .ToArray();
        if (geometryGroups.Length > 1)
        {
            warnings.Add(
                "Multiple screenshot geometries were found: " +
                string.Join(", ", geometryGroups.Select(group =>
                    $"{group.Key} ({group.Count()})")) + ".");
        }

        var exactCount = pairs.Count(pair => pair.ExactDuplicate);
        if (exactCount > 0)
        {
            warnings.Add($"{exactCount} pixel-identical file pair(s) were found.");
        }

        var nearCount = pairs.Count(pair => pair.NearDuplicate);
        if (nearCount > 0)
        {
            warnings.Add(
                $"{nearCount} near-duplicate pair(s) reached similarity " +
                $"{options.NearDuplicateThreshold:F3} or higher.");
        }

        if (clusters.Count == 1 && images.Count >= 2)
        {
            warnings.Add(
                "All decoded images fell into one visual cluster. More screen-state variation may be useful.");
        }

        warnings.Add(
            "iPhone screenshots are cross-platform fixtures only; they do not validate Android ADB coordinates, Calcy output or Android timing.");
        return warnings;
    }

    private static string AcceptedGate(
        int imageCount,
        int decodedCount,
        int failedCount,
        double decodeRate)
    {
        var rejected = failedCount == 0
            ? string.Empty
            : $"; {failedCount} rejected file(s) retained in diagnostics";
        return $"Accepted: {decodedCount}/{imageCount} portrait PNG screenshots decoded " +
            $"({decodeRate:P1}){rejected}.";
    }

    private static string GateFailure(
        int imageCount,
        int decodedCount,
        int portraitCount,
        int distinctHashCount,
        int minimumImageCount,
        double decodeRate,
        double minimumDecodeRate)
    {
        var failures = new List<string>();
        if (decodedCount < minimumImageCount)
        {
            failures.Add($"requires at least {minimumImageCount} decoded images");
        }
        if (decodeRate < minimumDecodeRate)
        {
            failures.Add(
                $"decode rate {decodeRate:P1} is below required {minimumDecodeRate:P1}");
        }
        if (portraitCount != decodedCount)
        {
            failures.Add("all decoded screenshots must be portrait");
        }
        if (distinctHashCount < 2)
        {
            failures.Add("requires at least two distinct decoded screenshots");
        }
        if (imageCount == 0)
        {
            failures.Add("no PNG files were found");
        }
        return "Rejected: " + string.Join("; ", failures.Distinct(StringComparer.Ordinal)) + ".";
    }

    private static bool IsPng(string path) =>
        string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);

    private static int Find(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }
        return index;
    }

    private static void Union(int[] parent, int first, int second)
    {
        var firstRoot = Find(parent, first);
        var secondRoot = Find(parent, second);
        if (firstRoot != secondRoot)
        {
            parent[secondRoot] = firstRoot;
        }
    }

    private sealed record WorkingImage(
        ImagePretestItem Item,
        byte[]? Fingerprint)
    {
        public static WorkingImage Failed(
            int sequenceNumber,
            string fileName,
            string relativePath,
            string code,
            string detail) =>
            new(
                new ImagePretestItem
                {
                    SequenceNumber = sequenceNumber,
                    FileName = fileName,
                    RelativePath = relativePath,
                    Sha256 = string.Empty,
                    LengthBytes = 0,
                    Width = 0,
                    Height = 0,
                    AspectRatio = 0,
                    Orientation = ScreenOrientation.Portrait,
                    GeometryKey = "unknown",
                    VisualFingerprintSha256 = string.Empty,
                    ErrorCode = code,
                    ErrorDetail = detail
                },
                null);
    }
}
