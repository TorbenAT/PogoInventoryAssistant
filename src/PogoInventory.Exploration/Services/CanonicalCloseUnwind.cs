using PogoInventory.Exploration.Models;
using PogoInventory.Automation.Models;

namespace PogoInventory.Exploration.Services;

public sealed record CleanupStateObservation
{
    public required PokemonGoGameState State { get; init; }
    public required RecoveryFrameKind? AppraisalKind { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}

public sealed record CanonicalCloseOperationResult
{
    public required bool Succeeded { get; init; }
    public required PokemonGoGameState StateBefore { get; init; }
    public required PokemonGoGameState StateAfter { get; init; }
    public required int InputCount { get; init; }
    public required bool UnsafeSurfacePresent { get; init; }
    public required bool CanonicalCloseVerified { get; init; }
    public required string Result { get; init; }
    public string? Blocker { get; init; }
}

public sealed record CanonicalUnwindResult
{
    public required bool Succeeded { get; init; }
    public required PokemonGoGameState InitialState { get; init; }
    public required PokemonGoGameState FinalState { get; init; }
    public required IReadOnlyList<string> Path { get; init; }
    public required IReadOnlyList<string> Actions { get; init; }
    public required int InputCount { get; init; }
    public required string Result { get; init; }
    public string? Blocker { get; init; }
}

/// <summary>
/// Owns the bounded generic startup unwind. Appraisal uses its existing named
/// visually guarded exit operation because that screen exposes the appraisal
/// close control rather than the lower-centre canonical X; all remaining
/// layers use the canonical-close operation. No Android Back fallback exists.
/// </summary>
public sealed class CanonicalCloseUnwindService
{
    public const int MaximumCloseInputs = 5;

    public async Task<CanonicalUnwindResult> UnwindToGameplayMapAsync(
        AndroidVerifiedInventoryNamedOperations operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var path = new List<string>();
        var actions = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var inputCount = 0;
        var initial = PokemonGoGameState.Unknown;

        for (var step = 0; step <= MaximumCloseInputs; step++)
        {
            var state = await operations.CaptureStableCleanupStateAsync(
                $"canonical-unwind-state-{step + 1}", cancellationToken);
            if (step == 0)
                initial = state.State;
            path.Add(state.State.ToString());
            if (state.State == PokemonGoGameState.GameplayMap)
            {
                return new CanonicalUnwindResult
                {
                    Succeeded = true,
                    InitialState = initial,
                    FinalState = PokemonGoGameState.GameplayMap,
                    Path = path,
                    Actions = actions,
                    InputCount = inputCount,
                    Result = "READY"
                };
            }

            var key = $"{state.State}|{state.ScreenshotSha256}";
            if (!seen.Add(key))
            {
                return Blocked(initial, path, actions, inputCount,
                    state.State, "repeated screenshot/state loop");
            }
            if (inputCount >= MaximumCloseInputs)
            {
                return Blocked(initial, path, actions, inputCount,
                    state.State, "canonical close input budget exhausted");
            }

            if (state.State == PokemonGoGameState.Appraisal)
            {
                var details = await operations.ExitAppraisalAsync(cancellationToken);
                inputCount += operations.LastCleanupRecoveryInputCount;
                actions.Add("exit-appraisal");
                if (details != VerifiedSequenceState.PokemonDetails)
                {
                    return Blocked(initial, path, actions, inputCount,
                        state.State, "guarded appraisal exit did not establish PokemonDetails");
                }

                if (inputCount > MaximumCloseInputs)
                {
                    return Blocked(initial, path, actions, inputCount,
                        PokemonGoGameState.PokemonDetails,
                        "canonical close input budget exhausted");
                }

                continue;
            }

            var close = await operations.CloseCanonicalScreenAsync(cancellationToken);
            inputCount += close.InputCount;
            actions.Add("close-canonical-screen");
            if (!close.Succeeded)
            {
                return Blocked(initial, path, actions, inputCount,
                    close.StateAfter, close.Blocker ?? close.Result);
            }
            if (inputCount > MaximumCloseInputs)
            {
                return Blocked(initial, path, actions, inputCount,
                    close.StateAfter, "canonical close input budget exhausted");
            }
        }

        return Blocked(initial, path, actions, inputCount,
            PokemonGoGameState.Unknown, "canonical close input budget exhausted");
    }

    private static CanonicalUnwindResult Blocked(
        PokemonGoGameState initial,
        IReadOnlyList<string> path,
        IReadOnlyList<string> actions,
        int inputCount,
        PokemonGoGameState finalState,
        string blocker) => new()
        {
            Succeeded = false,
            InitialState = initial,
            FinalState = finalState,
            Path = path,
            Actions = actions,
            InputCount = inputCount,
            Result = "BLOCKED",
            Blocker = blocker
        };
}
