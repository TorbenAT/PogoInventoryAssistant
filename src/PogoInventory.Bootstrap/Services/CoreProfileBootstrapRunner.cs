using System.Security.Cryptography;
using System.Text.Json;
using PogoInventory.Automation.Models;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Reporting;
using PogoInventory.Calibration.Services;
using PogoInventory.Bootstrap.Models;
using PogoInventory.Device;
using PogoInventory.Device.Transport;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;

namespace PogoInventory.Bootstrap.Services;

public sealed class CoreProfileBootstrapRunner
{
    private readonly IAndroidAutomationTransport _transport;

    public CoreProfileBootstrapRunner(IAndroidAutomationTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task<CoreProfileBootstrapResult> RunAsync(
        string outputDirectory,
        InventoryAutomationProfile automationProfile,
        CalibrationAnchorPlan anchorPlan,
        string? requestedSerial = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(automationProfile);
        ArgumentNullException.ThrowIfNull(anchorPlan);
        automationProfile.Validate();
        AnchorPlanLoader.Validate(anchorPlan);

        var fullOutput = Path.GetFullPath(outputDirectory);
        var captures = Path.Combine(fullOutput, "captures");
        var acceptanceDirectory = Path.Combine(fullOutput, "acceptance");
        Directory.CreateDirectory(captures);

        var devices = await _transport.ListDevicesAsync(cancellationToken);
        var selected = DeviceSnapshotService.SelectDevice(devices, requestedSerial);
        var metadata = await _transport.ReadMetadataAsync(selected.Serial, cancellationToken);
        var width = metadata.Screen.EffectiveWidth ??
            throw new InvalidOperationException("Device did not report an effective width.");
        var height = metadata.Screen.EffectiveHeight ??
            throw new InvalidOperationException("Device did not report an effective height.");

        if (width < anchorPlan.MinimumWidth || height < anchorPlan.MinimumHeight)
        {
            throw new InvalidOperationException(
                $"Device geometry {width}x{height} is below the bootstrap plan minimum.");
        }

        var captured = new List<BootstrapCapture>();
        var current = await CaptureAsync(
            selected.Serial,
            captures,
            "bootstrap-inventory-list",
            ScreenState.InventoryList,
            cancellationToken);
        captured.Add(current);

        current = await TapCaptureChangedAsync(
            selected.Serial,
            automationProfile.FirstInventoryCard,
            current,
            width,
            height,
            captures,
            "bootstrap-pokemon-details",
            ScreenState.PokemonDetails,
            GetStabilityAnchor(anchorPlan, ScreenState.PokemonDetails),
            automationProfile,
            cancellationToken);
        captured.Add(current);

        current = await TapCaptureChangedAsync(
            selected.Serial,
            automationProfile.DetailsMenuButton,
            current,
            width,
            height,
            captures,
            "bootstrap-pokemon-menu-open",
            ScreenState.PokemonMenuOpen,
            GetStabilityAnchor(anchorPlan, ScreenState.PokemonMenuOpen),
            automationProfile,
            cancellationToken);
        captured.Add(current);

        current = await TapCaptureChangedAsync(
            selected.Serial,
            automationProfile.AppraiseMenuItem,
            current,
            width,
            height,
            captures,
            "bootstrap-appraisal-1",
            ScreenState.AppraisalOpen,
            GetStabilityAnchor(anchorPlan, ScreenState.AppraisalOpen),
            automationProfile,
            cancellationToken);
        captured.Add(current);

        for (var index = 2; index <= 3; index++)
        {
            current = await SwipeCaptureChangedAsync(
                selected.Serial,
                automationProfile.NextPokemonSwipe,
                current,
                width,
                height,
                captures,
                $"bootstrap-appraisal-{index}",
                ScreenState.AppraisalOpen,
                GetStabilityAnchor(anchorPlan, ScreenState.AppraisalOpen),
                automationProfile,
                cancellationToken);
            captured.Add(current);
        }

        var manifest = CreateManifest(captured);
        var manifestPath = Path.Combine(fullOutput, "fixture-manifest.local.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                CalibrationJson.CreateOptions(writeIndented: true)),
            cancellationToken);

        var profilePath = Path.Combine(fullOutput, "screen-profile.local.json");
        var profile = await CalibrationProfileBuilder.BuildAsync(
            manifest,
            anchorPlan,
            captures,
            profilePath,
            cancellationToken);
        var acceptance = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            captures,
            cancellationToken);
        await CalibrationReportWriter.WriteAsync(
            acceptance,
            acceptanceDirectory,
            cancellationToken);

        if (!acceptance.Accepted ||
            acceptance.FalsePositiveCount != 0 ||
            acceptance.MisclassificationCount != 0)
        {
            throw new InvalidOperationException(
                "The generated core profile was rejected. See the local acceptance report.");
        }

        return new CoreProfileBootstrapResult
        {
            OutputDirectory = fullOutput,
            CaptureDirectory = captures,
            ManifestPath = manifestPath,
            ProfilePath = profilePath,
            AcceptanceDirectory = acceptanceDirectory,
            Profile = profile,
            Acceptance = acceptance,
            CapturedFiles = captured.Select(x => x.Path).ToArray()
        };
    }

    private async Task<BootstrapCapture> TapCaptureChangedAsync(
        string serial,
        NormalizedPoint point,
        BootstrapCapture previous,
        int width,
        int height,
        string captures,
        string id,
        ScreenState state,
        CalibrationAnchorDefinition stabilityAnchor,
        InventoryAutomationProfile profile,
        CancellationToken cancellationToken)
    {
        var pixel = point.ToPixels(width, height);
        await _transport.TapAsync(serial, pixel.X, pixel.Y, cancellationToken);
        return await WaitForChangedCaptureAsync(
            serial,
            previous,
            captures,
            id,
            state,
            stabilityAnchor,
            profile,
            cancellationToken);
    }

