using PogoInventory.Core.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public sealed record TagPlan
{
    public required string DesiredTag { get; init; }
    public required string RunId { get; init; }
    public required int Ordinal { get; init; }
    public required string StableFingerprint { get; init; }
    public required DecisionCategory DecisionType { get; init; }
    public required string Reason { get; init; }
    public required bool RequiresVisualVerification { get; init; }
    public required bool MayExecute { get; init; }
    public string? RejectionReason { get; init; }
    public required ScreenState VerifiedScreenState { get; init; }
    public required IReadOnlyList<DecisionReason> Evidence { get; init; }
    public required IReadOnlyList<string> EvidenceReferences { get; init; }
}
