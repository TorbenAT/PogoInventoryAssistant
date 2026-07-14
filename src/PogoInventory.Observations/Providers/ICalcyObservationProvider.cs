using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Providers;

public interface ICalcyObservationProvider
{
    string Name { get; }

    Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default);
}
