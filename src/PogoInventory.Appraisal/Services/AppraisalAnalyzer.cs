using PogoInventory.Appraisal.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Appraisal.Services;

public sealed class AppraisalAnalyzer
{
    public AppraisalAnalysisResult Analyze(
        PixelImage image,
        AppraisalVisualProfile profile,
        bool allowComplete = false)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();

        Candidate? best = null;
        foreach (var scale in profile.SearchScales.OrderBy(value => value))
        {
            foreach (var yOffset in profile.SearchYOffsets.OrderBy(value => value))
            {
                foreach (var xOffset in profile.SearchXOffsets.OrderBy(value => value))
                {
                    var transform = new AppraisalLayoutTransform
                    {
                        XOffset = xOffset,
                        YOffset = yOffset,
                        Scale = scale
                    };

                    var measurements = profile.Bars
                        .OrderBy(item => item.Kind)
                        .Select(item => Measure(
                            image,
                            item,
                            transform,
                            profile.Colors))
                        .ToArray();

                    var orangeBars = measurements.Count(item =>
                        item.OrangeDetected);
                    var trackBars = measurements.Count(item =>
                        item.TrackDetected);
                    var averageConfidence = measurements.Average(item =>
                        item.Confidence);
                    var score =
                        averageConfidence +
                        orangeBars / 3d * 0.30 +
                        trackBars / 3d * 0.10;

                    var candidate = new Candidate(
                        transform,
                        measurements,
                        score,
                        orangeBars,
                        trackBars);

                    if (best is null ||
                        candidate.Score > best.Score + 0.0000001 ||
                        (Math.Abs(candidate.Score - best.Score) <= 0.0000001 &&
                         Compare(candidate.Transform, best.Transform) < 0))
                    {
                        best = candidate;
                    }
                }
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException(
                "Appraisal profile did not contain any usable transforms.");
        }

        var isCandidate =
            best.Score >= profile.CandidateScoreMinimum &&
            best.OrangeBars >= profile.MinimumOrangeBars &&
            best.TrackBars >= profile.MinimumTrackBars;

        var allBarsConfident = best.Measurements.All(item =>
            item.TrackDetected &&
            item.EstimatedIv is not null &&
            item.Confidence >= profile.CompleteBarConfidenceMinimum);

        var status = isCandidate
            ? allowComplete && profile.Verified && allBarsConfident
                ? AppraisalAnalysisStatus.Complete
                : AppraisalAnalysisStatus.Candidate
            : AppraisalAnalysisStatus.NotAppraisal;

        var confidence = Math.Clamp(
            best.Score / Math.Max(profile.CandidateScoreMinimum, 1),
            0,
            1);

        var result = new AppraisalAnalysisResult
        {
            Status = status,
            Confidence = confidence,
            CandidateScore = Math.Clamp(best.Score, 0, 2),
            Transform = best.Transform,
            Bars = best.Measurements,
            Detail = status switch
            {
                AppraisalAnalysisStatus.Complete =>
                    "Verified appraisal profile produced three confident IV values.",
                AppraisalAnalysisStatus.Candidate =>
                    "Three appraisal bar regions were measured, but Complete output is not enabled by a verified provider selection.",
                _ =>
                    "The screenshot did not meet the appraisal candidate thresholds."
            }
        };
        result.Validate();
        return result;
    }

    public static NormalizedRegion TransformRegion(
        NormalizedRegion source,
        AppraisalLayoutTransform transform)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(transform);
        source.Validate();
        transform.Validate();