    private async Task<BootstrapCapture> SwipeCaptureChangedAsync(
        string serial,
        NormalizedSwipe swipe,
        BootstrapCapture previous,
        int width,
        int height,
        string captures,
        string id,
        ScreenState state,
        CalibrationAnchorDefinition stabilityAnchor,
        InventoryAutomationProfile profile,
        CancellationToken cancellationToken)
    {
        var start = swipe.Start.ToPixels(width, height);
        var end = swipe.End.ToPixels(width, height);
        await _transport.SwipeAsync(
            serial,
            start.X,
            start.Y,
            end.X,
            end.Y,
            swipe.DurationMilliseconds,
            cancellationToken);
        return await WaitForChangedCaptureAsync(
            serial,
            previous,
            captures,
            id,
            state,
            stabilityAnchor,
            profile,
            cancellationToken);
    }

    private async Task<BootstrapCapture> WaitForChangedCaptureAsync(
        string serial,
        BootstrapCapture previous,
        string captures,
        string id,
        ScreenState state,
        CalibrationAnchorDefinition stabilityAnchor,
        InventoryAutomationProfile profile,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(profile.StateTimeoutSeconds);
        byte[]? previousStableFingerprint = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var png = await _transport.CaptureScreenshotPngAsync(serial, cancellationToken);
            var hash = Hash(png);
            if (!string.Equals(hash, previous.Sha256, StringComparison.Ordinal))
            {
                var image = PngDecoder.Decode(png);
                var fingerprint = FingerprintExtractor.Extract(
                    image,
                    stabilityAnchor.Region,
                    stabilityAnchor.Mode,
                    stabilityAnchor.FingerprintWidth,
                    stabilityAnchor.FingerprintHeight);
                if (previousStableFingerprint is not null &&
                    FingerprintComparer.Similarity(
                        previousStableFingerprint,
                        fingerprint) >= 0.995)
                {
                    return await SaveAsync(captures, id, state, png, hash, cancellationToken);
                }

                previousStableFingerprint = fingerprint;
            }

            if (profile.StatePollMilliseconds > 0)
            {
                await Task.Delay(profile.StatePollMilliseconds, cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Bootstrap timed out waiting for a changed screen after {previous.State}.");
    }

    private async Task<BootstrapCapture> CaptureAsync(
        string serial,
        string captures,
        string id,
        ScreenState state,
        CancellationToken cancellationToken)
    {
        var png = await _transport.CaptureScreenshotPngAsync(serial, cancellationToken);
        return await SaveAsync(captures, id, state, png, Hash(png), cancellationToken);
    }

    private static async Task<BootstrapCapture> SaveAsync(
        string captures,
        string id,
        ScreenState state,
        byte[] png,
        string hash,
        CancellationToken cancellationToken)
    {
        _ = PngDecoder.Decode(png);
        var fileName = id + ".png";
        var path = Path.Combine(captures, fileName);
        await File.WriteAllBytesAsync(path, png, cancellationToken);
        return new BootstrapCapture(id, state, fileName, path, hash);
    }

    private static ScreenFixtureManifest CreateManifest(
        IReadOnlyList<BootstrapCapture> captures) =>
        new()
        {
            Name = "Automatic local core profile bootstrap",
            ProfileName = "Local Pokémon GO core screen profile",
            Acceptance = new CalibrationAcceptancePolicy
            {
                MaximumFalsePositives = 0,
                MaximumMisclassifications = 0,
                MaximumWeakAnchors = 4,
                MinimumWinnerMargin = 0.02,
                MinimumAnchorSeparation = 0.01,
                States = new[]
                {
                    Requirement(ScreenState.InventoryList, 1),
                    Requirement(ScreenState.PokemonDetails, 1),
                    Requirement(ScreenState.PokemonMenuOpen, 1),
                    Requirement(ScreenState.AppraisalOpen, 3)
                }
            },
            Fixtures = captures.Select(capture => new ScreenFixtureDefinition
            {
                Id = capture.Id,
                RelativePath = capture.FileName,
                ExpectedState = capture.State,
                Sha256 = capture.Sha256,
                SafetyReview = new FixtureSafetyReview
                {
                    AccountIdentitySafe = true,
                    LocationSafe = true,
                    NotificationsSafe = true,
                    OtherPersonalDataSafe = true,
                    ApprovedForCalibration = true,
                    ReviewedBy = "AutomaticLocalBootstrap",
                    ReviewedAtUtc = DateTimeOffset.UtcNow
                },
                Tags = new[] { "automatic", "local", "core" }
            }).ToArray()
        };

    private static StateAcceptanceRequirement Requirement(
        ScreenState state,
        int count) =>
        new()
        {
            State = state,
            MinimumApprovedFixtures = count,
            MinimumRecall = 1
        };

    private static CalibrationAnchorDefinition GetStabilityAnchor(
        CalibrationAnchorPlan plan,
        ScreenState state)
    {
        var statePlan = plan.States.SingleOrDefault(x => x.State == state) ??
            throw new InvalidOperationException(
                $"Bootstrap anchor plan has no state definition for {state}.");
        return statePlan.Anchors.FirstOrDefault(x =>
                   x.Expectation == AnchorExpectation.Required) ??
            throw new InvalidOperationException(
                $"Bootstrap state {state} has no required stability anchor.");
    }

    private static string Hash(byte[] data) =>
        Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private sealed record BootstrapCapture(
        string Id,
        ScreenState State,
        string FileName,
        string Path,
        string Sha256);
}
