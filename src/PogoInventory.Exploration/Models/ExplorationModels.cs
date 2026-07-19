using PogoInventory.Automation.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Exploration.Models;

public enum ExplorationRiskLevel { ReadOnly, ReversibleMutation, SensitiveCancelable, Irreversible }

public sealed record ExplorationSession
{
    public required string SessionId { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public required string DeviceSerial { get; init; }
    public required string OutputDirectory { get; init; }
    public int ActionsExecuted { get; init; }
}

public sealed record VisualFeatureSet
{
    public required string ScreenshotSha256 { get; init; }
    public string? PerceptualHash { get; init; }
    public IReadOnlyList<string> VisibleAnchors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> VisibleControls { get; init; } = Array.Empty<string>();
}

public sealed record UiHierarchySnapshot
{
    public required string Path { get; init; }
    public required string Sha256 { get; init; }
    public string? ForegroundPackage { get; init; }
    public IReadOnlyList<string> NodeSignatures { get; init; } = Array.Empty<string>();
}

public sealed record ScreenObservation
{
    public required string SessionId { get; init; }
    public required string ObservationId { get; init; }
    public required DateTimeOffset CapturedAtUtc { get; init; }
    public required string ScreenshotPath { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public string? PerceptualHash { get; init; }
    public string? ClusterId { get; init; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }
    public string? ForegroundPackage { get; init; }
    public ScreenState DetectedState { get; init; }
    public double StateConfidence { get; init; }
    public IReadOnlyList<ScreenState> AlternativeStates { get; init; } = Array.Empty<ScreenState>();
    public string? OcrText { get; init; }
    public string? UiHierarchyPath { get; init; }
    public IReadOnlyList<string> VisibleAnchors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> VisibleControls { get; init; } = Array.Empty<string>();
    public string? ModalClassification { get; init; }
    public double NoveltyScore { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
}

public sealed record ExplorationAction
{
    public required string ActionName { get; init; }
    public required string ActionType { get; init; }
    public NormalizedPoint? NormalizedTarget { get; init; }
    public string? TextValue { get; init; }
    public ExplorationRiskLevel RiskLevel { get; init; }
    public IReadOnlyList<ScreenState> ExpectedFromStates { get; init; } = Array.Empty<ScreenState>();
    public IReadOnlyList<ScreenState> ExpectedToStates { get; init; } = Array.Empty<ScreenState>();
    public bool RequiresAnchor { get; init; }
    public bool RequiresUiNode { get; init; }
    public bool Reversible { get; init; }
    public bool ProductionEligible { get; init; }
}

public sealed record ActionAttempt
{
    public required int Sequence { get; init; }
    public required string BeforeObservation { get; init; }
    public required ExplorationAction Action { get; init; }
    public required string AfterObservation { get; init; }
    public bool Success { get; init; }
    public bool ScreenshotChanged { get; init; }
    public bool UiHierarchyChanged { get; init; }
    public bool ExpectedStateReached { get; init; }
    public bool RecoveryRequired { get; init; }
    public bool RecoverySucceeded { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
}

public sealed record StateTransition
{
    public required string BeforeObservation { get; init; }
    public required string ActionName { get; init; }
    public required string AfterObservation { get; init; }
    public int Attempts { get; init; }
    public int Successes { get; init; }
    public double SuccessRate => Attempts == 0 ? 0 : (double)Successes / Attempts;
}

public sealed record ScreenCluster
{
    public required string ClusterId { get; init; }
    public required IReadOnlyList<string> ObservationIds { get; init; }
    public double Confidence { get; init; }
}

public sealed record ExplorationCoverage
{
    public int ObservationCount { get; init; }
    public int ActionCount { get; init; }
    public int VerifiedStateCount { get; init; }
    public int VerifiedTransitionCount { get; init; }
    public int RecoveryRouteCount { get; init; }
}

public sealed record RecoveryRoute
{
    public required string FromState { get; init; }
    public required IReadOnlyList<string> Actions { get; init; }
    public required string DestinationState { get; init; }
    public double Confidence { get; init; }
}
