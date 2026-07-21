using System.Globalization;
using System.Text.RegularExpressions;

namespace PogoInventory.HeaderText;

/// <summary>
/// Extracts species, CP and nickname from OCR'd text in the CP and
/// name/species header regions of the Pokemon Details or Appraisal bars
/// screens.
/// </summary>
public sealed class PokemonHeaderAnalyzer
{
    private const int MinimumCp = 10;
    private const int MaximumCp = 6000;

    private static readonly HashSet<string> UiLabelBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "CP",
        "Appraise",
        "Attack",
        "Defense",
        "HP",
        "Cancel",
        "Done",
        "Power Up",
        "Evolve",
        "Transfer"
    };

    private static readonly Regex CpPattern = new(
        @"(?:CP|cp)?\s*([0-9]{1,5})",
        RegexOptions.Compiled);

    private readonly ITextRecognizer _recognizer;
    private readonly ISpeciesReference _speciesReference;
    private readonly HeaderAnalysisProfile _profile;

    public PokemonHeaderAnalyzer(
        ITextRecognizer recognizer,
        ISpeciesReference speciesReference,
        HeaderAnalysisProfile? profile = null)
    {
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
        _speciesReference = speciesReference ?? throw new ArgumentNullException(nameof(speciesReference));
        _profile = profile ?? new HeaderAnalysisProfile();
        _profile.Validate();
    }

    public async Task<PokemonHeaderResult> AnalyzeAsync(
        byte[] framePng,
        HeaderScreenType screen,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(framePng);

        var failures = new List<string>();

        var cpLines = await _recognizer.RecognizeAsync(
            framePng, _profile.CpRegionFor(screen), cancellationToken, HeaderRegionKind.Cp).ConfigureAwait(false);
        var nameLines = await _recognizer.RecognizeAsync(
            framePng, _profile.NameRegionFor(screen), cancellationToken, HeaderRegionKind.Name).ConfigureAwait(false);

        var (cp, cpConfidence) = ParseCp(cpLines);
        if (cp is null)
        {
            failures.Add("CP_NOT_READ");
        }

        var (species, nickname, speciesConfidence) = ParseHeaderText(nameLines, failures);

        return new PokemonHeaderResult
        {
            Species = species,
            Cp = cp,
            Nickname = nickname,
            SpeciesConfidence = speciesConfidence,
            CpConfidence = cpConfidence,
            SourceScreen = screen,
            RawLines = cpLines.Concat(nameLines).ToArray(),
            FailureReasons = failures
        };
    }

    /// <summary>
    /// Parses CP from OCR text lines. Accepts "CP" prefix variants
    /// (CP/cp/noisy glyph before digits), requires digits only and
    /// range-validates 10..6000.
    /// </summary>
    internal static (int? Cp, double Confidence) ParseCp(IReadOnlyList<RecognizedTextLine> lines)
    {
        foreach (var line in lines)
        {
            var text = line.Text ?? string.Empty;
            foreach (Match match in CpPattern.Matches(text))
            {
                if (!match.Groups[1].Success) continue;
                if (!int.TryParse(
                        match.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    continue;
                }
                if (value < MinimumCp || value > MaximumCp) continue;
                return (value, 1.0);
            }
        }
        return (null, 0.0);
    }

    private (string? Species, string? Nickname, double Confidence) ParseHeaderText(
        IReadOnlyList<RecognizedTextLine> lines,
        List<string> failures)
    {
        var candidate = lines
            .Select(line => (line.Text ?? string.Empty).Trim())
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (string.IsNullOrWhiteSpace(candidate))
        {
            failures.Add("HEADER_TEXT_EMPTY");
            return (null, null, 0.0);
        }

        if (UiLabelBlacklist.Contains(candidate))
        {
            failures.Add("HEADER_TEXT_IS_UI_LABEL");
            return (null, candidate, 0.0);
        }

        var normalized = _speciesReference.NormalizeSpecies(candidate);
        if (normalized is null)
        {
            failures.Add("HEADER_TEXT_NOT_SPECIES");
            return (null, candidate, 0.0);
        }

        var isExactFold = string.Equals(
            StaticSpeciesReference.Fold(candidate),
            StaticSpeciesReference.Fold(normalized),
            StringComparison.Ordinal);

        return (normalized, null, isExactFold ? 1.0 : 0.75);
    }

    /// <summary>
    /// Combines >= 2 per-frame results. Species is accepted when at least two
    /// frames agree on the normalized species; CP is accepted when at least
    /// two frames agree on the same integer.
    /// </summary>
    public static PokemonHeaderConsensusResult Consensus(IReadOnlyList<PokemonHeaderResult> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count < 2)
        {
            throw new ArgumentException("At least two frames are required for consensus.", nameof(frames));
        }

        var failures = new List<string>();

        var species = frames
            .Where(frame => frame.Species is not null)
            .GroupBy(frame => frame.Species, StringComparer.Ordinal)
            .Where(group => group.Count() >= 2)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
        if (species is null)
        {
            failures.Add("SPECIES_CONSENSUS_NOT_REACHED");
        }

        int? cp = frames
            .Where(frame => frame.Cp is not null)
            .GroupBy(frame => frame.Cp!.Value)
            .Where(group => group.Count() >= 2)
            .OrderByDescending(group => group.Count())
            .Select(group => (int?)group.Key)
            .FirstOrDefault();
        if (cp is null)
        {
            failures.Add("CP_CONSENSUS_NOT_REACHED");
        }

        return new PokemonHeaderConsensusResult
        {
            Species = species,
            Cp = cp,
            Frames = frames,
            FailureReasons = failures
        };
    }
}
