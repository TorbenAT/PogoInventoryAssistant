using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Automation.Models;
using PogoInventory.Exploration.Models;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Services;

public enum RecoveryOutcome
{
    PROGRESSED,
    SUCCEEDED,
    ACTION_NOT_OBSERVED,
    UNKNOWN_STOP,
    UNEXPECTED_STOP,
    RETRY_EXHAUSTED,
    STABILITY_TIMEOUT
}

public enum RecoveryFrameKind
{
    Unknown,
    Other,
    AppraisalIntro,
    AppraisalBars,
    Conflicting
}

public enum AppraisalContinuationOutcome
{
    WAITING_FOR_STABILITY,
    SUCCESS_ALREADY_ADVANCED,
    TAP_INTRO_ONCE,
    SUCCESS_TAPPED,
    FAIL_CLOSED
}

public sealed record RecoveryRoiSignature(IReadOnlyList<double> Features);

public sealed record RecoveryBarSignature
{
    public required AppraisalBarKind Kind { get; init; }
    public required NormalizedRegion Region { get; init; }
    public required double TrackStartFraction { get; init; }
    public required double TrackEndFraction { get; init; }
    public required double FillFraction { get; init; }
    public required double TrackWidthFraction { get; init; }
    public required RecoveryRoiSignature ColorAndStructure { get; init; }
}

public sealed record RecoveryFrame
{
    public required PokemonGoGameStateDetection Detection { get; init; }
    public required byte[] Screenshot { get; init; }
    public required string EvidenceSignature { get; init; }
    public required RecoveryFrameKind Kind { get; init; }
    public required bool HasIntroAnchor { get; init; }
    public required bool HasBarsAnchor { get; init; }
    public required bool HasConflictingAnchor { get; init; }
    public double? LocatorConfidence { get; init; }
    public NormalizedPoint? LocatorTarget { get; init; }
    public IReadOnlyList<RecoveryRoiSignature> StableRegions { get; init; } =
        Array.Empty<RecoveryRoiSignature>();
    public IReadOnlyList<RecoveryBarSignature> Bars { get; init; } =
        Array.Empty<RecoveryBarSignature>();
}

public enum RecoveryInputAction
{
    ExitAppraisal,
    PressBack
}

public sealed record RecoveryActionAuthorization
{
    public required int Sequence { get; init; }
    public required RecoveryInputAction Action { get; init; }
    public required PokemonGoGameState StateBefore { get; init; }
    public required PokemonGoGameState ExpectedState { get; init; }
    public RecoveryFrameKind? ExpectedFrameKind { get; init; }
    public NormalizedPoint? Target { get; init; }
    public required string Detail { get; init; }
}

public sealed class GuardedInventoryRecovery
{
    public const int ConsensusMatches = 3;
    public const int ConsensusWindow = 5;
    public const int MaxAppraisalTotalActions = 3;
    // The real OnePlus AppraisalIntro anchor is consistently 0.611. Keep a
    // small evidence margin while rejecting weaker partial overlays.
    // The item-4 real-device intro is visually unambiguous but scored 0.594
    // because the avatar overlapped the lower dialog edge. Keep a bounded
    // margin below that observation while retaining ROI consensus.
    public const double MinimumIntroLocatorConfidence = 0.58;

    // Locked from the 1080x2340 OnePlus captures under local-data/validation:
    // animation changes the whole screen substantially, while these sampled
    // dialog, overlay-anchor and bar-region distributions remain within 0.035.
    public const double RoiFeatureDifferenceThreshold = 0.055;
    public const double BarGeometryDifferenceThreshold = 0.035;
    public const double BarFillDifferenceThreshold = 0.045;
    public const double LocatorTargetDifferenceThreshold = 0.015;

    private readonly PokemonGoGameStateDetector _detector = new();
    private readonly VisualControlLocator _locator = new();
    private readonly AppraisalAnalyzer _analyzer = new();
    private RecoveryFrame? _current;
    private RecoveryFrame? _origin;
    private PokemonGoGameState? _pendingExpected;
    private RecoveryFrameKind? _pendingExpectedKind;
    private RecoveryFrame? _pendingBefore;
    private bool _terminal;

    public int InputActions { get; private set; }
    public int BackActions { get; private set; }
    public int AppraisalTapActions { get; private set; }
    public RecoveryFrame? Current => _current;

