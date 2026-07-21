using PogoInventory.Exploration.Models;

namespace PogoInventory.Exploration.Services;

public sealed record CleanupStateObservation
{
    public required PokemonGoGameState State { get; init; }
    public required RecoveryFrameKind? AppraisalKind { get; init; }
    public required string ScreenshotSha256 { get; init; }
    public required IReadOnlyList<string> Evidence { get; init; }
}

public sealed record CleanupStateRecoveryResult
{
    public required PokemonGoGameState InitialState { get; init; }
    public required IReadOnlyList<string> RecoveryPath { get; init; }
    public required IReadOnlyList<string> RecoveryActions { get; init; }
    public required int RecoveryInputCount { get; init; }
    public required PokemonGoGameState ReadyState { get; init; }
    public required string Result { get; init; }
    public string? Blocker { get; init; }
    public bool IsReady => ReadyState == PokemonGoGameState.GameplayMap &&
        string.Equals(Result, "READY", StringComparison.Ordinal);
}

/// <summary>
/// Bounded graph for cleanup startup. The Android adapter supplies only named,
/// state-validated operations; this service owns ordering, loop detection and
/// the six-input budget.
/// </summary>
public sealed class KnownGameStateNormalizer
{
    public const int MaximumRecoveryInputs = 6;

    public static string? NextAction(
        PokemonGoGameState state,
        RecoveryFrameKind? appraisalKind) => (state, appraisalKind) switch
    {
        (PokemonGoGameState.GameplayMap, _) => null,
        (PokemonGoGameState.MainMenu, _) => "close-main-menu",
        (PokemonGoGameState.Inventory, _) => "close-inventory",
        (PokemonGoGameState.InventorySearchOpen, _) => "close-inventory",
        (PokemonGoGameState.InventoryFiltered, _) => "close-inventory",
        (PokemonGoGameState.PokemonDetails, _) => "return-to-inventory",
        (PokemonGoGameState.PokemonMenu, _) => "return-to-inventory",
        (PokemonGoGameState.Appraisal, RecoveryFrameKind.AppraisalIntro) =>
            "device-continue-appraisal-intro",
        (PokemonGoGameState.Appraisal, RecoveryFrameKind.AppraisalBars) =>
            "exit-appraisal",
        (PokemonGoGameState.KnownInformationalPopup, _) =>
            "dismiss-known-informational-popup",
        _ => "STOP_UNKNOWN"
    };

    public async Task<CleanupStateRecoveryResult> EnsureCleanupReadyAsync(
        AndroidVerifiedInventoryNamedOperations operations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var path = new List<string>();
        var actions = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var inputCount = 0;
        CleanupStateObservation? initial = null;

        for (var step = 0; step <= MaximumRecoveryInputs; step++)
        {
            var observation = await operations.CaptureStableCleanupStateAsync(
                $"start-state-{step + 1}", cancellationToken);
            initial ??= observation;
            path.Add(observation.State.ToString());
            var action = NextAction(observation.State, observation.AppraisalKind);
            if (action is null)
            {
                return await FinishAsync(initial, path, actions, inputCount,
                    PokemonGoGameState.GameplayMap, "READY", null, cancellationToken);
            }

            var key = $"{observation.State}|{observation.AppraisalKind}|{action}";
            if (!seen.Add(key))
            {
                return await FinishAsync(initial, path, actions, inputCount,
                    observation.State, "BLOCKED", "repeated state/action pair",
                    cancellationToken);
            }

            if (action == "STOP_UNKNOWN")
            {
                return await FinishAsync(initial, path, actions, inputCount,
                    observation.State, "BLOCKED", "unknown or conflicting state",
                    cancellationToken);
            }

            var outcome = action switch
            {
                "close-main-menu" => await operations.CloseMainMenuForCleanupAsync(cancellationToken),
                "close-inventory" => await operations.CloseInventoryForCleanupAsync(cancellationToken),
                "return-to-inventory" => await operations.ReturnToInventoryForCleanupAsync(cancellationToken),
                "device-continue-appraisal-intro" =>
                    await operations.ContinueAppraisalIntroForCleanupAsync(cancellationToken),
                "exit-appraisal" => await operations.ExitAppraisalForCleanupAsync(cancellationToken),
                "dismiss-known-informational-popup" =>
                    await operations.DismissKnownInformationalPopupForCleanupAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported recovery action '{action}'.")
            };
            inputCount += outcome.InputCount;
            actions.Add(action);
            if (!outcome.Succeeded)
            {
                return await FinishAsync(initial, path, actions, inputCount,
                    outcome.State, "BLOCKED", outcome.Blocker ?? action,
                    cancellationToken);
            }
            if (inputCount > MaximumRecoveryInputs)
            {
                return await FinishAsync(initial, path, actions, inputCount,
                    outcome.State, "BLOCKED", "recovery action budget exhausted",
                    cancellationToken);
            }
        }

        return await FinishAsync(initial!, path, actions, inputCount,
            PokemonGoGameState.Unknown, "BLOCKED", "recovery action budget exhausted",
            cancellationToken);
    }

    private static async Task<CleanupStateRecoveryResult> FinishAsync(
        CleanupStateObservation? initial,
        IReadOnlyList<string> path,
        IReadOnlyList<string> actions,
        int inputCount,
        PokemonGoGameState readyState,
        string result,
        string? blocker,
        CancellationToken cancellationToken)
    {
        var value = new CleanupStateRecoveryResult
        {
            InitialState = initial?.State ?? PokemonGoGameState.Unknown,
            RecoveryPath = path,
            RecoveryActions = actions,
            RecoveryInputCount = inputCount,
            ReadyState = readyState,
            Result = result,
            Blocker = blocker
        };
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }
}

public sealed record CleanupStateActionResult
{
    public required bool Succeeded { get; init; }
    public required PokemonGoGameState State { get; init; }
    public required int InputCount { get; init; }
    public string? Blocker { get; init; }
}
