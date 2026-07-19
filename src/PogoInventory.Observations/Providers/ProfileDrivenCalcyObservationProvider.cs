using PogoInventory.Observations.Models;
using PogoInventory.Observations.Parsing;
using PogoInventory.Observations.Sources;

namespace PogoInventory.Observations.Providers;

public sealed class ProfileDrivenCalcyObservationProvider : IPokemonObservationProvider
{
    private readonly ICalcyRawOutputSource _source;
    private readonly CalcyTextParserProfile _profile;
    private readonly CalcyRawTextParser _parser;

    public ProfileDrivenCalcyObservationProvider(
        ICalcyRawOutputSource source,
        CalcyTextParserProfile profile,
        CalcyRawTextParser? parser = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profile.Validate();
        _parser = parser ?? new CalcyRawTextParser();
    }

    public string Name => $"ProfileDriven:{_source.Name}";

    public async Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var bundle = await _source.ReadAsync(request, cancellationToken);
            return _parser.Parse(_profile, bundle, Name);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CalcyObservation.Failed(
                Name,
                "RawOutputProviderFailure",
                exception.Message);
        }
    }
}
