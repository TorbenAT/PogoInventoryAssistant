namespace PogoInventory.Observations.Models;

public sealed record CalcyRawOutputBundle
{
    public required IReadOnlyDictionary<string, string> Sources { get; init; }

    public string ToCombinedText() =>
        string.Join(
            Environment.NewLine,
            Sources.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .SelectMany(pair => new[]
                {
                    $"===== {pair.Key} =====",
                    pair.Value ?? string.Empty
                }));
}
