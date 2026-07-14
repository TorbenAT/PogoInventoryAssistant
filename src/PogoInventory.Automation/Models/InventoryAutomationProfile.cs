using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Models;

public sealed record InventoryAutomationProfile
{
    public string SchemaVersion { get; init; } = "1.0";
    public string Name { get; init; } = "Unnamed automation profile";
    public required NormalizedPoint FirstInventoryCard { get; init; }
    public required NormalizedPoint DetailsMenuButton { get; init; }
    public required NormalizedPoint AppraiseMenuItem { get; init; }
    public required NormalizedSwipe NextPokemonSwipe { get; init; }
    public required NormalizedRegion IdentityRegion { get; init; }
    public FingerprintMode IdentityFingerprintMode { get; init; } = FingerprintMode.Color;
    public int IdentityFingerprintWidth { get; init; } = 16;
    public int IdentityFingerprintHeight { get; init; } = 16;
    public double SamePokemonSimilarityThreshold { get; init; } = 0.995;
    public int StatePollMilliseconds { get; init; } = 350;
    public int StateTimeoutSeconds { get; init; } = 12;
    public int PostActionSettleMilliseconds { get; init; } = 250;
    public int MaxSwipeAttemptsAtEnd { get; init; } = 2;
    public int DefaultMaximumItems { get; init; } = 12000;
    public decimal MaximumBatteryTemperatureCelsius { get; init; } = 45m;
    public int MinimumBatteryPercent { get; init; } = 15;

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new ArgumentException(
                $"Unsupported automation profile schema '{SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Automation profile name cannot be empty.");
        }

        ArgumentNullException.ThrowIfNull(FirstInventoryCard);
        ArgumentNullException.ThrowIfNull(DetailsMenuButton);
        ArgumentNullException.ThrowIfNull(AppraiseMenuItem);
        ArgumentNullException.ThrowIfNull(NextPokemonSwipe);
        ArgumentNullException.ThrowIfNull(IdentityRegion);

        FirstInventoryCard.Validate(nameof(FirstInventoryCard));
        DetailsMenuButton.Validate(nameof(DetailsMenuButton));
        AppraiseMenuItem.Validate(nameof(AppraiseMenuItem));
        NextPokemonSwipe.Validate(nameof(NextPokemonSwipe));
        IdentityRegion.Validate();

        if (IdentityFingerprintWidth is < 4 or > 64 ||
            IdentityFingerprintHeight is < 4 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(IdentityFingerprintWidth),
                "Identity fingerprint dimensions must be between 4 and 64.");
        }

        if (!double.IsFinite(SamePokemonSimilarityThreshold) ||
            SamePokemonSimilarityThreshold is <= 0.8 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(SamePokemonSimilarityThreshold),
                "Same-Pokémon similarity threshold must be greater than 0.8 and at most 1.");
        }

        if (StatePollMilliseconds is < 100 or > 5000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StatePollMilliseconds),
                "State poll interval must be between 100 and 5000 milliseconds.");
        }

        if (StateTimeoutSeconds is < 2 or > 120)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StateTimeoutSeconds),
                "State timeout must be between 2 and 120 seconds.");
        }

        if (PostActionSettleMilliseconds is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PostActionSettleMilliseconds),
                "Post-action settle delay must be between 0 and 10000 milliseconds.");
        }

        if (MaxSwipeAttemptsAtEnd is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxSwipeAttemptsAtEnd),
                "End detection swipe attempts must be between 1 and 10.");
        }

        if (DefaultMaximumItems is < 1 or > 50000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DefaultMaximumItems),
                "Default maximum items must be between 1 and 50000.");
        }

        if (MaximumBatteryTemperatureCelsius is < 25 or > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumBatteryTemperatureCelsius),
                "Maximum battery temperature must be between 25 and 60 Celsius.");
        }

        if (MinimumBatteryPercent is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MinimumBatteryPercent),
                "Minimum battery percentage must be between 1 and 100.");
        }
    }
}