    public RecoveryFrame Observe(
        byte[] screenshotPng,
        AppraisalVisualProfile? appraisalProfile = null)
    {
        ArgumentNullException.ThrowIfNull(screenshotPng);
        var image = PngDecoder.Decode(screenshotPng);
        var detection = _detector.Detect(screenshotPng, appraisalProfile);
        var intro = _locator.LocateAppraisalIntroContinue(screenshotPng);
        var detailsTopology = _locator.LocateDetailsPageTopology(screenshotPng);
        if (detection.State == PokemonGoGameState.Unknown && detailsTopology is not null)
        {
            detection = detection with
            {
                State = PokemonGoGameState.PokemonDetails,
                Confidence = Math.Max(detection.Confidence, detailsTopology.Confidence),
                Evidence = detection.Evidence
                    .Concat(detailsTopology.Evidence)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
            };
        }
        AppraisalAnalysisResult? appraisal = appraisalProfile is null
            ? null
            : _analyzer.Analyze(image, appraisalProfile);
        var hasBars = appraisal is { IsAppraisal: true, Confidence: >= 0.90 };
        var hasIntro = !hasBars &&
            intro is { Confidence: >= MinimumIntroLocatorConfidence };
        var conflicting =
            (hasBars && detection.State != PokemonGoGameState.Appraisal) ||
            (hasIntro && detection.State != PokemonGoGameState.Appraisal);

        var kind = conflicting
            ? RecoveryFrameKind.Conflicting
            : detection.State == PokemonGoGameState.Unknown
                ? RecoveryFrameKind.Unknown
                : hasBars
                    ? RecoveryFrameKind.AppraisalBars
                    : hasIntro
                        ? RecoveryFrameKind.AppraisalIntro
                        : RecoveryFrameKind.Other;

        var stableRegions = kind switch
        {
            RecoveryFrameKind.AppraisalIntro => new[]
            {
                Signature(image, new NormalizedRegion
                    { X = 0.02, Y = 0.82, Width = 0.96, Height = 0.15 }),
                Signature(image, new NormalizedRegion
                    { X = 0.20, Y = 0.50, Width = 0.60, Height = 0.32 })
            },
            RecoveryFrameKind.AppraisalBars => new[]
            {
                // Fixed Attack/Defense/HP labels and left edge of the frame.
                Signature(image, new NormalizedRegion
                    { X = 0.06, Y = 0.56, Width = 0.13, Height = 0.29 })
            },
            _ => Array.Empty<RecoveryRoiSignature>()
        };

        var bars = hasBars
            ? appraisal!.Bars.OrderBy(value => value.Kind)
                .Select(value => BarSignature(image, value)).ToArray()
            : Array.Empty<RecoveryBarSignature>();

        return new RecoveryFrame
        {
            Detection = detection,
            Screenshot = screenshotPng,
            EvidenceSignature = EvidenceSignature(detection),
            Kind = kind,
            HasIntroAnchor = hasIntro,
            HasBarsAnchor = hasBars,
            HasConflictingAnchor = conflicting,
            LocatorConfidence = hasIntro ? intro!.Confidence : null,
            LocatorTarget = hasIntro
                ? new NormalizedPoint { X = 0.1001, Y = 0.5002 }
                : null,
            StableRegions = stableRegions,
            Bars = bars
        };
    }

    public static string EvidenceSignature(PokemonGoGameStateDetection detection) =>
        string.Join("|", detection.State,
            detection.Evidence.OrderBy(value => value, StringComparer.Ordinal));

    public static bool IsStable(IReadOnlyList<RecoveryFrame> frames) =>
        TryGetStableFrame(frames, out _);

    public static bool TryGetStableFrame(
        IReadOnlyList<RecoveryFrame> frames,
        out RecoveryFrame? stable)
    {
        stable = null;
        var window = frames.TakeLast(ConsensusWindow).ToArray();
        if (window.Length < ConsensusMatches || window.Any(IsUnsafe))
        {
            return false;
        }

        for (var first = 0; first <= window.Length - ConsensusMatches; first++)
        for (var second = first + 1; second <= window.Length - 2; second++)
        for (var third = second + 1; third < window.Length; third++)
        {
            var candidate = new[] { window[first], window[second], window[third] };
            if (candidate.All(frame =>
                    frame.Kind == candidate[0].Kind &&
                    frame.Detection.State == candidate[0].Detection.State &&
                    frame.EvidenceSignature == candidate[0].EvidenceSignature) &&
                Compatible(candidate[0], candidate[1]) &&
                Compatible(candidate[0], candidate[2]) &&
                Compatible(candidate[1], candidate[2]))
            {
                stable = candidate[^1];
                return true;
            }
        }

        return false;
    }

