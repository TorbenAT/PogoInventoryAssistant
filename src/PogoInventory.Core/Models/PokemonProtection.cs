namespace PogoInventory.Core.Models;

/// <summary>
/// Fail-closed proof state for a cleanup-critical assertion.  In particular,
/// <c>Known(false)</c> is distinct from <c>Unknown</c> and requires evidence.
/// </summary>
public enum ProtectionProofState
{
    Known,
    Unknown,
    Conflicting
}

public enum ProtectionEvidenceSource
{
    Visual,
    ReferenceDerived,
    SearchFilter,
    OfflineReplay,
    Diagnostic
}

public sealed record ProtectionEvidence
{
    public required ProtectionEvidenceSource Source { get; init; }
    public long? FrameId { get; init; }
    public string? EvidenceHash { get; init; }
    public string? RawEvidence { get; init; }
    public string? Detail { get; init; }

    public void Validate()
    {
        if (FrameId is < 0)
        {
            throw new InvalidOperationException(
                "Protection evidence frame ID cannot be negative.");
        }

        if (EvidenceHash is not null &&
            (EvidenceHash.Length != 64 ||
             EvidenceHash.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidOperationException(
                "Protection evidence hash must be a SHA-256 hex value.");
        }
    }
}

public sealed record ProtectionField<T>
{
    public T? Value { get; init; }
    /// <summary>
    /// Separates an asserted <c>false</c> value from the default value of a
    /// value type. Only Known fields may carry an asserted value.
    /// </summary>
    public bool HasValue { get; init; }
    public ProtectionProofState State { get; init; } = ProtectionProofState.Unknown;
    public double Confidence { get; init; }
    public IReadOnlyList<ProtectionEvidence> Evidence { get; init; } =
        Array.Empty<ProtectionEvidence>();
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    public bool IsKnown => State == ProtectionProofState.Known;

    public void Validate()
    {
        if (!double.IsFinite(Confidence) || Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Protection confidence must be finite and between zero and one.");
        }

        foreach (var evidence in Evidence)
        {
            evidence.Validate();
        }

        if (State == ProtectionProofState.Known)
        {
            if (!HasValue || Evidence.Count == 0)
            {
                throw new InvalidOperationException(
                    "Known protection values require a value and evidence.");
            }
            return;
        }

        if (HasValue)
        {
            throw new InvalidOperationException(
                "Unknown or conflicting protection values cannot carry a value.");
        }
    }

    public static ProtectionField<T> Unknown(string reason) => new()
    {
        State = ProtectionProofState.Unknown,
        Confidence = 0,
        Reasons = [reason]
    };
}

/// <summary>
/// Canonical, durable protection proof for one caught PokÃ©mon.  P0 fields are
/// cleanup-critical; a non-Known P0 field blocks any aggressive delete result.
/// </summary>
public sealed record PokemonProtection
{
    public ProtectionField<bool> Favorite { get; init; } =
        ProtectionField<bool>.Unknown("FAVORITE_NOT_OBSERVED");
    public ProtectionField<bool> Shiny { get; init; } =
        ProtectionField<bool>.Unknown("SHINY_NOT_OBSERVED");
    public ProtectionField<bool> Costume { get; init; } =
        ProtectionField<bool>.Unknown("COSTUME_NOT_OBSERVED");
    public ProtectionField<bool> SpecialBackground { get; init; } =
        ProtectionField<bool>.Unknown("SPECIAL_BACKGROUND_NOT_OBSERVED");
    public ProtectionField<bool> Lucky { get; init; } =
        ProtectionField<bool>.Unknown("LUCKY_NOT_OBSERVED");
    public ProtectionField<bool> Shadow { get; init; } =
        ProtectionField<bool>.Unknown("SHADOW_NOT_OBSERVED");
    public ProtectionField<bool> Purified { get; init; } =
        ProtectionField<bool>.Unknown("PURIFIED_NOT_OBSERVED");

    public ProtectionField<bool> Xxl { get; init; } =
        ProtectionField<bool>.Unknown("XXL_NOT_OBSERVED");
    public ProtectionField<bool> Xxs { get; init; } =
        ProtectionField<bool>.Unknown("XXS_NOT_OBSERVED");
    public ProtectionField<bool> Legendary { get; init; } =
        ProtectionField<bool>.Unknown("LEGENDARY_SPECIES_NOT_KNOWN");
    public ProtectionField<bool> Mythical { get; init; } =
        ProtectionField<bool>.Unknown("MYTHICAL_SPECIES_NOT_KNOWN");
    public ProtectionField<bool> UltraBeast { get; init; } =
        ProtectionField<bool>.Unknown("ULTRA_BEAST_SPECIES_NOT_KNOWN");
    public ProtectionField<string> Form { get; init; } =
        ProtectionField<string>.Unknown("FORM_NOT_OBSERVED");

    public bool HasUnknownCriticalProtection => P0Fields.Any(field =>
        field.State != ProtectionProofState.Known);

    public IReadOnlyDictionary<string, ProtectionProofState> States =>
        new Dictionary<string, ProtectionProofState>(StringComparer.Ordinal)
        {
            [nameof(Favorite)] = Favorite.State,
            [nameof(Shiny)] = Shiny.State,
            [nameof(Costume)] = Costume.State,
            [nameof(SpecialBackground)] = SpecialBackground.State,
            [nameof(Lucky)] = Lucky.State,
            [nameof(Shadow)] = Shadow.State,
            [nameof(Purified)] = Purified.State,
            [nameof(Xxl)] = Xxl.State,
            [nameof(Xxs)] = Xxs.State,
            [nameof(Legendary)] = Legendary.State,
            [nameof(Mythical)] = Mythical.State,
            [nameof(UltraBeast)] = UltraBeast.State,
            [nameof(Form)] = Form.State
        };

    public void Validate()
    {
        foreach (var field in P0Fields)
        {
            field.Validate();
        }
        Xxl.Validate();
        Xxs.Validate();
        Legendary.Validate();
        Mythical.Validate();
        UltraBeast.Validate();
        Form.Validate();
    }

    public static PokemonProtection Unknown { get; } = new();

    private IEnumerable<ProtectionField<bool>> P0Fields =>
    [
        Favorite, Shiny, Costume, SpecialBackground, Lucky, Shadow, Purified
    ];
}
