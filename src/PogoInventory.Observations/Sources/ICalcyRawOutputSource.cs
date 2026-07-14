using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Sources;

public interface ICalcyRawOutputSource
{
    string Name { get; }

    Task<CalcyRawOutputBundle> ReadAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default);
}
