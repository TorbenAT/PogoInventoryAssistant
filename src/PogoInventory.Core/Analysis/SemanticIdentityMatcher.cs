using PogoInventory.Core.Models;

namespace PogoInventory.Core.Analysis;

/// <summary>
/// The result of comparing one candidate semantic identity key against a new
/// observation's key.
/// </summary>
public enum SemanticMatchOutcome
{
    /// <summary>No prior record shares a Comparable key with the new observation.</summary>
    NoMatch,

    /// <summary>Exactly one prior record shares a Comparable key. Safe to treat as the same individual.</summary>
    Matched,

    /// <summary>Two or more prior records share the same Comparable key. Never auto-merge.</summary>
    AmbiguousCollision
}

/// <summary>
/// A minimal, storage-independent view of a persisted record's semantic identity,
/// used purely for in-memory cross-run matching.
/// </summary>
public sealed record SemanticIdentityRecord
{
    public required string LocalPokemonId { get; init; }
    public required string FullKey { get; init; }
    public required SemanticKeyCompleteness Completeness { get; init; }
}

/// <summary>
/// The outcome of matching a single new observation's semantic identity key
/// against a set of prior records.
/// </summary>
public sealed record SemanticIdentityMatchResult
{
    public required SemanticMatchOutcome Outcome { get; init; }

    /// <summary>Populated only when <see cref="Outcome"/> is <see cref="SemanticMatchOutcome.Matched"/>.</summary>
    public string? MatchedLocalPokemonId { get; init; }

    /// <summary>
    /// All candidate prior LocalPokemonIds that shared the queried key. Contains exactly one
    /// entry for Matched, two or more for AmbiguousCollision, and is empty for NoMatch.
    /// </summary>
    public IReadOnlyList<string> CandidateLocalPokemonIds { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Pure, storage-independent cross-run identity matching. Given a set of prior
/// records and a new observation's semantic key, decides whether the new
/// observation is very likely the same individual as a prior record.
///
/// A key is only ever used to match across runs when both sides report
/// <see cref="SemanticKeyCompleteness.Comparable"/>. Multiple prior records that
/// collide on the same Comparable key are reported as an ambiguous collision and
/// are never automatically merged.
/// </summary>
public static class SemanticIdentityMatcher
{
    public static SemanticIdentityMatchResult Match(
        IReadOnlyCollection<SemanticIdentityRecord> priorRecords,
        SemanticIdentityRecord newObservation)
    {
        ArgumentNullException.ThrowIfNull(priorRecords);
        ArgumentNullException.ThrowIfNull(newObservation);

        if (newObservation.Completeness != SemanticKeyCompleteness.Comparable)
        {
            return new SemanticIdentityMatchResult
            {
                Outcome = SemanticMatchOutcome.NoMatch,
                CandidateLocalPokemonIds = Array.Empty<string>()
            };
        }

        var candidates = priorRecords
            .Where(record =>
                record.Completeness == SemanticKeyCompleteness.Comparable &&
                string.Equals(record.FullKey, newObservation.FullKey, StringComparison.Ordinal))
            .Select(record => record.LocalPokemonId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return candidates.Count switch
        {
            0 => new SemanticIdentityMatchResult
            {
                Outcome = SemanticMatchOutcome.NoMatch,
                CandidateLocalPokemonIds = Array.Empty<string>()
            },
            1 => new SemanticIdentityMatchResult
            {
                Outcome = SemanticMatchOutcome.Matched,
                MatchedLocalPokemonId = candidates[0],
                CandidateLocalPokemonIds = candidates
            },
            _ => new SemanticIdentityMatchResult
            {
                Outcome = SemanticMatchOutcome.AmbiguousCollision,
                CandidateLocalPokemonIds = candidates
            }
        };
    }
}
