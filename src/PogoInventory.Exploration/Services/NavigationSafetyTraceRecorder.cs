using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Device.Transport;
using PogoInventory.Exploration.Models;

namespace PogoInventory.Exploration.Services;

/// <summary>
/// Records phase-aligned evidence for the deterministic navigation acceptance.
/// It observes the named host; it does not locate controls or authorize input.
/// </summary>
public sealed class NavigationSafetyTraceRecorder
{
    public const int RequiredPostInputFrames = 5;

    private readonly string _outputDirectory;
    private readonly string _tracePath;
    private readonly List<ObservedFrame> _recentFrames = new();
    private long _sequence;
    private int _cycle;
    private PendingAction? _pending;

    public NavigationSafetyTraceRecorder(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        _outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(_outputDirectory);
        _tracePath = Path.Combine(_outputDirectory, "action-trace.jsonl");
        File.WriteAllText(_tracePath, string.Empty);
    }

    public void BeginCycle(int cycle)
    {
        if (cycle is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(cycle));
        if (_pending is not null)
            throw new InvalidOperationException("Cannot start a cycle with an unfinished traced action.");
        _cycle = cycle;
        _recentFrames.Clear();
    }

    public async Task ObserveFrameAsync(
        byte[] screenshot,
        PokemonGoGameStateDetection detection,
        PokemonGoGameState? visualFallbackState,
        UnsafeConfirmationKind? unsafeSurface,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(screenshot);
        var frame = new ObservedFrame(
            screenshot,
            detection.State.ToString(),
            visualFallbackState?.ToString(),
            unsafeSurface?.ToString(),
            detection.ScreenshotSha256,
            detection.Confidence,
            detection.Evidence.ToArray());
        _recentFrames.Add(frame);
        if (_recentFrames.Count > 16)
            _recentFrames.RemoveAt(0);

        if (_pending is { InputSent: true } pending &&
            pending.PostFrameCount < RequiredPostInputFrames)
        {
            await WriteFrameAsync(
                pending.OperationId,
                "POST_INPUT_FRAME_" + (pending.PostFrameCount + 1),
                frame,
                pending.ExpectedState,
                "AUTHORIZED",
                transportInputType: pending.TransportInputType,
                inputSent: true,
                postcondition: null,
                cancellationToken: cancellationToken);
            pending.PostFrameCount++;
        }
    }

    public async Task AuthorizeAsync(
        string operationId,
        string expectedState,
        string requiredState,
        string authorizationResult,
        byte[] freshScreenshot,
        PokemonGoGameStateDetection freshDetection,
        PokemonGoGameState? visualFallbackState,
        UnsafeConfirmationKind? unsafeSurface,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedState);
        var recent = _recentFrames.ToArray();
        var freshIsLast = recent.Length > 0 &&
            ReferenceEquals(recent[^1].Screenshot, freshScreenshot);
        var preFrames = recent
            .TakeLast(freshIsLast ? 4 : 3)
            .Take(freshIsLast ? 3 : 3)
            .ToArray();

        for (var index = 0; index < 3; index++)
        {
            var frame = index < preFrames.Length ? preFrames[index] : null;
            await WriteFrameAsync(
                operationId,
                "PRECONDITION_FRAME_" + (index + 1),
                frame,
                expectedState,
                authorizationResult,
                transportInputType: null,
                inputSent: false,
                postcondition: frame is null ? "MISSING_PRECONDITION_FRAME" : null,
                cancellationToken: cancellationToken);
        }

        var fresh = new ObservedFrame(
            freshScreenshot,
            freshDetection.State.ToString(),
            visualFallbackState?.ToString(),
            unsafeSurface?.ToString(),
            freshDetection.ScreenshotSha256,
            freshDetection.Confidence,
            freshDetection.Evidence.ToArray());
        await WriteFrameAsync(
            operationId,
            "AUTHORIZATION",
            fresh,
            expectedState,
            authorizationResult,
            transportInputType: null,
            inputSent: false,
            postcondition: requiredState,
            cancellationToken: cancellationToken,
            requiredState: requiredState);
        await WriteFrameAsync(
            operationId,
            "FRESH_PRE_INPUT_FRAME",
            fresh,
            expectedState,
            authorizationResult,
            transportInputType: null,
            inputSent: false,
            postcondition: null,
            cancellationToken: cancellationToken,
            requiredState: requiredState);

