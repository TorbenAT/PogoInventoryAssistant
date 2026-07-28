using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Streaming.Scrcpy;

namespace PogoInventory.Streaming.Gates;

public enum GateProfileKind
{
    StableRegion = 0,
    TransitionDetected = 1,
    TransitionCompleted = 2
}

public sealed record StableRegionGateOptions
{
    public IReadOnlyList<string> RequiredRegions { get; init; } = Array.Empty<string>();
    public int MinimumStableFrames { get; init; } = 3;
    public TimeSpan MinimumStableDuration { get; init; } = TimeSpan.FromMilliseconds(150);
    public double MaximumMotionScore { get; init; } = 0.05;
    public double MaximumDifferenceScore { get; init; } = 0.04;
    public double MinimumSimilarityScore { get; init; } = 0.94;
    public double MinimumSharpnessScore { get; init; } = 0.20;
    public TimeSpan MaximumObservationDuration { get; init; } = TimeSpan.FromSeconds(3);
    public long MinimumEvidenceFrameIdDistance { get; init; } = 2;
    public TimeSpan MinimumEvidenceTimeDistance { get; init; } = TimeSpan.FromMilliseconds(80);
    public double MaximumEvidenceVisualSimilarity { get; init; } = 1.0;
}

public sealed record TransitionGateOptions
{
    public IReadOnlyList<string> TransitionRegions { get; init; } = Array.Empty<string>();
    public int MinimumChangedFrames { get; init; } = 2;
    public TimeSpan MinimumChangedDuration { get; init; } = TimeSpan.FromMilliseconds(70);
    public double MinimumMotionScore { get; init; } = 0.08;
    public double MinimumDifferenceScore { get; init; } = 0.07;
    public int MinimumPreStableFrames { get; init; } = 3;
    public int MinimumPostStableFrames { get; init; } = 3;
    public TimeSpan MinimumStableDuration { get; init; } = TimeSpan.FromMilliseconds(150);
    public double MaximumStableMotionScore { get; init; } = 0.05;
    public double MaximumStableDifferenceScore { get; init; } = 0.04;
    public double MinimumStableSimilarityScore { get; init; } = 0.94;
    public double MinimumSharpnessScore { get; init; } = 0.20;
    public double MinimumMeaningfulChange { get; init; } = 0.05;
    public TimeSpan MaximumObservationDuration { get; init; } = TimeSpan.FromSeconds(8);
}

public sealed record FrameDiversityOptions
{
    public long MinimumFrameIdDistance { get; init; } = 2;
    public TimeSpan MinimumTimeDistance { get; init; } = TimeSpan.FromMilliseconds(80);
    public double MaximumVisualSimilarity { get; init; } = 1.0;
}

public sealed record TemporalObserverOptions
{
    public int MaxConcurrentAnalysis { get; init; } = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
    public double StableMotionThreshold { get; init; } = 0.05;
    public double StableDifferenceThreshold { get; init; } = 0.04;
    public double StableSimilarityThreshold { get; init; } = 0.94;
    public double TransitionMotionThreshold { get; init; } = 0.08;
    public double TransitionDifferenceThreshold { get; init; } = 0.07;
    public double MinimumSharpness { get; init; } = 0.20;
    public TimeSpan FreezeIntervalThreshold { get; init; } = TimeSpan.FromSeconds(1);
    public int FrozenSourceTimestampFrames { get; init; } = 4;
    public int SamplingTarget { get; init; } = 12000;
}

public sealed record GateProfile
{
    public required string Name { get; init; }
    public required GateProfileKind Kind { get; init; }
    public required IReadOnlyList<RegionDefinition> Regions { get; init; }
    public StableRegionGateOptions Stable { get; init; } = new();
    public TransitionGateOptions Transition { get; init; } = new();
    public FrameDiversityOptions Diversity { get; init; } = new();
    public TemporalObserverOptions Observer { get; init; } = new();
    public int SessionHistoryCapacity { get; init; } = 240;
    public int MaximumObservedFrameIds { get; init; } = 1024;
    public int MaximumEvidenceFrames { get; init; } = 10;

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        if (Regions is null || Regions.Count == 0)
        {
            throw new InvalidOperationException("At least one region must be configured.");
        }

