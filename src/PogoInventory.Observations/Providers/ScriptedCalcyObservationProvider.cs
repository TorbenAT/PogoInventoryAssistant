using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Providers;

public sealed class ScriptedCalcyObservationProvider : ICalcyObservationProvider
{
    private readonly IReadOnlyDictionary<int, CalcyObservation> _observations;
    private readonly IReadOnlyDictionary<int, Exception> _exceptions;

    public ScriptedCalcyObservationProvider(
        IReadOnlyDictionary<int, CalcyObservation>? observations = null,
        IReadOnlyDictionary<int, Exception>? exceptions = null,
        string name = "ScriptedCalcyProvider")
    {
        _observations = observations ?? new Dictionary<int, CalcyObservation>();
        _exceptions = exceptions ?? new Dictionary<int, Exception>();
        Name = name;
    }

    public string Name { get; }

    public Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (_exceptions.TryGetValue(request.SequenceNumber, out var exception))
        {
            return Task.FromException<CalcyObservation>(exception);
        }

        var observation = _observations.TryGetValue(request.SequenceNumber, out var value)
            ? value
            : CalcyObservation.Unavailable(
                Name,
                $"No scripted observation exists for sequence {request.SequenceNumber}.");
        return Task.FromResult(observation);
    }
}
