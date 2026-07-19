using System.Globalization;
using System.Text.RegularExpressions;
using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Parsing;

public sealed class CalcyRawTextParser
{
    public CalcyObservation Parse(
        CalcyTextParserProfile profile,
        CalcyRawOutputBundle bundle,
        string providerName)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        profile.Validate();

        var candidates = new Dictionary<CalcyTextField, HashSet<string>>();
        var warnings = new List<string>();

        foreach (var definition in profile.Patterns)
        {
            var sources = string.IsNullOrWhiteSpace(definition.SourceName)
                ? bundle.Sources
                : bundle.Sources.Where(pair => string.Equals(
                    pair.Key,
                    definition.SourceName,
                    StringComparison.OrdinalIgnoreCase));

            var options = RegexOptions.CultureInvariant |
                (definition.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
            var expression = new Regex(
                definition.Pattern,
                options,
                TimeSpan.FromSeconds(1));

            foreach (var source in sources)
            {
                foreach (Match match in expression.Matches(source.Value ?? string.Empty))
                {
                    var value = match.Groups[definition.GroupName].Value.Trim();
                    if (value.Length == 0)
                    {
                        continue;
                    }

                    if (!candidates.TryGetValue(definition.Field, out var values))
                    {
                        values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        candidates[definition.Field] = values;
                    }

                    values.Add(value);
                }
            }
        }

        var conflicts = candidates
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => pair.Key)
            .OrderBy(field => field)
            .ToArray();
        foreach (var conflict in conflicts)
        {
            warnings.Add(
                $"Conflicting values were found for {conflict}: {string.Join(", ", candidates[conflict].OrderBy(value => value, StringComparer.OrdinalIgnoreCase))}.");
        }

        string? Text(CalcyTextField field) =>
            SingleValue(candidates, conflicts, field);
        int? Integer(CalcyTextField field, int minimum, int maximum) =>
            ParseInteger(Text(field), field, minimum, maximum, warnings);
        decimal? Decimal(CalcyTextField field, decimal minimum, decimal maximum) =>
            ParseDecimal(Text(field), field, minimum, maximum, warnings);

        var species = Text(CalcyTextField.Species);
        var pokedexNumber = Integer(CalcyTextField.PokedexNumber, 1, 10000);
        var cp = Integer(CalcyTextField.Cp, 10, 100000);
        var hp = Integer(CalcyTextField.Hp, 1, 10000);
        var level = Decimal(CalcyTextField.Level, 1, 100);
        var attackIv = Integer(CalcyTextField.AttackIv, 0, 15);
        var defenseIv = Integer(CalcyTextField.DefenseIv, 0, 15);
        var hpIv = Integer(CalcyTextField.HpIv, 0, 15);
        var catchLocation = Text(CalcyTextField.CatchLocation);

        var hasAny = !string.IsNullOrWhiteSpace(species) ||
            pokedexNumber is not null ||
            cp is not null ||
            hp is not null ||
            level is not null ||
            attackIv is not null ||
            defenseIv is not null ||
            hpIv is not null ||
            !string.IsNullOrWhiteSpace(Text(CalcyTextField.Form)) ||
            !string.IsNullOrWhiteSpace(Text(CalcyTextField.Gender)) ||
            !string.IsNullOrWhiteSpace(Text(CalcyTextField.FastMove)) ||
            !string.IsNullOrWhiteSpace(Text(CalcyTextField.ChargedMove1)) ||
            !string.IsNullOrWhiteSpace(Text(CalcyTextField.ChargedMove2)) ||
            !string.IsNullOrWhiteSpace(catchLocation);
        var complete = (species is not null || pokedexNumber is not null) &&
            cp is not null &&
            attackIv is not null &&
            defenseIv is not null &&
            hpIv is not null;

        var status = conflicts.Length > 0
            ? CalcyObservationStatus.Conflicting
            : complete
                ? CalcyObservationStatus.Complete
                : hasAny
                    ? CalcyObservationStatus.Partial
                    : CalcyObservationStatus.Failed;

        var coreScore = 0;
        coreScore += species is not null || pokedexNumber is not null ? 1 : 0;
        coreScore += cp is not null ? 1 : 0;
        coreScore += attackIv is not null ? 1 : 0;
        coreScore += defenseIv is not null ? 1 : 0;
        coreScore += hpIv is not null ? 1 : 0;
        var confidence = status == CalcyObservationStatus.Conflicting
            ? 0
            : coreScore / 5.0;

        var raw = bundle.ToCombinedText();
        var observation = CalcyObservation.WithRawOutput(new CalcyObservation
        {
            ProviderName = providerName,
            ProviderVersion = profile.ProviderVersion,
            Status = status,
            Confidence = confidence,
            Species = species,
            PokedexNumber = pokedexNumber,
            Form = Text(CalcyTextField.Form),
            Cp = cp,
            Hp = hp,
            Level = level,
            AttackIv = attackIv,
            DefenseIv = defenseIv,
            HpIv = hpIv,
            Gender = Text(CalcyTextField.Gender),
            FastMove = Text(CalcyTextField.FastMove),
            ChargedMove1 = Text(CalcyTextField.ChargedMove1),
            ChargedMove2 = Text(CalcyTextField.ChargedMove2),
            CatchLocation = catchLocation,
            RawProviderOutput = raw,
            ErrorCode = status == CalcyObservationStatus.Failed
                ? "NoRecognizedFields"
                : status == CalcyObservationStatus.Conflicting
                    ? "ConflictingFields"
                    : null,
            ErrorDetail = status == CalcyObservationStatus.Failed
                ? "The configured parser profile did not recognize any Pokémon fields."
                : conflicts.Length > 0
                    ? $"Conflicting fields: {string.Join(", ", conflicts)}."
                    : null,
            Warnings = warnings
        });
        observation.Validate();
        return observation;
    }

    private static string? SingleValue(
        IReadOnlyDictionary<CalcyTextField, HashSet<string>> candidates,
        IReadOnlyCollection<CalcyTextField> conflicts,
        CalcyTextField field)
    {
        if (conflicts.Contains(field) ||
            !candidates.TryGetValue(field, out var values) ||
            values.Count != 1)
        {
            return null;
        }

        return values.Single();
    }

    private static int? ParseInteger(
        string? value,
        CalcyTextField field,
        int minimum,
        int maximum,
        ICollection<string> warnings)
    {
        if (value is null)
        {
            return null;
        }

        if (!int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result) ||
            result < minimum ||
            result > maximum)
        {
            warnings.Add(
                $"Value '{value}' for {field} is not a valid integer between {minimum} and {maximum}.");
            return null;
        }

        return result;
    }

    private static decimal? ParseDecimal(
        string? value,
        CalcyTextField field,
        decimal minimum,
        decimal maximum,
        ICollection<string> warnings)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Replace(',', '.');
        if (!decimal.TryParse(
                normalized,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var result) ||
            result < minimum ||
            result > maximum)
        {
            warnings.Add(
                $"Value '{value}' for {field} is not a valid number between {minimum} and {maximum}.");
            return null;
        }

        return result;
    }
}
