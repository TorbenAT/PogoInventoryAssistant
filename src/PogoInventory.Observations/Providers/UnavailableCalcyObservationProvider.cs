using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Providers;

public sealed class UnavailableCalcyObservationProvider : ICalcyObservationProvider
{
    public const string ProviderName = "UnavailableCalcyProvider";

    public string Name => ProviderName;

    public Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CalcyObservation.Unavailable(
            Name,
            "No real Calcy adapter has been configured for this run."));
    }
}
