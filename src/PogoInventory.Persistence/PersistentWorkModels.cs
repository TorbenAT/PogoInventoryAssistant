namespace PogoInventory.Persistence;

/// <summary>
/// Absolute, restartable work partition. Relative in-game age is deliberately
/// not persisted because its meaning changes over time.
/// </summary>
public sealed record PersistentWorkBucket
{
    public required string LogicalBucketId { get; init; }
    public required DateOnly AbsoluteDateStart { get; init; }
    public required DateOnly AbsoluteDateEnd { get; init; }
    public int? PokedexStart { get; init; }
    public int? PokedexEnd { get; init; }
    public required string DerivedPhoneQuery { get; init; }
    public PersistentWorkBucketStatus Status { get; init; } = PersistentWorkBucketStatus.Planned;
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public int ItemsObserved { get; init; }
    public int ItemsIndexed { get; init; }
    public int ItemsReview { get; init; }
    public int ItemsDeleteCandidate { get; init; }
    public int Failures { get; init; }
    public int Retries { get; init; }
    public string? LastSuccessfulItem { get; init; }
    public string? CompletionEvidenceJson { get; init; }
}

public enum PersistentWorkBucketStatus
{
    Planned,
    Active,
    ReconciliationRequired,
    Complete,
    Blocked
}

/// <summary>
/// DB-backed processing state for one exact, already-persisted observation.
/// The phone tag is a checkpoint, but state cannot advance to TagVerified
/// until its separately stored verification evidence exists.
/// </summary>
public sealed record PersistentWorkItem
{
    public required string LogicalBucketId { get; init; }
    public required string LocalPokemonId { get; init; }
    public required PersistentWorkItemState State { get; init; }
    public required string Disposition { get; init; }
    public string? ExactBindingEvidence { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public string? LastError { get; init; }
}

public enum PersistentWorkItemState
{
    ObservedPersisted,
    DecisionReady,
    TagPending,
    TagVerified,
    ReconciliationRequired,
    Blocked
}

public sealed record PersistentWorkAttempt
{
    public required string LogicalBucketId { get; init; }
    public required string LocalPokemonId { get; init; }
    public required string AttemptKind { get; init; }
    public required string Result { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public string? DetailJson { get; init; }
}

/// <summary>
/// Immutable, query-level evidence. A bounded traversal is explicitly not a
/// result-count or filter-end proof; those values remain null/false until the
/// corresponding phone evidence exists.
/// </summary>
public sealed record PersistentSearchOracleEvidence
{
    public required string Query { get; init; }
    public required string Outcome { get; init; }
    public required DateTimeOffset ObservedAtUtc { get; init; }
    public string? LogicalBucketId { get; init; }
    public string? RunId { get; init; }
    public int? ExpectedResultCount { get; init; }
    public int? ObservedResultCount { get; init; }
    public bool EmptyVerified { get; init; }
    public string? EvidencePath { get; init; }
    public string? DetailJson { get; init; }
}
