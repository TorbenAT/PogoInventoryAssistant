namespace PogoInventory.Application;

public sealed record TagExecutionResult
{
    public required string TagName { get; init; }
    public bool ActionExecuted { get; init; }
    public bool VisuallyVerified { get; init; }
    public string? BeforeScreenshotHash { get; init; }
    public string? AfterScreenshotHash { get; init; }
    public string? AuditReference { get; init; }
    public string? Error { get; init; }

    public bool IsCompleteVerification =>
        ActionExecuted &&
        VisuallyVerified &&
        !string.IsNullOrWhiteSpace(BeforeScreenshotHash) &&
        !string.IsNullOrWhiteSpace(AfterScreenshotHash) &&
        !string.Equals(BeforeScreenshotHash, AfterScreenshotHash, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(AuditReference) &&
        string.IsNullOrWhiteSpace(Error);
}
