namespace PogoInventory.Verification.Models;

public sealed record CalcyVerificationCase
{
    public required string Id { get; init; }
    public string? ObservationPath { get; init; }
    public IReadOnlyDictionary<string, string> Sources { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
    public ExpectedPokemonObservation Expected { get; init; } = new();

    public void ValidateForRun()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new InvalidOperationException("Verification case id is required.");
        }

        var hasObservation = !string.IsNullOrWhiteSpace(ObservationPath);
        var hasSources = Sources.Count > 0;
        if (hasObservation == hasSources)
        {
            throw new InvalidOperationException(
                $"Verification case '{Id}' must define exactly one of observationPath or sources.");
        }

        if (hasSources && Sources.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new InvalidOperationException(
                $"Verification case '{Id}' contains an empty source name or path.");
        }

        Expected.ValidateForRun();
    }
}