    public static AppraisalContinuationOutcome DecideAppraisalContinuation(
        IReadOnlyList<RecoveryFrame> frames,
        int introTapActions,
        bool tapWasSent)
    {
        if (introTapActions is < 0 or > 1 ||
            !TryGetStableFrame(frames, out var stable) ||
            stable is null)
        {
            return introTapActions > 1
                ? AppraisalContinuationOutcome.FAIL_CLOSED
                : AppraisalContinuationOutcome.WAITING_FOR_STABILITY;
        }

        if (stable.Kind == RecoveryFrameKind.AppraisalBars &&
            stable.HasBarsAnchor && !stable.HasIntroAnchor)
        {
            return tapWasSent
                ? AppraisalContinuationOutcome.SUCCESS_TAPPED
                : AppraisalContinuationOutcome.SUCCESS_ALREADY_ADVANCED;
        }

        if (!tapWasSent && introTapActions == 0 &&
            stable.Kind == RecoveryFrameKind.AppraisalIntro &&
            stable.HasIntroAnchor && !stable.HasBarsAnchor &&
            stable.LocatorConfidence >= MinimumIntroLocatorConfidence &&
            stable.LocatorTarget is not null)
        {
            return AppraisalContinuationOutcome.TAP_INTRO_ONCE;
        }

        return AppraisalContinuationOutcome.FAIL_CLOSED;
    }

    public RecoveryOutcome Begin(IReadOnlyList<RecoveryFrame> frames)
    {
        Reset();
        if (!TryGetStableFrame(frames, out var stable) || stable is null)
        {
            return frames.TakeLast(ConsensusWindow).Any(IsUnknown)
                ? RecoveryOutcome.UNKNOWN_STOP
                : RecoveryOutcome.STABILITY_TIMEOUT;
        }

        _current = stable;
        _origin = stable;
        return stable.Detection.State == PokemonGoGameState.Inventory
            ? RecoveryOutcome.SUCCEEDED
            : IsRecoverable(stable.Detection.State)
                ? RecoveryOutcome.PROGRESSED
                : RecoveryOutcome.UNEXPECTED_STOP;
    }

    public RecoveryActionAuthorization? AuthorizeNextAction()
    {
        if (_current is null || _origin is null || _pendingExpected is not null || _terminal ||
            !IsRecoverable(_current.Detection.State) ||
            InputActions >= MaximumActions(_origin))
        {
            return null;
        }

        var action = _current.Detection.State == PokemonGoGameState.Appraisal
            ? RecoveryInputAction.ExitAppraisal
            : RecoveryInputAction.PressBack;
        var expected = (_current.Detection.State, _current.Kind) switch
        {
            (PokemonGoGameState.Appraisal, RecoveryFrameKind.AppraisalIntro) =>
                PokemonGoGameState.Appraisal,
            (PokemonGoGameState.Appraisal, RecoveryFrameKind.AppraisalBars) =>
                PokemonGoGameState.PokemonDetails,
            (PokemonGoGameState.PokemonMenu, _) => PokemonGoGameState.Inventory,
            (PokemonGoGameState.PokemonDetails, _) => PokemonGoGameState.Inventory,
            _ => PokemonGoGameState.Unknown
        };
        if (expected == PokemonGoGameState.Unknown)
        {
            return null;
        }

        _pendingExpected = expected;
        _pendingExpectedKind =
            _current.Kind == RecoveryFrameKind.AppraisalIntro
                ? RecoveryFrameKind.AppraisalBars
                : null;
        _pendingBefore = _current;
        InputActions++;
        if (action == RecoveryInputAction.PressBack)
        {
            BackActions++;
        }
        else
        {
            AppraisalTapActions++;
        }

        return new RecoveryActionAuthorization
        {
            Sequence = InputActions,
            Action = action,
            StateBefore = _current.Detection.State,
            ExpectedState = expected,
            ExpectedFrameKind = _pendingExpectedKind,
            Target = action == RecoveryInputAction.ExitAppraisal
                ? new NormalizedPoint { X = 0.1001, Y = 0.5002 }
                : null,
            Detail = action == RecoveryInputAction.ExitAppraisal
                ? "One state-validated normalized ExitAppraisal tap."
                : "One state-validated Android Back."
        };
    }

