using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Observations.Models;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Observations.Providers;

public sealed class AppraisalProfileObservationProvider : ICalcyObservationProvider
{
    private readonly AppraisalVisualProfile _profile;
    private readonly AppraisalAnalyzer _analyzer;

    public AppraisalProfileObservationProvider(
        AppraisalVisualProfile profile,
        AppraisalAnalyzer? analyzer = null)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _profile.Validate();
        _analyzer = analyzer ?? new AppraisalAnalyzer();
    }

    public string Name => $"AppraisalProfile:{_profile.ProfileId}";

    public Task<CalcyObservation> ObserveAsync(
        CalcyObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var image = PngDecoder.Decode(request.ScreenshotPng);
            var analysis = _analyzer.Analyze(image, _profile);
            var observation = new CalcyObservation
            {
                ProviderName = Name,
                ProviderVersion = _profile.ProfileId,
                Status = CalcyObservationStatus.Partial,
                Confidence = Math.Clamp(analysis.Confidence, 0, 1),
                AttackIv = analysis.AttackIv,
                DefenseIv = analysis.DefenseIv,
                HpIv = analysis.HpIv,
                ErrorCode = analysis.IsAppraisal ? null : "AppraisalCandidateNotConfirmed",
                ErrorDetail = analysis.IsAppraisal ? null : analysis.Detail
            };

            observation.Validate();
            return Task.FromResult(CalcyObservation.WithRawOutput(observation));
        }
        catch (Exception exception)
        {
            var observation = new CalcyObservation
            {
                ProviderName = Name,
                ProviderVersion = _profile.ProfileId,
                Status = CalcyObservationStatus.Partial,
                Confidence = 0,
                ErrorCode = exception.GetType().Name,
                ErrorDetail = exception.Message
            };

            observation.Validate();
            return Task.FromResult(CalcyObservation.WithRawOutput(observation));
        }
    }
}
