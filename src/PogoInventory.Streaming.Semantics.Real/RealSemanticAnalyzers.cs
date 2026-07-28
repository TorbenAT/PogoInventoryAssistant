using System.Security.Cryptography;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.HeaderText;
using PogoInventory.Streaming.Semantics.Shadow;
using PogoInventory.Streaming.Semantics;
using PogoInventory.Vision.Imaging;

namespace PogoInventory.Streaming.Semantics.Real;

public sealed class RealHeaderAnalyzer : IShadowSemanticAnalyzer
{
    private readonly PokemonHeaderAnalyzer _headers;
    private readonly HeaderScreenType _screen;
    public RealHeaderAnalyzer(PokemonHeaderAnalyzer headers, HeaderScreenType screen) { _headers = headers; _screen = screen; }
    public string Name => "EasyOCR-header";
    public async ValueTask<IReadOnlyList<ShadowFieldCandidate>> AnalyzeAsync(ShadowFrameInput frame, CancellationToken cancellationToken = default)
    {
        var result = await _headers.AnalyzeAsync(ToPng(frame), _screen, cancellationToken).ConfigureAwait(false);
        return new ShadowFieldCandidate[]
        {
            Candidate("Species", result.Species, result.SpeciesConfidence, FieldReadingStatus.Unknown, result.Species is null ? "EASYOCR_SPECIES_UNKNOWN" : "EASYOCR_SPECIES_CANDIDATE"),
            Candidate("CP", result.Cp?.ToString(), result.CpConfidence, FieldReadingStatus.Unknown, result.Cp is null ? "EASYOCR_CP_UNKNOWN" : "EASYOCR_CP_CANDIDATE")
        };
    }
    private static ShadowFieldCandidate Candidate(string field, string? value, double confidence, FieldReadingStatus status, string reason) => new("EasyOCR-header", field, status, value, confidence, reason, 0, "pending");
    private static byte[] ToPng(ShadowFrameInput frame)
    {
        var pixels = frame.Pixels.Span;
        var rgba = new byte[frame.Metadata.Descriptor.RequiredByteLength];
        for (var i = 0; i < rgba.Length; i += 4) { rgba[i] = pixels[i + 2]; rgba[i + 1] = pixels[i + 1]; rgba[i + 2] = pixels[i]; rgba[i + 3] = 255; }
        return PngEncoder.Encode(new PixelImage(frame.Metadata.Descriptor.Width, frame.Metadata.Descriptor.Height, rgba));
    }
}

public sealed class RealIvGeometryAnalyzer : IShadowSemanticAnalyzer
{
    private readonly AppraisalAnalyzer _analyzer;
    private readonly AppraisalVisualProfile _profile;
    public RealIvGeometryAnalyzer(AppraisalAnalyzer analyzer, AppraisalVisualProfile profile) { _analyzer = analyzer; _profile = profile; }
    public string Name => "IV-bar-geometry";
    public ValueTask<IReadOnlyList<ShadowFieldCandidate>> AnalyzeAsync(ShadowFrameInput frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var image = PngDecoder.Decode(ToPng(frame));
        var result = _analyzer.Analyze(image, _profile, allowComplete: false);
        var values = new[] { ("AttackIV", result.AttackIv), ("DefenseIV", result.DefenseIv), ("HPIV", result.HpIv) };
        return ValueTask.FromResult<IReadOnlyList<ShadowFieldCandidate>>(values.Select(item => new ShadowFieldCandidate(Name, item.Item1, FieldReadingStatus.Unknown, item.Item2?.ToString(), result.Confidence, item.Item2 is null ? "IV_GEOMETRY_UNKNOWN" : "IV_GEOMETRY_CANDIDATE", 0, "pending")).ToArray());
    }
    private static byte[] ToPng(ShadowFrameInput frame)
    {
        var pixels = frame.Pixels.Span; var rgba = new byte[frame.Metadata.Descriptor.RequiredByteLength];
        for (var i = 0; i < rgba.Length; i += 4) { rgba[i] = pixels[i + 2]; rgba[i + 1] = pixels[i + 1]; rgba[i + 2] = pixels[i]; rgba[i + 3] = 255; }
        return PngEncoder.Encode(new PixelImage(frame.Metadata.Descriptor.Width, frame.Metadata.Descriptor.Height, rgba));
    }
}

public sealed class ScreenshotReferenceProvider : IShadowReferenceProvider
{
    private readonly IReadOnlyDictionary<string, string?> _truth;
    public ScreenshotReferenceProvider(IReadOnlyDictionary<string, string?> truth) => _truth = truth;
    public string Name => "verified-screenshot-reference";
    public ValueTask<IReadOnlyList<ShadowReferenceReading>> GetReferenceAsync(ShadowFrameInput frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hash = Convert.ToHexString(SHA256.HashData(frame.Pixels.ToArray())).ToLowerInvariant();
        var values = _truth.Select(pair => new ShadowReferenceReading(Name, pair.Key, pair.Value is null ? FieldReadingStatus.Unknown : FieldReadingStatus.Known, pair.Value, pair.Value is null ? 0 : 1, pair.Value is null ? "TRUTH_UNVERIFIABLE" : "MANUAL_SCREENSHOT_TRUTH")).ToArray();
        _ = hash;
        return ValueTask.FromResult<IReadOnlyList<ShadowReferenceReading>>(values);
    }
}