    public RecoveryOutcome ObservePostAction(IReadOnlyList<RecoveryFrame> frames)
    {
        if (_pendingExpected is null || _pendingBefore is null)
        {
            return RecoveryOutcome.UNEXPECTED_STOP;
        }

        if (!TryGetStableFrame(frames, out var stable) || stable is null)
        {
            ClearPending();
            return frames.TakeLast(ConsensusWindow).Any(IsUnknown)
                ? RecoveryOutcome.UNKNOWN_STOP
                : RecoveryOutcome.STABILITY_TIMEOUT;
        }

        var expected = _pendingExpected.Value;
        var expectedKind = _pendingExpectedKind;
        var before = _pendingBefore;
        ClearPending();
        _current = stable;

        if (stable.Detection.State == expected &&
            (expectedKind is null || stable.Kind == expectedKind))
        {
            return expected == PokemonGoGameState.Inventory
                ? RecoveryOutcome.SUCCEEDED
                : RecoveryOutcome.PROGRESSED;
        }

        if (stable.Detection.State == before.Detection.State &&
            stable.Kind == before.Kind &&
            stable.EvidenceSignature == before.EvidenceSignature)
        {
            _terminal = true;
            return RecoveryOutcome.ACTION_NOT_OBSERVED;
        }

        _terminal = true;
        return stable.Detection.State == PokemonGoGameState.Unknown
            ? RecoveryOutcome.UNKNOWN_STOP
            : RecoveryOutcome.UNEXPECTED_STOP;
    }

    private static bool Compatible(RecoveryFrame left, RecoveryFrame right)
    {
        if (left.Kind != right.Kind || left.HasConflictingAnchor || right.HasConflictingAnchor)
        {
            return false;
        }

        if (left.Kind == RecoveryFrameKind.AppraisalIntro)
        {
            return left.HasIntroAnchor && right.HasIntroAnchor &&
                !left.HasBarsAnchor && !right.HasBarsAnchor &&
                left.LocatorConfidence >= MinimumIntroLocatorConfidence &&
                right.LocatorConfidence >= MinimumIntroLocatorConfidence &&
                left.LocatorTarget is not null && right.LocatorTarget is not null &&
                Math.Abs(left.LocatorTarget.X - right.LocatorTarget.X) <= LocatorTargetDifferenceThreshold &&
                Math.Abs(left.LocatorTarget.Y - right.LocatorTarget.Y) <= LocatorTargetDifferenceThreshold &&
                RegionsCompatible(left.StableRegions, right.StableRegions);
        }

        if (left.Kind == RecoveryFrameKind.AppraisalBars)
        {
            return left.HasBarsAnchor && right.HasBarsAnchor &&
                !left.HasIntroAnchor && !right.HasIntroAnchor &&
                RegionsCompatible(left.StableRegions, right.StableRegions) &&
                BarsCompatible(left.Bars, right.Bars);
        }

        return left.Kind == RecoveryFrameKind.Other;
    }

    private static bool RegionsCompatible(
        IReadOnlyList<RecoveryRoiSignature> left,
        IReadOnlyList<RecoveryRoiSignature> right) =>
        left.Count == right.Count && left.Count > 0 &&
        left.Zip(right, Difference).All(value =>
            value <= RoiFeatureDifferenceThreshold);

    private static bool BarsCompatible(
        IReadOnlyList<RecoveryBarSignature> left,
        IReadOnlyList<RecoveryBarSignature> right)
    {
        if (left.Count != 3 || right.Count != 3)
        {
            return false;
        }

        return left.Zip(right).All(pair =>
            pair.First.Kind == pair.Second.Kind &&
            RegionDifference(pair.First.Region, pair.Second.Region) <=
                BarGeometryDifferenceThreshold &&
            Math.Abs(pair.First.TrackStartFraction - pair.Second.TrackStartFraction) <=
                BarGeometryDifferenceThreshold &&
            Math.Abs(pair.First.TrackEndFraction - pair.Second.TrackEndFraction) <=
                BarGeometryDifferenceThreshold &&
            Math.Abs(pair.First.TrackWidthFraction - pair.Second.TrackWidthFraction) <=
                BarGeometryDifferenceThreshold &&
            Math.Abs(pair.First.FillFraction - pair.Second.FillFraction) <=
                BarFillDifferenceThreshold &&
            Difference(pair.First.ColorAndStructure, pair.Second.ColorAndStructure) <=
                RoiFeatureDifferenceThreshold);
    }

