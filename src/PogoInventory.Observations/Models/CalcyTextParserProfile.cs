using System.Text.RegularExpressions;

namespace PogoInventory.Observations.Models;

public sealed record CalcyTextParserProfile
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Name { get; init; }
    public required string ProviderVersion { get; init; }
    public IReadOnlyList<CalcyTextPatternDefinition> Patterns { get; init; } =
        Array.Empty<CalcyTextPatternDefinition>();

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported Calcy text parser profile schema '{SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Parser profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(ProviderVersion))
        {
            throw new InvalidOperationException("Parser provider version is required.");
        }

        if (Patterns.Count == 0)
        {
            throw new InvalidOperationException("At least one parser pattern is required.");
        }

        foreach (var definition in Patterns)
        {
            if (string.IsNullOrWhiteSpace(definition.Pattern))
            {
                throw new InvalidOperationException(
                    $"Pattern for {definition.Field} cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(definition.GroupName))
            {
                throw new InvalidOperationException(
                    $"Group name for {definition.Field} cannot be empty.");
            }

            var options = RegexOptions.CultureInvariant |
                (definition.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            var expression = new Regex(
                definition.Pattern,
                options,
                TimeSpan.FromSeconds(1));
            if (!expression.GetGroupNames().Contains(
                    definition.GroupName,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Pattern for {definition.Field} does not define group '{definition.GroupName}'.");
            }
        }
    }
}
