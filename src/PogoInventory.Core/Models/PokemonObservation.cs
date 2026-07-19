namespace PogoInventory.Core.Models;

public sealed record PokemonObservation
{
    public required string ExternalKey { get; init; }
    public required int SequenceNumber { get; init; }
    public required string Species { get; init; }
    public string? Form { get; init; }
    public string? Costume { get; init; }
    public string? Nickname { get; init; }
    public int? Cp { get; init; }
    public int? Hp { get; init; }
    public decimal? Level { get; init; }
    public int? AttackIv { get; init; }
    public int? DefenseIv { get; init; }
    public int? HpIv { get; init; }
    public DateOnly? CatchDate { get; init; }
    public string? CatchLocation { get; init; }

    public bool? IsShiny { get; init; }
    public bool? IsLegendary { get; init; }
    public bool? IsMythical { get; init; }
    public bool? IsUltraBeast { get; init; }
    public bool? IsBackground { get; init; }
    public bool? IsFavorite { get; init; }
    public bool? IsShadow { get; init; }
    public bool? IsPurified { get; init; }
    public bool? IsLucky { get; init; }
    public bool? IsCostume { get; init; }
    public bool? IsDynamax { get; init; }
    public bool? IsGigantamax { get; init; }
    public bool? HasSpecialMove { get; init; }
    public bool? IsXxl { get; init; }
    public bool? IsXxs { get; init; }

    public IdentityConfidence IdentityConfidence { get; init; } = IdentityConfidence.Unknown;
    public PokemonVariantIdentity? VariantIdentity { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();

    public int? TotalIv => AttackIv is null || DefenseIv is null || HpIv is null
        ? null
        : AttackIv.Value + DefenseIv.Value + HpIv.Value;

    public bool IsPerfect => TotalIv == 45;

    public string GroupKey => VariantIdentity?.VariantKey ??
        $"unknown-variant|{Normalize(ExternalKey)}";

    public bool HasKnownCriticalValues =>
        !string.IsNullOrWhiteSpace(Species) &&
        Cp is not null &&
        AttackIv is not null &&
        DefenseIv is not null &&
        HpIv is not null &&
        CatchDate is not null &&
        IsShiny is not null &&
        IsLegendary is not null &&
        IsMythical is not null &&
        IsUltraBeast is not null &&
        IsBackground is not null &&
        IsFavorite is not null &&
        IsShadow is not null &&
        IsPurified is not null &&
        IsLucky is not null &&
        IsCostume is not null &&
        IsDynamax is not null &&
        IsGigantamax is not null &&
        HasSpecialMove is not null &&
        IsXxl is not null &&
        IsXxs is not null &&
        VariantIdentity?.VariantKey is not null;

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();
}
