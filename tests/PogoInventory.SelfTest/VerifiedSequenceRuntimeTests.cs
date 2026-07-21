using PogoInventory.Exploration.Services;

internal static class VerifiedSequenceRuntimeTests
{
    public static Task RunAsync()
    {
        var root = FindRepositoryRoot();
        var host = File.ReadAllText(Path.Combine(root, "src", "PogoInventory.Exploration",
            "Services", "AndroidVerifiedInventoryNamedOperations.cs"));
        var sequence = File.ReadAllText(Path.Combine(root, "src", "PogoInventory.Automation",
            "Services", "VerifiedInventoryTaskSequence.cs"));
        Assert(host.Contains("CaptureRecoveryFramesAsync", StringComparison.Ordinal),
            "host uses guarded recovery frame consensus");
        Assert(host.Contains("DecideAppraisalContinuation", StringComparison.Ordinal),
            "appraisal intro continuation delegates to GuardedInventoryRecovery");
        Assert(host.Contains("ExecuteRecoveryActionAsync", StringComparison.Ordinal),
            "recovery actions are authorized before input");
        var exitStart = host.IndexOf("ExitAppraisalAsync", StringComparison.Ordinal);
        var exitEnd = host.IndexOf("ReadTagObservationAsync", exitStart, StringComparison.Ordinal);
        var exit = host[exitStart..exitEnd];
        Assert(!exit.Contains("PressBackAsync", StringComparison.Ordinal),
            "appraisal exit never sends Back");
        var returnStart = host.IndexOf("ReturnToInventoryAsync", StringComparison.Ordinal);
        var returnEnd = host.IndexOf("ApplyIndexTagAsync", returnStart, StringComparison.Ordinal);
        var recovery = host[returnStart..returnEnd];
        Assert(recovery.Contains("_recovery.Begin", StringComparison.Ordinal) &&
            recovery.Contains("AuthorizeNextAction", StringComparison.Ordinal) &&
            recovery.Contains("ObservePostAction", StringComparison.Ordinal),
            "return to Inventory delegates all actions to guarded recovery");
        Assert(!recovery.Contains("for (var action = 0; action < 3", StringComparison.Ordinal),
            "return to Inventory has no blind Back loop");
        var advanceStart = host.IndexOf("AdvanceToNextPokemonAsync", StringComparison.Ordinal);
        var advanceEnd = host.IndexOf("ReturnToInventoryAsync", advanceStart, StringComparison.Ordinal);
        var advance = host[advanceStart..advanceEnd];
        Assert(advance.Contains("ObserveSwipeTransitionAsync", StringComparison.Ordinal) &&
            advance.Contains("CaptureIndependentDetailsFramesAsync", StringComparison.Ordinal),
            "cursor requires observed transition and three independent post frames");
        Assert(advance.Contains("ClassifySwipeProgression", StringComparison.Ordinal),
            "cursor classifies changed identity after post-frame capture");
        Assert(host.Contains("allowVisualDetailsFallback", StringComparison.Ordinal) &&
            host.Contains("LocateDetailsPageTopology", StringComparison.Ordinal),
            "cursor can validate visually stable Details when transient state is missed");
        var recoverySource = File.ReadAllText(Path.Combine(root, "src", "PogoInventory.Exploration",
            "Services", "GuardedInventoryRecovery.cs"));
        Assert(recoverySource.Contains("LocateDetailsPageTopology", StringComparison.Ordinal) &&
            recoverySource.Contains("State = PokemonGoGameState.PokemonDetails", StringComparison.Ordinal),
            "guarded recovery uses bounded visual Details topology fallback");
        Assert(advance.Split("_transport.SwipeAsync", StringSplitOptions.None).Length - 1 == 1,
            "cursor authorizes exactly one swipe");
        Assert(AndroidVerifiedInventoryNamedOperations.ClassifySwipeProgression(
                false, "cp88", "cp129") == CursorProgressionOutcome.SuccessChangedIdentity,
            "CP88 to CP129 without captured animation is changed identity success");
        Assert(AndroidVerifiedInventoryNamedOperations.ClassifySwipeProgression(
                false, "pikachu", "eevee") == CursorProgressionOutcome.SuccessChangedIdentity,
            "different species without captured animation is changed identity success");
        Assert(AndroidVerifiedInventoryNamedOperations.ClassifySwipeProgression(
                true, "same", "same") == CursorProgressionOutcome.Success,
            "same fingerprint with observed transition is success");
        Assert(AndroidVerifiedInventoryNamedOperations.ClassifySwipeProgression(
                false, "same", "same") == CursorProgressionOutcome.NoEffectOrEndOfFilter,
            "same fingerprint without observed transition is no effect or end of filter");
        Assert(!advance.Contains("before.Screenshot },\n            new PokemonIdentityFrame { ScreenshotPng = after.Screenshot },\n            new PokemonIdentityFrame { ScreenshotPng = after.Screenshot", StringComparison.Ordinal),
            "cursor does not synthesize before/after/after consensus");
        Assert(sequence.Contains("ControlledStopped", StringComparison.Ordinal) &&
            sequence.Contains("TerminalUnknown", StringComparison.Ordinal) &&
            sequence.Contains("Completed", StringComparison.Ordinal),
            "checkpoint states distinguish controlled, terminal and completed outcomes");
        return Task.CompletedTask;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Repository root was not found.");
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
