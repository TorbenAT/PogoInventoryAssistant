using System.Security.Cryptography;
using PogoInventory.Vision.Errors;
using PogoInventory.Vision.Models;

namespace PogoInventory.Vision.Imaging;

/// <summary>
/// Separates screenshot integrity from a stable Details fingerprint. The model,
/// status bar, overlays and the dynamically located tag section are excluded;
/// lower content is sampled relative to a detected visual anchor.
/// </summary>
public sealed class PokemonDetailsIdentityAnalyzer
{
    private readonly PokemonIdentityFingerprintProfile _profile;

    public PokemonDetailsIdentityAnalyzer(PokemonIdentityFingerprintProfile? profile = null)
    {
        _profile = profile ?? new PokemonIdentityFingerprintProfile();
        _profile.Validate();
    }

    public PokemonIdentityFingerprintObservation Analyze(byte[] screenshotPng)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var evidenceHash = Hex(SHA256.HashData(screenshotPng));
        try
        {
            return Analyze(PngDecoder.Decode(screenshotPng), evidenceHash);
        }
        catch (Exception exception) when (exception is ScreenVisionException or ArgumentException)
        {
            return Unavailable(evidenceHash, exception.Message);
        }
    }

    public PokemonIdentityConsensus Consensus(IReadOnlyList<PokemonIdentityFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("At least one identity frame is required.", nameof(frames));

        var observations = frames.Select(frame => Analyze(frame.ScreenshotPng)).ToArray();
        var usable = observations.Where(item =>
            item.Status != PokemonIdentityObservationStatus.Unavailable &&
            !string.IsNullOrEmpty(item.StableFingerprintBase64)).ToArray();
        if (usable.Length == 0)
        {
            return new PokemonIdentityConsensus
            {
                Status = PokemonIdentityObservationStatus.Unavailable,
                StableFingerprintSha256 = string.Empty,
                StableFingerprintBase64 = string.Empty,
                Confidence = 0,
                Frames = observations,
                EvidenceHashes = observations.Select(item => item.EvidenceSha256).ToArray(),
                Tags = EmptyTags(),
                IgnoredFrameCount = observations.Length
            };
        }

        var best = usable.Select(candidate => new
            {
                Candidate = candidate,
                Matches = usable.Count(other => Similarity(candidate, other) >=
                    _profile.SameIdentitySimilarityThreshold)
            })
            .OrderByDescending(value => value.Matches)
            .ThenByDescending(value => value.Candidate.Confidence)
            .First();
        var consensusFrames = usable.Where(item => Similarity(best.Candidate, item) >=
            _profile.SameIdentitySimilarityThreshold).ToArray();
        var confidence = consensusFrames.Length == 0 ? 0 :
            consensusFrames.Average(item => Similarity(best.Candidate, item)) *
            consensusFrames.Length / (double)Math.Max(3, frames.Count);
        return new PokemonIdentityConsensus
        {
            Status = consensusFrames.Length >= Math.Min(3, frames.Count)
                ? PokemonIdentityObservationStatus.Complete
                : PokemonIdentityObservationStatus.Partial,
            StableFingerprintSha256 = best.Candidate.StableFingerprintSha256,
            StableFingerprintBase64 = best.Candidate.StableFingerprintBase64,
            Confidence = Math.Clamp(confidence, 0, 1),
            Frames = observations,
            EvidenceHashes = observations.Select(item => item.EvidenceSha256).ToArray(),
            Tags = consensusFrames.OrderByDescending(item => item.Confidence).First().Tags,
            IgnoredFrameCount = observations.Length - consensusFrames.Length
        };
    }

    private PokemonIdentityFingerprintObservation Analyze(PixelImage image, string evidenceHash)
    {
        var tags = LocateTags(image);
        var lowerAnchor = LocateLowerAnchor(image, tags.Section);
        if (lowerAnchor is null)
        {
            return new PokemonIdentityFingerprintObservation
            {
                Status = PokemonIdentityObservationStatus.Partial,
                EvidenceSha256 = evidenceHash,
                StableFingerprintSha256 = string.Empty,
                StableFingerprintBase64 = string.Empty,
                Confidence = 0.25,
                Tags = tags,
                IgnoredDynamicRegions = IgnoredRegions(tags.Section),
                AnchorEvidence = new[] { "LowerContentAnchorMissing" }
            };
        }

        var top = FingerprintExtractor.Extract(
            image, _profile.HeaderRegion, FingerprintMode.Color,
            _profile.FingerprintWidth, _profile.FingerprintHeight);
        var anchor = FingerprintExtractor.Extract(
            image, RegionAroundY(0.10, lowerAnchor.Value - 0.055, 0.11), FingerprintMode.Edge,
            _profile.FingerprintWidth, _profile.FingerprintHeight);
        var lowerProfile = _profile.LowerContentRegion;
        var lower = FingerprintExtractor.Extract(
            image,
            RegionAroundY(lowerProfile.X, lowerAnchor.Value + lowerProfile.Y,
                lowerProfile.Height, lowerProfile.Width),
            FingerprintMode.Color,
            _profile.FingerprintWidth, _profile.FingerprintHeight);
        var combined = top.Concat(anchor).Concat(lower).ToArray();
        return new PokemonIdentityFingerprintObservation
        {
            Status = PokemonIdentityObservationStatus.Complete,
            EvidenceSha256 = evidenceHash,
            StableFingerprintSha256 = Hex(SHA256.HashData(combined)),
            StableFingerprintBase64 = Convert.ToBase64String(combined),
            Confidence = 0.85,
            Tags = tags,
            IgnoredDynamicRegions = IgnoredRegions(tags.Section),
            AnchorEvidence = new[] { "HeaderAnchor", "LowerContentAnchor", "TagSectionDynamicallyLocated" },
            LowerAnchorY = lowerAnchor
        };
    }

    private PokemonIdentityTagObservation LocateTags(PixelImage image)
    {
        var region = _profile.TagSearchRegion.ToPixels(image.Width, image.Height);
        var visited = new bool[image.Width * image.Height];
        var components = new List<(int Left, int Top, int Right, int Bottom)>();
        for (var y = region.Y; y < region.Y + region.Height; y++)
        for (var x = region.X; x < region.X + region.Width; x++)
        {
            var index = y * image.Width + x;
            if (visited[index] || !IsPillPixel(image.GetPixel(x, y))) continue;
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((x, y)); visited[index] = true;
            var left = x; var right = x; var top = y; var bottom = y; var count = 0;
            while (queue.Count > 0)
            {
                var point = queue.Dequeue(); count++;
                left = Math.Min(left, point.X); right = Math.Max(right, point.X);
                top = Math.Min(top, point.Y); bottom = Math.Max(bottom, point.Y);
                foreach (var next in new[]
                {
                    (point.X - 1, point.Y), (point.X + 1, point.Y),
                    (point.X, point.Y - 1), (point.X, point.Y + 1)
                })
                {
                    if (next.Item1 < region.X || next.Item1 >= region.X + region.Width ||
                        next.Item2 < region.Y || next.Item2 >= region.Y + region.Height) continue;
                    var nextIndex = next.Item2 * image.Width + next.Item1;
                    if (!visited[nextIndex] && IsPillPixel(image.GetPixel(next.Item1, next.Item2)))
                    {
                        visited[nextIndex] = true; queue.Enqueue(next);
                    }
                }
            }
            var componentWidth = (right - left) / (double)image.Width;
            var componentHeight = (bottom - top) / (double)image.Height;
            var aspect = (right - left) / (double)Math.Max(1, bottom - top);
            if (count >= Math.Max(20, image.Width * image.Height / 3000) &&
                componentWidth is >= 0.12 and <= 0.50 &&
                componentHeight is >= 0.012 and <= 0.08 &&
                aspect is >= 2.0 and <= 9.0)
                components.Add((left, top, right, bottom));
        }
        var accepted = components.OrderBy(item => item.Top).Take(8).ToArray();
        var sectionBottom = accepted.Length == 0 ? 0.35 :
            accepted.Max(item => item.Bottom) / (double)image.Height;
        var sectionTop = accepted.Length == 0 ? 0.25 :
            accepted.Min(item => item.Top) / (double)image.Height - 0.025;
        return new PokemonIdentityTagObservation
        {
            TagCount = accepted.Length,
            Section = new NormalizedRegion
            {
                X = _profile.TagSearchRegion.X,
                Y = Math.Clamp(sectionTop, 0, 1),
                Width = _profile.TagSearchRegion.Width,
                Height = Math.Clamp(sectionBottom - sectionTop + 0.02, 0.01, 1)
            },
            IsSeparateFromIdentity = true
        };
    }

    private double? LocateLowerAnchor(PixelImage image, NormalizedRegion? tagSection)
    {
        var search = _profile.LowerAnchorSearchRegion.ToPixels(image.Width, image.Height);
        var start = search.Y;
        if (tagSection is not null)
        {
            var tagPixels = tagSection.ToPixels(image.Width, image.Height);
            start = Math.Max(start, tagPixels.Y + tagPixels.Height + Math.Max(2, image.Height / 100));
        }
        var bestScore = 0d;
        var bestDividerScore = 0d;
        var bestY = -1;
        for (var y = start + 2; y < search.Y + search.Height - 2; y++)
        {
            var score = 0d;
            var dividerScore = 0d;
            for (var x = search.X + 2; x < search.X + search.Width - 2; x += 2)
            {
                var above = Luma(image.GetPixel(x, y - 1));
                var current = Luma(image.GetPixel(x, y));
                var below = Luma(image.GetPixel(x, y + 1));
                var pixel = image.GetPixel(x, y);
                var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                var horizontalDividerPixel = maximum - minimum <= 14 &&
                    current is >= 165 and <= 235 &&
                    (above - current >= 6 || below - current >= 6);
                if (horizontalDividerPixel) dividerScore++;
                score += Math.Abs(current - above) + Math.Abs(below - current) * 0.05;
            }
            if (dividerScore > bestDividerScore ||
                dividerScore == bestDividerScore && score > bestScore)
            {
                bestDividerScore = dividerScore;
                bestScore = score;
                bestY = y;
            }
        }
        return bestY < 0 || bestDividerScore < image.Width * 0.10
            ? null
            : bestY / (double)image.Height;
    }

    private IReadOnlyList<NormalizedRegion> IgnoredRegions(NormalizedRegion? tag) => new[]
    {
        new NormalizedRegion { X = 0, Y = 0, Width = 1, Height = 0.055 },
        new NormalizedRegion { X = 0.08, Y = 0.24, Width = 0.84, Height = 0.42 },
        tag ?? new NormalizedRegion { X = 0.08, Y = 0.24, Width = 0.84, Height = 0.12 }
    };

    private static NormalizedRegion RegionAroundY(double x, double y, double height, double width = 0.80) => new()
    {
        X = Math.Clamp(x, 0, 1 - width), Y = Math.Clamp(y, 0, 1 - height),
        Width = width, Height = height
    };

    private static bool IsPillPixel(Rgba32 pixel)
    {
        var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        var gray = maximum - minimum <= 24 && maximum is >= 145 and <= 225;
        // Current accepted Android tags are gray pills. Keep colored support
        // only for sufficiently bright, pill-shaped profiles; large colored
        // action buttons are rejected by the component geometry gate above.
        var colored = maximum is >= 150 and <= 225 && maximum - minimum >= 18;
        return pixel.A > 0 && (gray || colored);
    }

    private static double Similarity(PokemonIdentityFingerprintObservation first,
        PokemonIdentityFingerprintObservation second) =>
        string.IsNullOrEmpty(first.StableFingerprintBase64) ||
        string.IsNullOrEmpty(second.StableFingerprintBase64)
            ? 0
            : FingerprintComparer.Similarity(
                Convert.FromBase64String(first.StableFingerprintBase64),
                Convert.FromBase64String(second.StableFingerprintBase64));

    private static double Luma(Rgba32 pixel) => (77 * pixel.R + 150 * pixel.G + 29 * pixel.B) / 256d;
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
    private static PokemonIdentityTagObservation EmptyTags() => new()
    {
        TagCount = 0, Section = null, IsSeparateFromIdentity = true
    };

    private static PokemonIdentityFingerprintObservation Unavailable(string hash, string reason) => new()
    {
        Status = PokemonIdentityObservationStatus.Unavailable,
        EvidenceSha256 = hash,
        StableFingerprintSha256 = string.Empty,
        StableFingerprintBase64 = string.Empty,
        Confidence = 0,
        Tags = EmptyTags(),
        AnchorEvidence = new[] { reason }
    };
}
