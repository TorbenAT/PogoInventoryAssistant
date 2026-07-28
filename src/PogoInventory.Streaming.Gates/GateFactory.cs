namespace PogoInventory.Streaming.Gates;

public static class GateFactory
{
    public static ITemporalGate Create(GateProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        return profile.Kind switch
        {
            GateProfileKind.StableRegion => new StableRegionGate(profile.Name, profile.Stable, profile.Regions),
            GateProfileKind.TransitionDetected => new TransitionDetectedGate(profile.Name, profile.Transition),
            GateProfileKind.TransitionCompleted => new TransitionCompletedGate(
                profile.Name,
                profile.Transition,
                profile.Regions,
                profile.Stable.RequiredRegions),
            _ => throw new ArgumentOutOfRangeException(nameof(profile.Kind))
        };
    }
}