        var region = new NormalizedRegion
        {
            X = source.X * transform.Scale + transform.XOffset,
            Y = source.Y * transform.Scale + transform.YOffset,
            Width = source.Width * transform.Scale,
            Height = source.Height * transform.Scale
        };
        region.Validate();
        return region;
    }

    private static AppraisalBarMeasurement Measure(
        PixelImage image,
        AppraisalBarDefinition definition,
        AppraisalLayoutTransform transform,
        AppraisalColorProfile colors)
    {
        NormalizedRegion region;
        try
        {
            region = TransformRegion(definition.Region, transform);
        }
        catch
        {
            return EmptyMeasurement(definition.Kind, definition.Region);
        }

        var rectangle = region.ToPixels(image.Width, image.Height);
        var columnStep = Math.Max(
            1,
            (int)Math.Ceiling(rectangle.Width / 160d));
        var rowStep = Math.Max(
            1,
            (int)Math.Ceiling(rectangle.Height / 12d));
        var sampledWidth = (int)Math.Ceiling(
            rectangle.Width / (double)columnStep);
        var orangeColumns = new bool[sampledWidth];
        var trackColumns = new bool[sampledWidth];

        for (var sampledColumn = 0;
             sampledColumn < sampledWidth;
             sampledColumn++)
        {
            var sourceColumn = Math.Min(
                rectangle.Width - 1,
                sampledColumn * columnStep);
            var orangeCount = 0;
            var trackCount = 0;
            var sampleCount = 0;

            for (var row = 0;
                 row < rectangle.Height;
                 row += rowStep)
            {
                var pixel = image.GetPixel(
                    rectangle.X + sourceColumn,
                    rectangle.Y + row);
                var orange = IsOrange(pixel, colors);
                if (orange)
                {
                    orangeCount++;
                }

                if (orange || IsTrack(pixel, colors))
                {
                    trackCount++;
                }

                sampleCount++;
            }

            var denominator = Math.Max(1, sampleCount);
            orangeColumns[sampledColumn] =
                orangeCount / (double)denominator >=
                colors.MinimumColumnCoverage;
            trackColumns[sampledColumn] =
                trackCount / (double)denominator >=
                colors.MinimumColumnCoverage;
        }

        FillInteriorGaps(
            trackColumns,
            Math.Max(
                1,
                (int)Math.Round(
                    sampledWidth *
                    colors.MaximumTrackGapFraction)));

        var (trackStart, trackEnd) = LongestRun(trackColumns);
        var trackLength = trackEnd >= trackStart
            ? trackEnd - trackStart + 1
            : 0;
        var trackWidthFraction = trackLength /
            (double)Math.Max(1, sampledWidth);
        var trackDetected =
            trackLength > 0 &&
            trackWidthFraction >= colors.MinimumTrackWidthFraction;

        var fillEnd = -1;
        if (trackDetected)
        {
            for (var column = trackEnd; column >= trackStart; column--)
            {
                if (orangeColumns[column])
                {
                    fillEnd = column;
                    break;
                }
            }
        }

        var orangeDetected = fillEnd >= trackStart;
        var fillFraction = orangeDetected
            ? Math.Clamp(
                (fillEnd - trackStart + 1) /
                (double)Math.Max(1, trackLength),
                0,
                1)
            : 0;
        var estimatedIv = trackDetected
            ? Math.Clamp(
                (int)Math.Round(
                    fillFraction * 15,
                    MidpointRounding.AwayFromZero),
                0,
                15)
            : null;

        var activeOrangeColumns = trackDetected
            ? orangeColumns
                .Skip(trackStart)
                .Take(trackLength)
                .Count(value => value)
            : 0;
        var orangeColumnShare = activeOrangeColumns /
            (double)Math.Max(1, trackLength);
        var leftMarginFraction = trackDetected
            ? trackStart / (double)Math.Max(1, sampledWidth)
            : 0;
        var rightMarginFraction = trackDetected
            ? (sampledWidth - 1 - trackEnd) /
              (double)Math.Max(1, sampledWidth)
            : 0;
        var widthFit = trackDetected
            ? Math.Clamp(
                1 - Math.Abs(trackWidthFraction - 0.84) / 0.25,
                0,
                1)
            : 0;
        var marginBalance = trackDetected
            ? Math.Clamp(
                1 - Math.Abs(
                    leftMarginFraction -
                    rightMarginFraction) / 0.15,
                0,
                1)
            : 0;
        var trackConfidence = trackDetected
            ? (widthFit + marginBalance) / 2
            : Math.Clamp(
                trackWidthFraction /
                Math.Max(colors.MinimumTrackWidthFraction, 0.01),
                0,
                0.25);
        var orangeConfidence = orangeDetected
            ? Math.Clamp(orangeColumnShare / 0.35, 0, 1)
            : 0;
        var confidence = Math.Clamp(
            trackConfidence * 0.55 +
            orangeConfidence * 0.45,
            0,
            1);

        return new AppraisalBarMeasurement
        {
            Kind = definition.Kind,
            Region = region,
            TrackDetected = trackDetected,
            OrangeDetected = orangeDetected,
            FillFraction = fillFraction,
            EstimatedIv = estimatedIv,
            Confidence = confidence,
            TrackWidthFraction = trackWidthFraction,
            TrackStartColumn = trackStart < 0
                ? -1
                : Math.Min(
                    rectangle.Width - 1,
                    trackStart * columnStep),
            TrackEndColumn = trackEnd < 0
                ? -1
                : Math.Min(
                    rectangle.Width - 1,
                    trackEnd * columnStep),
            FillEndColumn = fillEnd < 0
                ? -1
                : Math.Min(
                    rectangle.Width - 1,
                    fillEnd * columnStep),
            RegionPixelWidth = rectangle.Width,
            RegionPixelHeight = rectangle.Height
        };
    }

    private static AppraisalBarMeasurement EmptyMeasurement(
        AppraisalBarKind kind,
        NormalizedRegion region) =>
        new()
        {
            Kind = kind,
            Region = region,
            TrackDetected = false,
            OrangeDetected = false,
            FillFraction = 0,
            EstimatedIv = null,
            Confidence = 0,
            TrackWidthFraction = 0,
            TrackStartColumn = -1,
            TrackEndColumn = -1,
            FillEndColumn = -1,
            RegionPixelWidth = 1,
            RegionPixelHeight = 1
        };

    private static bool IsOrange(
        Rgba32 pixel,
        AppraisalColorProfile colors) =>
        pixel.R >= colors.OrangeRedMinimum &&
        pixel.G >= colors.OrangeGreenMinimum &&
        pixel.G <= colors.OrangeGreenMaximum &&
        pixel.B <= colors.OrangeBlueMaximum &&
        pixel.R - pixel.G >= colors.OrangeRedGreenDeltaMinimum &&
        pixel.G - pixel.B >= colors.OrangeGreenBlueDeltaMinimum;

    private static bool IsTrack(
        Rgba32 pixel,
        AppraisalColorProfile colors)
    {
        var minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        var maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        return minimum >= colors.TrackChannelMinimum &&
            maximum <= colors.TrackChannelMaximum &&
            maximum - minimum <= colors.TrackMaximumChannelSpread;
    }

    private static void FillInteriorGaps(
        bool[] values,
        int maximumGap)
    {
        var index = 0;
        while (index < values.Length)
        {
            if (values[index])
            {
                index++;
                continue;
            }

            var start = index;
            while (index < values.Length && !values[index])
            {
                index++;
            }

            var length = index - start;
            if (start > 0 &&
                index < values.Length &&
                length <= maximumGap)
            {
                Array.Fill(values, true, start, length);
            }
        }
    }

    private static (int Start, int End) LongestRun(bool[] values)
    {
        var bestStart = -1;
        var bestEnd = -1;
        var index = 0;

        while (index < values.Length)
        {
            if (!values[index])
            {
                index++;
                continue;
            }

            var start = index;
            while (index < values.Length && values[index])
            {
                index++;
            }

            var end = index - 1;
            if (bestStart < 0 ||
                end - start > bestEnd - bestStart)
            {
                bestStart = start;
                bestEnd = end;
            }
        }

        return (bestStart, bestEnd);
    }

    private static int Compare(
        AppraisalLayoutTransform first,
        AppraisalLayoutTransform second)
    {
        var firstDistance =
            Math.Abs(first.XOffset) +
            Math.Abs(first.YOffset) +
            Math.Abs(first.Scale - 1);
        var secondDistance =
            Math.Abs(second.XOffset) +
            Math.Abs(second.YOffset) +
            Math.Abs(second.Scale - 1);
        var distance = firstDistance.CompareTo(secondDistance);
        if (distance != 0)
        {
            return distance;
        }

        var scale = first.Scale.CompareTo(second.Scale);
        if (scale != 0)
        {
            return scale;
        }

        var y = first.YOffset.CompareTo(second.YOffset);
        return y != 0
            ? y
            : first.XOffset.CompareTo(second.XOffset);
    }

    private sealed record Candidate(
        AppraisalLayoutTransform Transform,
        IReadOnlyList<AppraisalBarMeasurement> Measurements,
        double Score,
        int OrangeBars,
        int TrackBars);
}
