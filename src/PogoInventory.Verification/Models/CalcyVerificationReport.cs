namespace PogoInventory.Verification.Models;

public sealed record CalcyVerificationReport
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string ManifestName { get; init; }
    public required string ManifestSha256 { get; init; }
    public required CalcyProviderMechanism Mechanism { get; init; }
    public string? ProviderVersion { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public int MinimumCases { get; init; }
    public double MinimumExactCompleteRate { get; init; }
    public int CaseCount { get; init; }
    public int ExactCompleteCount { get; init; }
    public int SafeIncompleteCount { get; init; }
    public int IncorrectIncompleteCount { get; init; }
    public int WrongCompleteCount { get; init; }
    public int ConflictingCount { get; init; }
    public int FailedCount { get; init; }
    public int UnavailableCount { get; init; }
    public int InvalidEvidenceCount { get; init; }
    public double ExactCompleteRate { get; init; }
    public bool ZeroFalseComplete { get; init; }
    public bool SafeForLongScan { get; init; }
    public bool RecommendedForLongScan { get; init; }
    public required string GateDetail { get; init; }
    public IReadOnlyList<CalcyVerificationCaseResult> Cases { get; init; } =
        Array.Empty<CalcyVerificationCaseResult>();

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported verification report schema '{SchemaVersion}'.");
        }

        if (CaseCount != Cases.Count)
        {
            throw new InvalidOperationException("Verification report case count is inconsistent.");
        }

        var counted = ExactCompleteCount + SafeIncompleteCount +
            IncorrectIncompleteCount + WrongCompleteCount + ConflictingCount +
            FailedCount + UnavailableCount + InvalidEvidenceCount;
        if (counted != CaseCount)
        {
            throw new InvalidOperationException("Verification outcome counts are inconsistent.");
        }

        if (string.IsNullOrWhiteSpace(ManifestSha256))
        {
            throw new InvalidOperationException("Verification manifest hash is required.");
        }

        var expectedRate = CaseCount == 0 ? 0 : ExactCompleteCount / (double)CaseCount;
        if (Math.Abs(ExactCompleteRate - expectedRate) > 0.0000001)
        {
            throw new InvalidOperationException("Exact Complete rate is inconsistent.");
        }

        var expectedZeroFalseComplete = WrongCompleteCount == 0;
        if (ZeroFalseComplete != expectedZeroFalseComplete)
        {
            throw new InvalidOperationException("ZeroFalseComplete is inconsistent.");
        }

        var expectedSafe = CaseCount >= MinimumCases &&
            expectedZeroFalseComplete &&
            InvalidEvidenceCount == 0;
        if (SafeForLongScan != expectedSafe)
        {
            throw new InvalidOperationException("SafeForLongScan is inconsistent.");
        }

        var expectedRecommended = expectedSafe &&
            expectedRate >= MinimumExactCompleteRate &&
            IncorrectIncompleteCount == 0 &&
            ConflictingCount == 0 &&
            FailedCount == 0;
        if (RecommendedForLongScan != expectedRecommended)
        {
            throw new InvalidOperationException("RecommendedForLongScan is inconsistent.");
        }
    }
}
