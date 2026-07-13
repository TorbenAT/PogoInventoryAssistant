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
        IncomingPath = Path.Combine(RootPath, "incoming");
        FixturesPath = Path.Combine(RootPath, "fixtures");
        ProfilesPath = Path.Combine(RootPath, "profiles");
        ReportsPath = Path.Combine(RootPath, "reports");
        ManifestPath = Path.Combine(RootPath, "fixture-manifest.local.json");
        AnchorPlanPath = Path.Combine(RootPath, "anchor-plan.local.json");
        CapturePlanPath = Path.Combine(RootPath, "capture-plan.local.json");
        CaptureSessionPath = Path.Combine(RootPath, "capture-session.local.json");
        ProfilePath = Path.Combine(ProfilesPath, "screen-profile.local.json");
    }

    public string RootPath { get; }
    public string MarkerPath { get; }
    public string IncomingPath { get; }
    public string FixturesPath { get; }
    public string ProfilesPath { get; }
    public string ReportsPath { get; }
    public string ManifestPath { get; }
    public string AnchorPlanPath { get; }
    public string CapturePlanPath { get; }
    public string CaptureSessionPath { get; }
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
        Directory.CreateDirectory(workspace.IncomingPath);
        Directory.CreateDirectory(workspace.FixturesPath);
        Directory.CreateDirectory(workspace.ProfilesPath);
        Directory.CreateDirectory(workspace.ReportsPath);

        foreach (var state in Enum.GetValues<ScreenState>())
        {
            Directory.CreateDirectory(Path.Combine(workspace.IncomingPath, state.ToString()));
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
                "Guided ADB captures are written to incoming/<ExpectedState>/.\n" +
                "Incoming screenshots are never calibration fixtures until explicitly reviewed and promoted.\n" +
                "Run calibration-capture-status to see missing screen states.\n" +
                "Review every screenshot for account, location, notification and other personal data.\n" +
                "Run calibration-capture-approve only after that review.\n" +
                "Real screenshots, device serials, sessions and generated local profiles must not be committed.\n",
                cancellationToken);
        }

        if (!File.Exists(workspace.ManifestPath))
        {
            await WriteJsonAsync(
                workspace.ManifestPath,
                CreateManifestTemplate(),
                cancellationToken);
        }

        if (!File.Exists(workspace.AnchorPlanPath))
        {
            await WriteJsonAsync(
                workspace.AnchorPlanPath,
                CreateAnchorPlanTemplate(),
                cancellationToken);
        }

        if (!File.Exists(workspace.CapturePlanPath))
        {
            await WriteJsonAsync(
                workspace.CapturePlanPath,
                CreateCapturePlanTemplate(),
                cancellationToken);
        }

        return workspace;
    }

    private static Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) =>
        AtomicFile.WriteTextAsync(
            path,
            JsonSerializer.Serialize(
                value,
                CalibrationJson.CreateOptions(writeIndented: true)),
            cancellationToken);

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

    private static CalibrationCapturePlan CreateCapturePlanTemplate() =>
        new()
        {
            Name = "Torben Android Pokémon GO guided real-screen capture",
            RequiredOrientation = ScreenOrientation.Portrait,
            MinimumWidth = 720,
            MinimumHeight = 1280,
            LockDeviceSerial = true,
            LockExactGeometry = true,
            Requirements = new[]
            {
                Capture(
                    ScreenState.InventoryList,
                    3,
                    "Open Pokémon storage with the grid visible and no dialog open.",
                    "Use different scroll positions.",
                    "Include different Pokémon artwork behind the fixed interface."),
                Capture(
                    ScreenState.PokemonDetails,
                    3,
                    "Open one Pokémon's normal details page with appraisal and menus closed.",
                    "Use different species and CP values.",
                    "Do not rely on artwork, name or CP as a future anchor."),
                Capture(
                    ScreenState.AppraisalOpen,
                    3,
                    "Open Appraise so the appraisal overlay and IV bars are fully visible.",
                    "Use different IV combinations.",
                    "Use different team-leader dialogue where possible."),
                Capture(
                    ScreenState.PokemonMenuOpen,
                    2,
                    "Open the Pokémon details action menu without selecting an action.",
                    "Capture at least two different Pokémon."),
                Capture(
                    ScreenState.TagDialogOpen,
                    2,
                    "Open the tag-selection dialog without changing any tag.",
                    "Use different Pokémon if possible."),
                Capture(
                    ScreenState.SearchOpen,
                    2,
                    "Open the Pokémon storage search interface with the search field visible.",
                    "Capture one empty search and one harmless query if practical."),
                Capture(
                    ScreenState.Popup,
                    2,
                    "Capture non-destructive popups that could interrupt scanning.",
                    "Use more than one popup layout if available."),
                Capture(
                    ScreenState.Unknown,
                    6,
                    "Capture screens that must never be accepted as a known inventory state.",
                    "Include the map or an unrelated app.",
                    "Include a partial transition or interrupted layout.",
                    "Include at least one visually conflicting or unsupported screen."),
                Capture(
                    ScreenState.Loading,
                    1,
                    "Capture a genuine in-app loading state. This is required before profile acceptance but is ordered last so the session can progress while you wait for a suitable example."),
                Capture(
                    ScreenState.NetworkError,
                    1,
                    "Capture a genuine Pokémon GO network-error screen. This is required before profile acceptance but is ordered last so the session can progress while you wait for a suitable example.")
            }
        };

    private static CalibrationCaptureRequirement Capture(
        ScreenState state,
        int required,
        string instruction,
        string? hint1 = null,
        string? hint2 = null,
        string? hint3 = null,
        bool optional = false) =>
        new()
        {
            State = state,
            RequiredUniqueCaptures = required,
            Instruction = instruction,
            VariationHints = new[] { hint1, hint2, hint3 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToArray(),
            OptionalWhenUnavailable = optional
        };
}
