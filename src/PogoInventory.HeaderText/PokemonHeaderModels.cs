namespace PogoInventory.HeaderText;

/// <summary>
/// Result of analyzing one frame's header (CP + name/species) region.
/// </summary>
public sealed record PokemonHeaderResult
{
    /// <summary>Canonical species name, or null if unread/unrecognized.</summary>
    public string? Species { get; init; }

    public int? Cp { get; init; }

    /// <summary>
    /// Set when the header text was read but did not validate against known
    /// species (an actual nickname, or an unrecognized OCR read).
    /// </summary>
    public string? Nickname { get; init; }

    public double SpeciesConfidence { get; init; }
    public double CpConfidence { get; init; }
    public required HeaderScreenType SourceScreen { get; init; }
    public IReadOnlyList<RecognizedTextLine> RawLines { get; init; } = Array.Empty<RecognizedTextLine>();
    public IReadOnlyList<string> FailureReasons { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Multi-frame consensus over a set of per-frame header results. Mirrors the
/// style of <c>PokemonDetailsIdentityAnalyzer.Consensus</c>: species/CP are
/// only accepted when at least two of the supplied frames agree.
/// </summary>
public sealed record PokemonHeaderConsensusResult
{
    public string? Species { get; init; }
    public int? Cp { get; init; }
    public required IReadOnlyList<PokemonHeaderResult> Frames { get; init; }
    public IReadOnlyList<string> FailureReasons { get; init; } = Array.Empty<string>();
}
