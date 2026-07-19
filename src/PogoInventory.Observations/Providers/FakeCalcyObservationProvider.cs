using System.Text.Json;
using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Providers;

public sealed class FakeCalcyObservationProvider : IPokemonObservationProvider
{
    private static readonly IReadOnlyDictionary<int, CalcyObservation> Observations =
        new Dictionary<int, CalcyObservation>
        {
            [1] = Create(
                "Pikachu",
                25,
                501,
                62,
                20.0m,
                15,
                14,
                13,
                "Male",
                "Thunder Shock",
                "Wild Charge"),
            [2] = Create(
                "Machop",
                66,
                742,
                78,
                24.5m,
                0,
                15,
                15,
                "Female",
                "Karate Chop",
                "Cross Chop"),
            [3] = Create(
                "Eevee",
                133,
                612,
                71,
                22.0m,
                12,
                12,
                12,
                "Female",
                "Quick Attack",
                "Body Slam")
        };

    public string Name => "FakeCalcyProvider";

    public Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var observation = Observations.TryGetValue(request.SequenceNumber, out var value)
            ? value
            : CalcyObservation.Unavailable(
                Name,
                $"No deterministic fake observation exists for sequence {request.SequenceNumber}.");
        return Task.FromResult(observation);
    }

    private static CalcyObservation Create(
        string species,
        int pokedexNumber,
        int cp,
        int hp,
        decimal level,
        int attackIv,
        int defenseIv,
        int hpIv,
        string gender,
        string fastMove,
        string chargedMove)
    {
        var raw = JsonSerializer.Serialize(new
        {
            species,
            pokedexNumber,
            cp,
            hp,
            level,
            attackIv,
            defenseIv,
            hpIv,
            gender,
            fastMove,
            chargedMove
        });

        return CalcyObservation.WithRawOutput(new CalcyObservation
        {
            ProviderName = "FakeCalcyProvider",
            ProviderVersion = "1.0",
            Status = CalcyObservationStatus.Complete,
            Confidence = 1,
            Species = species,
            PokedexNumber = pokedexNumber,
            Form = "Normal",
            Cp = cp,
            Hp = hp,
            Level = level,
            AttackIv = attackIv,
            DefenseIv = defenseIv,
            HpIv = hpIv,
            Gender = gender,
            FastMove = fastMove,
            ChargedMove1 = chargedMove,
            RawProviderOutput = raw
        });
    }
}
