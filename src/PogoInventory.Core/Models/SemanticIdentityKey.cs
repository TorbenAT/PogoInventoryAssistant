using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PogoInventory.Core.Models;

/// <summary>
/// How many identity-bearing fields of a <see cref="SemanticIdentityKey"/> are known.
/// Only <see cref="Comparable"/> keys may be used to match records across scan runs.
/// </summary>
public enum SemanticKeyCompleteness
{
    /// <summary>Species is unknown. This key must never be used to match across runs.</summary>
    Insufficient,

    /// <summary>Species is known but CP and/or one or more IVs are unknown.</summary>
    Partial,

    /// <summary>Species, CP and all three IVs are known.</summary>
    Comparable
}

/// <summary>
/// A normalized, run-independent identity key derived from the semantic (human/OCR
/// reviewed) fields of a <see cref="PokemonObservation"/>. Unlike
/// <c>PokemonIdentityInstance.InstanceId</c>, which is scoped to a single scan run,
/// this key is intended to allow the same individual Pokémon to be recognised across
/// separate scan runs once its semantic fields are known.
/// </summary>
public sealed record SemanticIdentityKey
{
    private const string UnknownToken = "unknown";

    /// <summary>The normalized, human-readable pipe-joined field string.</summary>
    public required string ReadableKey { get; init; }

    /// <summary>SHA-256 hex digest of <see cref="ReadableKey"/>, used for compact storage and comparison.</summary>
    public required string FullKey { get; init; }

    /// <summary>How many identity-bearing fields are known.</summary>
    public required SemanticKeyCompleteness Completeness { get; init; }

    public static SemanticIdentityKey FromObservation(PokemonObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        var speciesKnown = IsKnown(observation.Species);
        var species = NormalizeString(observation.Species);
        var form = NormalizeString(observation.Form);
        var costume = NormalizeString(observation.Costume);
        var shiny = NormalizeBool(observation.IsShiny, "shiny", "not-shiny");
        var shadowState = ShadowPurifiedState(observation.IsShadow, observation.IsPurified);
        var lucky = NormalizeBool(observation.IsLucky, "lucky", "not-lucky");
        var background = NormalizeBool(observation.IsBackground, "background", "not-background");
        var attackIv = NormalizeInt(observation.AttackIv);
        var defenseIv = NormalizeInt(observation.DefenseIv);
        var hpIv = NormalizeInt(observation.HpIv);
        var cp = NormalizeInt(observation.Cp);
        var nickname = NormalizeString(observation.Nickname);
        var catchDate = observation.CatchDate is { } date
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : UnknownToken;

        var readable = string.Join(
            '|',
            species,
            form,
            costume,
            shiny,
            shadowState,
            lucky,
            background,
            attackIv,
            defenseIv,
            hpIv,
            cp,
            nickname,
            catchDate);

        var completeness = ComputeCompleteness(
            speciesKnown,
            observation.Cp,
            observation.AttackIv,
            observation.DefenseIv,
            observation.HpIv);

        return new SemanticIdentityKey
        {
            ReadableKey = readable,
            FullKey = ComputeSha256Hex(readable),
            Completeness = completeness
        };
    }

    private static SemanticKeyCompleteness ComputeCompleteness(
        bool speciesKnown,
        int? cp,
        int? attackIv,
        int? defenseIv,
        int? hpIv)
    {
        if (!speciesKnown)
        {
            return SemanticKeyCompleteness.Insufficient;
        }

        return cp is not null && attackIv is not null && defenseIv is not null && hpIv is not null
            ? SemanticKeyCompleteness.Comparable
            : SemanticKeyCompleteness.Partial;
    }

    private static bool IsKnown(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Trim().Equals(UnknownToken, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeString(string? value) =>
        IsKnown(value) ? value!.Trim().ToLowerInvariant() : UnknownToken;

    private static string NormalizeInt(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? UnknownToken;

    private static string NormalizeBool(bool? value, string trueToken, string falseToken) =>
        value is null ? UnknownToken : value.Value ? trueToken : falseToken;

    private static string ShadowPurifiedState(bool? isShadow, bool? isPurified)
    {
        if (isShadow is true) return "shadow";
        if (isPurified is true) return "purified";
        if (isShadow is false && isPurified is false) return "normal";
        return UnknownToken;
    }

    private static string ComputeSha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