    private static RecoveryBarSignature BarSignature(
        PixelImage image,
        AppraisalBarMeasurement measurement)
    {
        var denominator = Math.Max(1, measurement.RegionPixelWidth - 1);
        return new RecoveryBarSignature
        {
            Kind = measurement.Kind,
            Region = measurement.Region,
            TrackStartFraction = measurement.TrackStartColumn < 0
                ? -1
                : measurement.TrackStartColumn / (double)denominator,
            TrackEndFraction = measurement.TrackEndColumn < 0
                ? -1
                : measurement.TrackEndColumn / (double)denominator,
            FillFraction = measurement.FillFraction,
            TrackWidthFraction = measurement.TrackWidthFraction,
            ColorAndStructure = Signature(image, measurement.Region)
        };
    }

    private static RecoveryRoiSignature Signature(
        PixelImage image,
        NormalizedRegion region)
    {
        var rectangle = region.ToPixels(image.Width, image.Height);
        var histogram = new double[24];
        var spatialLuminance = new double[16];
        var spatialCounts = new int[16];
        var samples = 0;
        var xStep = Math.Max(1, rectangle.Width / 64);
        var yStep = Math.Max(1, rectangle.Height / 64);
        for (var y = 0; y < rectangle.Height; y += yStep)
        for (var x = 0; x < rectangle.Width; x += xStep)
        {
            var pixel = image.GetPixel(rectangle.X + x, rectangle.Y + y);
            histogram[Math.Min(7, pixel.R / 32)]++;
            histogram[8 + Math.Min(7, pixel.G / 32)]++;
            histogram[16 + Math.Min(7, pixel.B / 32)]++;
            var blockX = Math.Min(3, x * 4 / Math.Max(1, rectangle.Width));
            var blockY = Math.Min(3, y * 4 / Math.Max(1, rectangle.Height));
            var block = blockY * 4 + blockX;
            spatialLuminance[block] +=
                (pixel.R * 0.2126 + pixel.G * 0.7152 + pixel.B * 0.0722) / 255;
            spatialCounts[block]++;
            samples++;
        }

        for (var index = 0; index < histogram.Length; index++)
        {
            histogram[index] /= Math.Max(1, samples);
        }
        for (var index = 0; index < spatialLuminance.Length; index++)
        {
            spatialLuminance[index] /= Math.Max(1, spatialCounts[index]);
        }

        return new RecoveryRoiSignature(histogram.Concat(spatialLuminance).ToArray());
    }

    private static double Difference(RecoveryRoiSignature left, RecoveryRoiSignature right)
    {
        if (left.Features.Count != right.Features.Count || left.Features.Count == 0)
        {
            return 1;
        }

        return left.Features.Zip(right.Features,
            (first, second) => Math.Abs(first - second)).Average();
    }

    private static double RegionDifference(NormalizedRegion left, NormalizedRegion right) =>
        new[]
        {
            Math.Abs(left.X - right.X), Math.Abs(left.Y - right.Y),
            Math.Abs(left.Width - right.Width), Math.Abs(left.Height - right.Height)
        }.Max();

    private static bool IsUnsafe(RecoveryFrame frame) =>
        IsUnknown(frame) || frame.Kind == RecoveryFrameKind.Conflicting ||
        frame.HasConflictingAnchor;

    private static bool IsUnknown(RecoveryFrame frame) =>
        frame.Detection.State == PokemonGoGameState.Unknown ||
        frame.Kind == RecoveryFrameKind.Unknown;

    private static bool IsRecoverable(PokemonGoGameState state) =>
        state is PokemonGoGameState.Appraisal or
            PokemonGoGameState.PokemonMenu or
            PokemonGoGameState.PokemonDetails;

    private static int MaximumActions(RecoveryFrame origin) =>
        (origin.Detection.State, origin.Kind) switch
    {
        (PokemonGoGameState.Appraisal, RecoveryFrameKind.AppraisalIntro) =>
            MaxAppraisalTotalActions,
        (PokemonGoGameState.Appraisal, RecoveryFrameKind.AppraisalBars) => 2,
        (PokemonGoGameState.PokemonMenu, _) => 1,
        (PokemonGoGameState.PokemonDetails, _) => 1,
        _ => 0
    };

    private void ClearPending()
    {
        _pendingExpected = null;
        _pendingExpectedKind = null;
        _pendingBefore = null;
    }

    private void Reset()
    {
        _current = null;
        _origin = null;
        _pendingExpected = null;
        _pendingExpectedKind = null;
        _pendingBefore = null;
        _terminal = false;
        InputActions = 0;
        BackActions = 0;
        AppraisalTapActions = 0;
    }
}
