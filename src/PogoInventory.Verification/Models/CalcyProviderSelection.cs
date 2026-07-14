namespace PogoInventory.Verification.Models;

public sealed record CalcyProviderSelection
{
    public string SchemaVersion { get; init; } = "1.0";
    public required CalcyProviderMechanism Mechanism { get; init; }
    public required string ProviderVersion { get; init; }
    public required DateTimeOffset SelectedAtUtc { get; init; }
    public required string VerificationReportPath { get; init; }
    public required string VerificationReportSha256 { get; init; }
    public string? ParserProfilePath { get; init; }
    public string? ParserProfileSha256 { get; init; }
    public int VerifiedCaseCount { get; init; }
    public int ExactCompleteCount { get; init; }
    public double ExactCompleteRate { get; init; }
    public int WrongCompleteCount { get; init; }

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported provider selection schema '{SchemaVersion}'.");
        }

        if (Mechanism == CalcyProviderMechanism.Unknown)
        {
            throw new InvalidOperationException("A provider mechanism must be selected.");
        }

        if (string.IsNullOrWhiteSpace(ProviderVersion))
        {
            throw new InvalidOperationException("Provider version is required.");
        }

        if (string.IsNullOrWhiteSpace(VerificationReportSha256))
        {
            throw new InvalidOperationException("Verification report hash is required.");
        }

        if ((ParserProfilePath is null) != (ParserProfileSha256 is null))
        {
            throw new InvalidOperationException(
                "Parser profile path and hash must either both be present or both be absent.");
        }

        if (WrongCompleteCount != 0)
        {
            throw new InvalidOperationException(
                "A provider selection cannot contain false Complete observations.");
        }
    }
}
