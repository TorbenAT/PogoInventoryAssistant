using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Providers;

public interface IPokemonObservationProvider
{
    string Name { get; }

    Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default);
}
