using System.Security.Cryptography;
using System.Text;

namespace PogoInventory.Observations.Models;

public sealed record CalcyObservation
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string ProviderName { get; init; }
    public string? ProviderVersion { get; init; }
    public CalcyObservationStatus Status { get; init; } = CalcyObservationStatus.Unavailable;
    public double Confidence { get; init; }
    public string? Species { get; init; }
    public int? PokedexNumber { get; init; }
    public string? Form { get; init; }
    public int? Cp { get; init; }
    public int? Hp { get; init; }
    public decimal? Level { get; init; }
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public string? Gender { get; init; }
    public string? FastMove { get; init; }
    public string? ChargedMove1 { get; init; }
    public string? ChargedMove2 { get; init; }
    public string? RawProviderOutput { get; init; }
    public string? RawProviderOutputSha256 { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public bool HasAnyPokemonData =>
        !string.IsNullOrWhiteSpace(Species) ||
        PokedexNumber is not null ||
        Cp is not null ||
        Hp is not null ||
        Level is not null ||
        AttackIv is not null ||
        DefenseIv is not null ||
        HpIv is not null;

    public bool HasCompleteIv =>
        AttackIv is not null &&
        DefenseIv is not null &&
        HpIv is not null;

    public static CalcyObservation Unavailable(
        string providerName,
        string? detail = null) =>
        new()
        {
            ProviderName = providerName,
            Status = CalcyObservationStatus.Unavailable,
            Confidence = 0,
            ErrorCode = "ProviderUnavailable",
            ErrorDetail = detail
        };

    public static CalcyObservation Failed(
        string providerName,
        string errorCode,
        string detail,
        string? rawOutput = null) =>
        WithRawOutput(new CalcyObservation
        {
            ProviderName = providerName,
            Status = CalcyObservationStatus.Failed,
            Confidence = 0,
            ErrorCode = errorCode,
            ErrorDetail = detail,
            RawProviderOutput = rawOutput
        });

    public static CalcyObservation WithRawOutput(CalcyObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var hash = string.IsNullOrEmpty(observation.RawProviderOutput)
            ? null
            : Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(observation.RawProviderOutput))).ToLowerInvariant();
        return observation with { RawProviderOutputSha256 = hash };
    }

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new InvalidOperationException(
                $"Unsupported Calcy observation schema '{SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(ProviderName))
        {
            throw new InvalidOperationException("Observation provider name is required.");
        }

        if (!double.IsFinite(Confidence) || Confidence < 0 || Confidence > 1)
        {
            throw new InvalidOperationException("Observation confidence must be finite and between 0 and 1.");
        }

        ValidateRange(PokedexNumber, 1, 10000, nameof(PokedexNumber));
        ValidateRange(Cp, 10, 100000, nameof(Cp));
        ValidateRange(Hp, 1, 10000, nameof(Hp));
        ValidateRange(AttackIv, 0, 15, nameof(AttackIv));
        ValidateRange(DefenseIv, 0, 15, nameof(DefenseIv));
        ValidateRange(HpIv, 0, 15, nameof(HpIv));

        if (Level is { } level && (level < 1 || level > 100))
        {
            throw new InvalidOperationException("Level must be between 1 and 100 when present.");
        }

        if (Status == CalcyObservationStatus.Complete &&
            ((string.IsNullOrWhiteSpace(Species) && PokedexNumber is null) ||
             Cp is null ||
             !HasCompleteIv))
        {
            throw new InvalidOperationException(
                "A complete observation requires species or Pokédex number, CP and all three IV values.");
        }

        if (RawProviderOutput is null && RawProviderOutputSha256 is not null)
        {
            throw new InvalidOperationException(
                "Raw output hash cannot be present when raw provider output is missing.");
        }

        if (RawProviderOutput is not null)
        {
            var expected = WithRawOutput(this).RawProviderOutputSha256;
            if (!string.Equals(
                    expected,
                    RawProviderOutputSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Raw provider output hash is invalid.");
            }
        }
    }

    private static void ValidateRange(int? value, int minimum, int maximum, string name)
    {
        if (value is { } actual && (actual < minimum || actual > maximum))
        {
            throw new InvalidOperationException(
                $"{name} must be between {minimum} and {maximum} when present.");
        }
    }
}
