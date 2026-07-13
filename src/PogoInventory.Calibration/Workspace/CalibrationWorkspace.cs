using System.Text.Json;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Services;
using PogoInventory.Vision.Models;

namespace PogoInventory.Calibration.Workspace;

public sealed class CalibrationWorkspace
{
    public const string MarkerFileName = ".pogo-private-calibration";

    private CalibrationWorkspace(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath);
        MarkerPath = Path.Combine(RootPath, MarkerFileName);
        FixturesPath = Path.Combine(RootPath, "fixtures");
        ProfilesPath = Path.Combine(RootPath, "profiles");
        ReportsPath = Path.Combine(RootPath, "reports");
        ManifestPath = Path.Combine(RootPath, "fixture-manifest.local.json");
        AnchorPlanPath = Path.Combine(RootPath, "anchor-plan.local.json");
        ProfilePath = Path.Combine(ProfilesPath, "screen-profile.local.json");
    }

    public string RootPath { get; }
    public string MarkerPath { get; }
    public string FixturesPath { get; }
    public string ProfilesPath { get; }
    public string ReportsPath { get; }
    public string ManifestPath { get; }
    public string AnchorPlanPath { get; }
    public string ProfilePath { get; }

    public static CalibrationWorkspace Open(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var workspace = new CalibrationWorkspace(rootPath);
        if (!File.Exists(workspace.MarkerPath))
        {
            throw new CalibrationException(
                CalibrationErrorCode.InvalidWorkspace,
                $"'{workspace.RootPath}' is not an initialised private calibration workspace. " +
                "Run calibration-init first.");
        }

        return workspace;
    }

    public static async Task<CalibrationWorkspace> InitializeAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var workspace = new CalibrationWorkspace(rootPath);

        Directory.CreateDirectory(workspace.RootPath);
        Directory.CreateDirectory(workspace.FixturesPath);
        Directory.CreateDirectory(workspace.ProfilesPath);
        Directory.CreateDirectory(workspace.ReportsPath);

        foreach (var state in Enum.GetValues<ScreenState>())
        {
            Directory.CreateDirectory(Path.Combine(workspace.FixturesPath, state.ToString()));
        }

        if (!File.Exists(workspace.MarkerPath))
        {
            await AtomicFile.WriteTextAsync(
                workspace.MarkerPath,
                "Private local Pokémon GO calibration workspace. Do not commit real screenshots.\n",
                cancellationToken);
        }

        var readmePath = Path.Combine(workspace.RootPath, "PRIVATE-README.txt");
        if (!File.Exists(readmePath))
        {
            await AtomicFile.WriteTextAsync(
                readmePath,
                "Place PNG screenshots in fixtures/<ExpectedState>/.\n" +
                "Run calibration-index after adding files.\n" +
                "Review every fixture in fixture-manifest.local.json before setting all safety fields to true.\n" +
                "Real screenshots and generated local profiles must not be committed while the repository is public.\n",
                cancellationToken);
        }

        if (!File.Exists(workspace.ManifestPath))
        {
            var manifest = CreateManifestTemplate();
            await AtomicFile.WriteTextAsync(
                workspace.ManifestPath,
                JsonSerializer.Serialize(
                    manifest,
                    CalibrationJson.CreateOptions(writeIndented: true)),
                cancellationToken);
        }

        if (!File.Exists(workspace.AnchorPlanPath))
        {
            var plan = CreateAnchorPlanTemplate();
            await AtomicFile.WriteTextAsync(
                workspace.AnchorPlanPath,
                JsonSerializer.Serialize(
                    plan,
                    CalibrationJson.CreateOptions(writeIndented: true)),
                cancellationToken);
        }

        return workspace;
    }

    private static ScreenFixtureManifest CreateManifestTemplate() =>
        new()
        {
            Name = "Torben private real-screen fixture set",
            ProfileName = "Torben Android Pokémon GO profile",
            Acceptance = new CalibrationAcceptancePolicy
            {
                MaximumFalsePositives = 0,
                MaximumMisclassifications = 0,
                MaximumWeakAnchors = 0,
                MinimumWinnerMargin = 0.05,
                MinimumAnchorSeparation = 0.05,
                States = new[]
                {
                    Requirement(ScreenState.InventoryList, 3, 0.90),
                    Requirement(ScreenState.PokemonDetails, 3, 0.90),
                    Requirement(ScreenState.AppraisalOpen, 3, 0.90),
                    Requirement(ScreenState.PokemonMenuOpen, 1, 0.80),
                    Requirement(ScreenState.TagDialogOpen, 1, 0.80),
                    Requirement(ScreenState.SearchOpen, 1, 0.80),
                    Requirement(ScreenState.Loading, 1, 0.80),
                    Requirement(ScreenState.Popup, 1, 0.80),
                    Requirement(ScreenState.NetworkError, 1, 0.80),
                    Requirement(ScreenState.Unknown, 4, 1.00)
                }
            },
            Fixtures = Array.Empty<ScreenFixtureDefinition>()
        };

    private static StateAcceptanceRequirement Requirement(
        ScreenState state,
        int minimumFixtures,
        double minimumRecall) =>
        new()
        {
            State = state,
            MinimumApprovedFixtures = minimumFixtures,
            MinimumRecall = minimumRecall
        };

    private static CalibrationAnchorPlan CreateAnchorPlanTemplate() =>
        new()
        {
            Name = "Torben Android Pokémon GO anchor plan",
            RequiredOrientation = ScreenOrientation.Portrait,
            MinimumWidth = 720,
            MinimumHeight = 1280,
            MinimumAspectRatio = 0.40,
            MaximumAspectRatio = 0.60,
            MinimumStateScore = 0.90,
            MinimumWinnerMargin = 0.05,
            States = Enum.GetValues<ScreenState>()
                .Where(state => state != ScreenState.Unknown)
                .Select(state => new CalibrationStatePlan
                {
                    State = state,
                    Anchors = Array.Empty<CalibrationAnchorDefinition>()
                })
                .ToArray()
        };
}
