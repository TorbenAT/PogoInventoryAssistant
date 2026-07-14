namespace PogoInventory.Verification.Models;

public sealed record CalcyVerificationManifest
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Name { get; init; }
    public CalcyProviderMechanism Mechanism { get; init; }
    public string? ProviderVersion { get; init; }
    public int MinimumCases { get; init; } = 20;
    public double MinimumExactCompleteRate { get; init; } = 0.95;
    public IReadOnlyList<CalcyVerificationCase> Cases { get; init; } =
        Array.Empty<CalcyVerificationCase>();

    public void ValidateForRun()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported verification manifest schema '{SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Verification manifest name is required.");
        }

        if (Mechanism == CalcyProviderMechanism.Unknown)
        {
            throw new InvalidOperationException(
                "Verification manifest must name the candidate provider mechanism.");
        }

        if (MinimumCases < 1 || MinimumCases > 1000)
        {
            throw new InvalidOperationException("MinimumCases must be between 1 and 1000.");
        }

        if (!double.IsFinite(MinimumExactCompleteRate) ||
            MinimumExactCompleteRate < 0 || MinimumExactCompleteRate > 1)
        {
            throw new InvalidOperationException(
                "MinimumExactCompleteRate must be finite and between zero and one.");
        }

        if (Cases.Count == 0)
        {
            throw new InvalidOperationException("Verification manifest contains no cases.");
        }

        var duplicate = Cases.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Duplicate verification case id '{duplicate.Key}'.");
        }

        foreach (var item in Cases)
        {
            item.ValidateForRun();
        }
    }
}
