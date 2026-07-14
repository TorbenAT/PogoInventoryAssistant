using PogoInventory.Observations.Models;

namespace PogoInventory.Observations.Sources;

public sealed class ScriptedCalcyRawOutputSource : ICalcyRawOutputSource
{
    private readonly IReadOnlyDictionary<int, CalcyRawOutputBundle> _outputs;

    public ScriptedCalcyRawOutputSource(
        IReadOnlyDictionary<int, CalcyRawOutputBundle> outputs,
        string name = "ScriptedCalcyRawOutput")
    {
        _outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
        Name = name;
    }

    public string Name { get; }

    public Task<CalcyRawOutputBundle> ReadAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_outputs.TryGetValue(request.SequenceNumber, out var output))
        {
            output = new CalcyRawOutputBundle
            {
                Sources = new Dictionary<string, string>()
            };
        }

        return Task.FromResult(output);
    }
}
