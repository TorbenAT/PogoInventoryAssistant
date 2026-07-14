namespace PogoInventory.Verification.Models;

public sealed record ExpectedPokemonObservation
{
    public string? Species { get; init; }
    public int? PokedexNumber { get; init; }
    public int? Cp { get; init; }
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }

    public bool IsComplete =>
        (!string.IsNullOrWhiteSpace(Species) || PokedexNumber is not null) &&
        Cp is not null &&
        AttackIv is not null &&
        DefenseIv is not null &&
        HpIv is not null;

    public void ValidateForRun()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException(
                "Expected verification data requires species or Pokédex number, CP and all three IV values.");
        }

        ValidateRange(PokedexNumber, 1, 10000, nameof(PokedexNumber));
        ValidateRange(Cp, 10, 100000, nameof(Cp));
        ValidateRange(AttackIv, 0, 15, nameof(AttackIv));
        ValidateRange(DefenseIv, 0, 15, nameof(DefenseIv));
        ValidateRange(HpIv, 0, 15, nameof(HpIv));
    }

    private static void ValidateRange(int? value, int minimum, int maximum, string name)
    {
        if (value is { } actual && (actual < minimum || actual > maximum))
        {
            throw new InvalidOperationException(
                $"{name} must be between {minimum} and {maximum}.");
        }
    }
}