        if (SessionHistoryCapacity < 8 || SessionHistoryCapacity > 3600)
        {
            throw new ArgumentOutOfRangeException(nameof(SessionHistoryCapacity));
        }

        if (MaximumObservedFrameIds < SessionHistoryCapacity || MaximumObservedFrameIds > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumObservedFrameIds));
        }

        if (MaximumEvidenceFrames < 1 || MaximumEvidenceFrames > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumEvidenceFrames));
        }

        ValidateUnitScore(Stable.MaximumMotionScore, nameof(Stable.MaximumMotionScore));
        ValidateUnitScore(Stable.MaximumDifferenceScore, nameof(Stable.MaximumDifferenceScore));
        ValidateUnitScore(Stable.MinimumSimilarityScore, nameof(Stable.MinimumSimilarityScore));
        ValidateUnitScore(Stable.MinimumSharpnessScore, nameof(Stable.MinimumSharpnessScore));
        ValidateUnitScore(Stable.MaximumEvidenceVisualSimilarity, nameof(Stable.MaximumEvidenceVisualSimilarity));
        ValidatePositive(Stable.MinimumStableFrames, nameof(Stable.MinimumStableFrames));
        ValidatePositive(Stable.MaximumObservationDuration, nameof(Stable.MaximumObservationDuration));
        ValidateNonNegative(Stable.MinimumStableDuration, nameof(Stable.MinimumStableDuration));
        ValidateNonNegative(Stable.MinimumEvidenceTimeDistance, nameof(Stable.MinimumEvidenceTimeDistance));
        if (Stable.MinimumEvidenceFrameIdDistance < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Stable.MinimumEvidenceFrameIdDistance));
        }

        ValidateUnitScore(Transition.MinimumMotionScore, nameof(Transition.MinimumMotionScore));
        ValidateUnitScore(Transition.MinimumDifferenceScore, nameof(Transition.MinimumDifferenceScore));
        ValidateUnitScore(Transition.MaximumStableMotionScore, nameof(Transition.MaximumStableMotionScore));
        ValidateUnitScore(Transition.MaximumStableDifferenceScore, nameof(Transition.MaximumStableDifferenceScore));
        ValidateUnitScore(Transition.MinimumStableSimilarityScore, nameof(Transition.MinimumStableSimilarityScore));
        ValidateUnitScore(Transition.MinimumSharpnessScore, nameof(Transition.MinimumSharpnessScore));
        ValidateUnitScore(Transition.MinimumMeaningfulChange, nameof(Transition.MinimumMeaningfulChange));
        ValidatePositive(Transition.MinimumChangedFrames, nameof(Transition.MinimumChangedFrames));
        ValidatePositive(Transition.MinimumPreStableFrames, nameof(Transition.MinimumPreStableFrames));
        ValidatePositive(Transition.MinimumPostStableFrames, nameof(Transition.MinimumPostStableFrames));
        ValidatePositive(Transition.MaximumObservationDuration, nameof(Transition.MaximumObservationDuration));
        ValidateNonNegative(Transition.MinimumChangedDuration, nameof(Transition.MinimumChangedDuration));
        ValidateNonNegative(Transition.MinimumStableDuration, nameof(Transition.MinimumStableDuration));

        if (Diversity.MinimumFrameIdDistance < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Diversity.MinimumFrameIdDistance));
        }

        ValidateNonNegative(Diversity.MinimumTimeDistance, nameof(Diversity.MinimumTimeDistance));
        ValidateUnitScore(Diversity.MaximumVisualSimilarity, nameof(Diversity.MaximumVisualSimilarity));

        if (Observer.MaxConcurrentAnalysis < 1 || Observer.MaxConcurrentAnalysis > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(Observer.MaxConcurrentAnalysis));
        }

        ValidateUnitScore(Observer.StableMotionThreshold, nameof(Observer.StableMotionThreshold));
        ValidateUnitScore(Observer.StableDifferenceThreshold, nameof(Observer.StableDifferenceThreshold));
        ValidateUnitScore(Observer.StableSimilarityThreshold, nameof(Observer.StableSimilarityThreshold));
        ValidateUnitScore(Observer.TransitionMotionThreshold, nameof(Observer.TransitionMotionThreshold));
        ValidateUnitScore(Observer.TransitionDifferenceThreshold, nameof(Observer.TransitionDifferenceThreshold));
        ValidateUnitScore(Observer.MinimumSharpness, nameof(Observer.MinimumSharpness));
        ValidatePositive(Observer.FreezeIntervalThreshold, nameof(Observer.FreezeIntervalThreshold));
        ValidatePositive(Observer.FrozenSourceTimestampFrames, nameof(Observer.FrozenSourceTimestampFrames));
        ValidatePositive(Observer.SamplingTarget, nameof(Observer.SamplingTarget));

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var region in Regions)
        {
            region.Validate();
            if (!names.Add(region.Name))
            {
                throw new InvalidOperationException($"Duplicate region name '{region.Name}'.");
            }
        }

        foreach (var required in Stable.RequiredRegions)
        {
            var region = Regions.FirstOrDefault(x => string.Equals(x.Name, required, StringComparison.Ordinal));
            if (region is null)
            {
                throw new InvalidOperationException($"Required region '{required}' is not configured.");
            }

            if (region.StabilityRole == RegionStabilityRole.Volatile)
            {
                throw new InvalidOperationException($"Volatile region '{required}' cannot be a required stability region.");
            }
        }

        foreach (var transitionRegion in Transition.TransitionRegions)
        {
            var region = Regions.FirstOrDefault(x => string.Equals(x.Name, transitionRegion, StringComparison.Ordinal));
            if (region is null)
            {
                throw new InvalidOperationException($"Transition region '{transitionRegion}' is not configured.");
            }

            if (!region.ObserveTransition)
            {
                throw new InvalidOperationException($"Region '{transitionRegion}' is disabled for transition observation.");
            }
        }

        if (Kind == GateProfileKind.StableRegion && Stable.RequiredRegions.Count == 0)
        {
            throw new InvalidOperationException("A stable-region profile must define required regions.");
        }

        if ((Kind is GateProfileKind.TransitionDetected or GateProfileKind.TransitionCompleted) && Transition.TransitionRegions.Count == 0)
        {
            throw new InvalidOperationException("A transition profile must define transition regions.");
        }
    }

    private static void ValidateUnitScore(double value, string name)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidatePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidatePositive(TimeSpan value, string name)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static void ValidateNonNegative(TimeSpan value, string name)
    {
        if (value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

public static class BuiltInGateProfiles
{
    public static GateProfile GenericStableScreen => CreateFullFrame("GenericStableScreen", GateProfileKind.StableRegion);
    public static GateProfile StableFullFrame => CreateFullFrame("StableFullFrame", GateProfileKind.StableRegion);

    public static GateProfile StableHeaderAndPanel => new()
    {
        Name = "StableHeaderAndPanel",
        Kind = GateProfileKind.StableRegion,
        Regions = PokemonScreenRegions(),
        Stable = new StableRegionGateOptions
        {
            RequiredRegions = new[] { "Header", "Panel", "BottomControl" },
            MinimumStableFrames = 3,
            MinimumStableDuration = TimeSpan.FromMilliseconds(150),
            MaximumMotionScore = 0.05,
            MaximumDifferenceScore = 0.04,
            MinimumSimilarityScore = 0.94,
            MinimumSharpnessScore = 0.18,
            MaximumObservationDuration = TimeSpan.FromSeconds(4),
            MinimumEvidenceFrameIdDistance = 2,
            MinimumEvidenceTimeDistance = TimeSpan.FromMilliseconds(80),
            MaximumEvidenceVisualSimilarity = 1.0
        }
    };

    public static GateProfile GenericScreenTransition => new()
    {
        Name = "GenericScreenTransition",
        Kind = GateProfileKind.TransitionCompleted,
        Regions = PokemonScreenRegions(),
        Stable = new StableRegionGateOptions
        {
            RequiredRegions = new[] { "Header", "Panel", "BottomControl" },
            MinimumStableFrames = 3,
            MinimumStableDuration = TimeSpan.FromMilliseconds(150),
            MaximumMotionScore = 0.05,
            MaximumDifferenceScore = 0.04,
            MinimumSimilarityScore = 0.94,
            MinimumSharpnessScore = 0.18,
            MaximumObservationDuration = TimeSpan.FromSeconds(8)
        },
        Transition = new TransitionGateOptions
        {
            TransitionRegions = new[] { "Header", "Panel", "BottomControl" },
            MinimumChangedFrames = 2,
            MinimumChangedDuration = TimeSpan.FromMilliseconds(70),
            MinimumMotionScore = 0.08,
            MinimumDifferenceScore = 0.07,
            MinimumPreStableFrames = 3,
            MinimumPostStableFrames = 3,
            MinimumStableDuration = TimeSpan.FromMilliseconds(150),
            MaximumStableMotionScore = 0.05,
            MaximumStableDifferenceScore = 0.04,
            MinimumStableSimilarityScore = 0.94,
            MinimumSharpnessScore = 0.18,
            MinimumMeaningfulChange = 0.05,
            MaximumObservationDuration = TimeSpan.FromSeconds(12)
        }
    };

    public static IReadOnlyDictionary<string, GateProfile> All { get; } =
        new Dictionary<string, GateProfile>(StringComparer.OrdinalIgnoreCase)
        {
            [GenericStableScreen.Name] = GenericStableScreen,
            [GenericScreenTransition.Name] = GenericScreenTransition,
            [StableHeaderAndPanel.Name] = StableHeaderAndPanel,
            [StableFullFrame.Name] = StableFullFrame
        };

    private static GateProfile CreateFullFrame(string name, GateProfileKind kind) => new()
    {
        Name = name,
        Kind = kind,
        Regions = new[]
        {
            new RegionDefinition
            {
                Name = "FullFrame",
                Region = new NormalizedRegion(0, 0, 1, 1),
                StabilityRole = RegionStabilityRole.Required,
                ObserveTransition = true
            }
        },
        Stable = new StableRegionGateOptions
        {
            RequiredRegions = new[] { "FullFrame" },
            MinimumStableFrames = 3,
            MinimumStableDuration = TimeSpan.FromMilliseconds(150),
            MaximumObservationDuration = TimeSpan.FromSeconds(4)
        }
    };

    private static IReadOnlyList<RegionDefinition> PokemonScreenRegions() =>
        new[]
        {
            new RegionDefinition
            {
                Name = "FullFrame",
                Region = new NormalizedRegion(0, 0, 1, 1),
                StabilityRole = RegionStabilityRole.DiagnosticOnly,
                ObserveTransition = false
            },
            new RegionDefinition
            {
                Name = "Header",
                Region = new NormalizedRegion(0.10, 0.02, 0.80, 0.14),
                StabilityRole = RegionStabilityRole.Required,
                ObserveTransition = true
            },
            new RegionDefinition
            {
                Name = "Model",
                Region = new NormalizedRegion(0.10, 0.16, 0.80, 0.39),
                StabilityRole = RegionStabilityRole.Volatile,
                ObserveTransition = false
            },
            new RegionDefinition
            {
                Name = "AnimatedBackground",
                Region = new NormalizedRegion(0.00, 0.16, 1.00, 0.39),
                StabilityRole = RegionStabilityRole.Volatile,
                ObserveTransition = false
            },
            new RegionDefinition
            {
                Name = "Panel",
                Region = new NormalizedRegion(0.05, 0.55, 0.90, 0.32),
                StabilityRole = RegionStabilityRole.Required,
                ObserveTransition = true
            },
            new RegionDefinition
            {
                Name = "BottomControl",
                Region = new NormalizedRegion(0.05, 0.87, 0.90, 0.11),
                StabilityRole = RegionStabilityRole.Required,
                ObserveTransition = true
            }
        };
}

public static class GateProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<GateProfile> LoadAsync(string nameOrPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nameOrPath);
        if (BuiltInGateProfiles.All.TryGetValue(nameOrPath, out var builtIn))
        {
            builtIn.Validate();
            return builtIn;
        }

        if (!File.Exists(nameOrPath))
        {
            throw new FileNotFoundException($"Unknown built-in profile and profile file not found: {nameOrPath}", nameOrPath);
        }

        await using var stream = File.OpenRead(nameOrPath);
        var profile = await JsonSerializer.DeserializeAsync<GateProfile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Profile JSON did not contain a gate profile.");
        profile.Validate();
        return profile;
    }
}