        _pending = authorizationResult == "AUTHORIZED"
            ? new PendingAction(operationId, expectedState)
            : null;
    }

    public Task RecordDeniedAsync(
        string operationId,
        string expectedState,
        string authorizationResult,
        byte[] screenshot,
        PokemonGoGameStateDetection detection,
        PokemonGoGameState? visualFallbackState,
        UnsafeConfirmationKind? unsafeSurface,
        CancellationToken cancellationToken) =>
        AuthorizeAsync(
            operationId,
            expectedState,
            "validated-named-operation",
            authorizationResult,
            screenshot,
            detection,
            visualFallbackState,
            unsafeSurface,
            cancellationToken);

    public async Task RecordInputSentAsync(
        string transportInputType,
        string inputDescription,
        CancellationToken cancellationToken)
    {
        var pending = _pending ?? throw new InvalidOperationException(
            "A traced input cannot be sent before authorization.");
        pending.InputSent = true;
        pending.TransportInputType = transportInputType;
        await WriteFrameAsync(
            pending.OperationId,
            "INPUT_SENT",
            frame: null,
            expectedState: pending.ExpectedState,
            authorizationResult: "AUTHORIZED",
            transportInputType: transportInputType,
            inputSent: true,
            inputDescription: inputDescription,
            postcondition: null,
            cancellationToken: cancellationToken);
    }

    public void RecordPostcondition(string actualState, string result)
    {
        if (_pending is null)
            return;
        _pending.PostconditionState = actualState;
        _pending.PostconditionResult = result;
    }

    public async Task CompletePostFramesAsync(
        IAndroidAutomationTransport transport,
        string serial,
        CancellationToken cancellationToken)
    {
        var pending = _pending;
        if (pending is null)
            return;
        if (!pending.InputSent)
        {
            _pending = null;
            return;
        }

        while (pending.PostFrameCount < RequiredPostInputFrames)
        {
            var screenshot = await transport.CaptureScreenshotPngAsync(serial, cancellationToken);
            var detection = new PokemonGoGameStateDetector().Detect(screenshot);
            var fallback = detection.State == PokemonGoGameState.Unknown &&
                new VisualControlLocator().LocateDetailsPageTopology(screenshot) is not null
                ? PokemonGoGameState.PokemonDetails
                : (PokemonGoGameState?)null;
            await ObserveFrameAsync(screenshot, detection, fallback, null, cancellationToken);
        }

        var postcondition = pending.PostconditionResult ?? "UNRESOLVED";
        if (pending.PostFrameCount != RequiredPostInputFrames)
            postcondition = "FAIL_MISSING_POST_FRAMES";
        await WriteFrameAsync(
            pending.OperationId,
            "POSTCONDITION",
            frame: null,
            expectedState: pending.ExpectedState,
            authorizationResult: "AUTHORIZED",
            transportInputType: pending.TransportInputType,
            inputSent: true,
            inputDescription: null,
            postcondition: pending.PostconditionState is null
                ? postcondition
                : $"{pending.PostconditionState}:{postcondition}",
            cancellationToken: cancellationToken);
        _pending = null;
    }

    private async Task WriteFrameAsync(
        string operationId,
        string phase,
        ObservedFrame? frame,
        string expectedState,
        string authorizationResult,
        string? transportInputType,
        bool inputSent,
        string? postcondition,
        CancellationToken cancellationToken,
        string? requiredState = null,
        string? inputDescription = null)
    {
        string? screenshotPath = null;
        if (frame is not null)
        {
            var operationDirectory = Path.Combine(
                _outputDirectory,
                "screenshots",
                $"cycle-{_cycle:00}",
                Sanitize(operationId));
            Directory.CreateDirectory(operationDirectory);
            var filename = $"{_sequence + 1:0000}-{phase.ToLowerInvariant()}.png";
            screenshotPath = Path.Combine(operationDirectory, filename);
            await File.WriteAllBytesAsync(screenshotPath, frame.Screenshot, cancellationToken);
        }

        var entry = new NavigationSafetyTraceEntry
        {
            OperationId = operationId,
            Cycle = _cycle,
            Phase = phase,
            TimestampUtc = DateTimeOffset.UtcNow,
            MonotonicSequence = ++_sequence,
            StrictDetectedState = frame?.StrictDetectedState ?? "NotApplicable",
            VisualFallbackState = frame?.VisualFallbackState,
            UnsafeSurface = frame?.UnsafeSurface,
            ExpectedState = expectedState,
            RequiredState = requiredState,
            ScreenshotSha256 = frame?.ScreenshotSha256,
            ScreenshotPath = screenshotPath is null ? null : Path.GetRelativePath(_outputDirectory, screenshotPath),
            AuthorizationResult = authorizationResult,
            TransportInputType = transportInputType,
            InputSent = inputSent,
            InputDescription = inputDescription,
            Postcondition = postcondition,
            Confidence = frame?.Confidence,
            Evidence = frame?.Evidence ?? Array.Empty<string>()
        };
        await File.AppendAllTextAsync(
            _tracePath,
            JsonSerializer.Serialize(entry, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull }) + Environment.NewLine,
            cancellationToken);
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

    private sealed class PendingAction(string operationId, string expectedState)
    {
        public string OperationId { get; } = operationId;
        public string ExpectedState { get; } = expectedState;
        public bool InputSent { get; set; }
        public int PostFrameCount { get; set; }
        public string? TransportInputType { get; set; }
        public string? PostconditionState { get; set; }
        public string? PostconditionResult { get; set; }
    }

    private sealed record ObservedFrame(
        byte[] Screenshot,
        string StrictDetectedState,
        string? VisualFallbackState,
        string? UnsafeSurface,
        string ScreenshotSha256,
        double Confidence,
        IReadOnlyList<string> Evidence);
}

public sealed class NavigationSafetyTraceEntry
{
    public required string OperationId { get; init; }
    public required int Cycle { get; init; }
    public required string Phase { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
    public required long MonotonicSequence { get; init; }
    public required string StrictDetectedState { get; init; }
    public string? VisualFallbackState { get; init; }
    public string? UnsafeSurface { get; init; }
    public required string ExpectedState { get; init; }
    public string? RequiredState { get; init; }
    public string? ScreenshotSha256 { get; init; }
    public string? ScreenshotPath { get; init; }
    public required string AuthorizationResult { get; init; }
    public string? TransportInputType { get; init; }
    public required bool InputSent { get; init; }
    public string? InputDescription { get; init; }
    public string? Postcondition { get; init; }
    public double? Confidence { get; init; }
    public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();
}
