using System.Globalization;
using System.Text.Json;
using PogoInventory.Appraisal.Models;
using PogoInventory.Appraisal.Services;
using PogoInventory.Application;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Automation.Transport;
using PogoInventory.Bootstrap.Services;
using PogoInventory.Calibration.Errors;
using PogoInventory.Calibration.Models;
using PogoInventory.Calibration.Reporting;
using PogoInventory.Calibration.Services;
using PogoInventory.Calibration.Workspace;
using PogoInventory.CalcyProbe.Models;
using PogoInventory.CalcyProbe.Services;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.CropAtlas.Models;
using PogoInventory.CropAtlas.Services;
using PogoInventory.CropAtlas.Semantic.Models;
using PogoInventory.CropAtlas.Semantic.Services;
using PogoInventory.Device;
using PogoInventory.Device.Adb;
using PogoInventory.Device.Errors;
using PogoInventory.Device.Models;
using PogoInventory.Device.Transport;
using PogoInventory.ImagePretest.Models;
using PogoInventory.ImagePretest.Services;
using PogoInventory.Observations.Models;
using PogoInventory.Observations.Parsing;
using PogoInventory.Observations.Providers;
using PogoInventory.Observations.Sources;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.RegionDiscovery.Services;
using PogoInventory.Vision.Imaging;
using PogoInventory.Vision.Models;
using PogoInventory.Vision.Profiles;
using PogoInventory.Vision.Reporting;
using PogoInventory.Verification.Models;
using PogoInventory.Verification.Services;
using PogoInventory.Vision.Errors;
using PogoInventory.SelfTest;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Real-state detector regression fixtures", StateDetectorRegressionTests.RunAsync),
    ("Guarded inventory close enforces one map transition", GuardedInventoryCloseTests.RunAsync),
    ("Recovery ROI consensus ignores full-screen animation", GuardedInventoryRecoveryTests.RunRoiConsensusAsync),
    ("Guarded inventory recovery enforces stable bounded transitions", GuardedInventoryRecoveryTests.RunStateMachineAsync),
    ("Appraisal exit uses one tap per substate and Back only from Details", GuardedInventoryRecoveryTests.RunExitActionsAsync),
    ("Perfect is kept", Sync(PerfectIsKept)),
    ("Trade nickname is kept", Sync(TradeNicknameIsKept)),
    ("Old Pokémon is kept", Sync(OldPokemonIsKept)),
    ("Pokémon observation preserves catch location", Sync(PokemonObservationPreservesCatchLocation)),
    ("Shadow is reviewed", Sync(ShadowIsReviewed)),
    ("Inferior duplicate is deleted", Sync(InferiorDuplicateIsDeleted)),
    ("Preliminary PvP candidate is reviewed", Sync(PvpCandidateIsReviewed)),
    ("Non-exact identity cannot be deleted", Sync(NonExactIdentityCannotBeDeleted)),
    ("Normal and party-hat Pikachu are distinct variants", Sync(NormalAndPartyHatPikachuAreDistinctVariants)),
    ("Different Pikachu costumes are distinct variants", Sync(DifferentPikachuCostumesAreDistinctVariants)),
    ("Ordinary and background Pikachu are distinct variants", Sync(OrdinaryAndBackgroundPikachuAreDistinctVariants)),
    ("Named backgrounds are distinct variants", Sync(NamedBackgroundsAreDistinctVariants)),
    ("Exact costume identity remains review protected", Sync(ExactCostumeIdentityRemainsReviewProtected)),
    ("Exact background identity remains keep protected", Sync(ExactBackgroundIdentityRemainsKeepProtected)),
    ("Unknown costume is not confirmed none", Sync(UnknownCostumeIsNotConfirmedNone)),
    ("Unknown background is not confirmed none", Sync(UnknownBackgroundIsNotConfirmedNone)),
    ("Unknown variant fields forbid delete", Sync(UnknownVariantFieldsForbidDelete)),
    ("Contradictory exact variant markers are rejected", Sync(ContradictoryExactVariantMarkersAreRejected)),
    ("Exact same variant can be evaluated as duplicates", Sync(ExactSameVariantCanBeEvaluatedAsDuplicates)),
    ("Shiny and non-shiny do not collapse", Sync(ShinyAndNonShinyDoNotCollapse)),
    ("Instance fingerprint cannot replace variant identity", Sync(InstanceFingerprintCannotReplaceVariantIdentity)),
    ("ADB device list parser recognises states", Sync(AdbDeviceListParserRecognisesStates)),
    ("ADB metadata parsers read screen and battery", Sync(AdbMetadataParsersReadScreenAndBattery)),
    ("ADB transport uses only expected read-only commands", AdbTransportUsesExpectedCommandsAsync),
    ("ADB known-app stop is allow-listed", AdbKnownAppStopIsAllowListedAsync),
    ("ADB automation transport uses only tap and swipe commands", AdbAutomationTransportUsesExpectedCommandsAsync),
    ("ADB app inspection uses only named read commands", AdbAppInspectionUsesExpectedCommandsAsync),
    ("Calcy package dump parser reads metadata", Sync(CalcyPackageDumpParserReadsMetadata)),
    ("Calcy package dump parser detects missing package", Sync(CalcyPackageDumpParserDetectsMissingPackage)),
    ("Calcy logcat filter selects related lines", Sync(CalcyLogcatFilterSelectsRelatedLines)),
    ("Device selection rejects no authorised device", RejectsNoAuthorisedDeviceAsync),
    ("Device selection rejects multiple authorised devices", RejectsMultipleAuthorisedDevicesAsync),
    ("Requested serial selects one authorised device", RequestedSerialSelectsDeviceAsync),
    ("Fake snapshot writes PNG, metadata and manifest", FakeSnapshotWritesFilesAsync),
    ("Metadata serial mismatch is rejected", MetadataSerialMismatchIsRejectedAsync),
    ("Invalid screenshot is rejected", InvalidScreenshotIsRejectedAsync),
    ("Cancelled snapshot does not write files", CancelledSnapshotDoesNotWriteFilesAsync),
    ("PNG decoder reads synthetic fixture", Sync(PngDecoderReadsSyntheticFixture)),
    ("PNG decoder reconstructs Paeth-filtered row", Sync(PngDecoderReconstructsPaethFilteredRow)),
    ("Known screen fixtures classify correctly", KnownScreenFixturesClassifyAsync),
    ("Known modal requires allow-list and post-state", Sync(KnownModalRequiresAllowListAndPostState)),
    ("Inventory search query is safely validated", Sync(InventorySearchQueryIsSafelyValidated)),
    ("Inventory search text is centrally escaped and submitted", InventorySearchTests.RunEncodingAsync),
    ("Inventory search workflow verifies clear, text and result transitions", InventorySearchTests.RunWorkflowAsync),
    ("Tag selector profile bounds scrolling and requires named templates", TagSelectorTests.RunProfileSafetyAsync),
    ("Tag workflow is idempotent and never uses a fixed row", TagSelectorTests.RunWorkflowContractAsync),
    ("Details tag layout counts zero, one and two mutable pills", TagSelectorTests.RunDetailsLayoutAsync),
    ("Incomplete screen returns Unknown", IncompleteScreenReturnsUnknownAsync),
    ("Conflicting screen returns Unknown", ConflictingScreenReturnsUnknownAsync),
    ("Landscape screen fails closed", LandscapeScreenFailsClosedAsync),
    ("Confidence thresholds are deterministic", ConfidenceThresholdsAreDeterministicAsync),
    ("Invalid PNG is rejected", Sync(InvalidPngIsRejected)),
    ("Screen evidence report writes JSON", ScreenEvidenceReportWritesJsonAsync),
    ("Calibration workspace creates private structure", CalibrationWorkspaceCreatesPrivateStructureAsync),
    ("Calibration index preserves and resets approvals", CalibrationIndexPreservesAndResetsApprovalsAsync),
    ("Calibration rejects fixture path traversal", Sync(CalibrationRejectsFixturePathTraversal)),
    ("Synthetic calibration profile builds", SyntheticCalibrationProfileBuildsAsync),
    ("Synthetic calibration acceptance passes", SyntheticCalibrationAcceptancePassesAsync),
    ("Calibration false positive fails acceptance", CalibrationFalsePositiveFailsAcceptanceAsync),
    ("Calibration rejects changed approved fixture", CalibrationRejectsChangedApprovedFixtureAsync),
    ("Calibration report writes all formats", CalibrationReportWritesAllFormatsAsync),
    ("Capture workspace creates guided private structure", CaptureWorkspaceCreatesGuidedStructureAsync),
    ("Guided capture writes incoming file and session", GuidedCaptureWritesIncomingFileAndSessionAsync),
    ("Duplicate guided capture does not add coverage", DuplicateGuidedCaptureDoesNotAddCoverageAsync),
    ("Capture session rejects device change", CaptureSessionRejectsDeviceChangeAsync),
    ("Capture session rejects geometry change", CaptureSessionRejectsGeometryChangeAsync),
    ("Capture session rejects changed plan", CaptureSessionRejectsChangedPlanAsync),
    ("Changed incoming capture is rejected", ChangedIncomingCaptureIsRejectedAsync),
    ("Capture promotion requires explicit privacy review", CapturePromotionRequiresPrivacyReviewAsync),
    ("Reviewed capture promotes to approved fixture", ReviewedCapturePromotesToFixtureAsync),
    ("Promotion refuses untracked destination file", PromotionRefusesUntrackedDestinationFileAsync),
    ("Duplicate capture cannot be promoted", DuplicateCaptureCannotBePromotedAsync),
    ("Capture status prioritises required states", Sync(CaptureStatusPrioritisesRequiredStates)),
    ("Capture status report writes JSON and Markdown", CaptureStatusReportWritesFilesAsync),
    ("Automation profile loads and validates", AutomationProfileLoadsAndValidatesAsync),
    ("Automatic scan captures three items and detects end", AutomaticScanCapturesThreeItemsAsync),
    ("Automatic scan respects maximum item limit", AutomaticScanRespectsMaximumItemLimitAsync),
    ("Completed automatic checkpoint is idempotent", CompletedAutomaticCheckpointIsIdempotentAsync),
    ("Fake scan attaches complete observations", FakeScanAttachesCompleteObservationsAsync),
    ("Partial observation stays partial", PartialObservationStaysPartialAsync),
    ("Conflicting observation stays conflicting", ConflictingObservationStaysConflictingAsync),
    ("Observation provider failure is recorded", ObservationProviderFailureIsRecordedAsync),
    ("Appraisal provider propagates cancellation", AppraisalProviderPropagatesCancellationAsync),
    ("Legacy checkpoint migrates to schema 2", Sync(LegacyCheckpointMigratesToSchema2)),
    ("Real scan exporter accepts valid twenty item evidence", RealScanExporterAcceptsValidTwentyItemEvidenceAsync),
    ("Real scan exporter rejects wrong capture state", RealScanExporterRejectsWrongCaptureStateAsync),
    ("Real scan exporter rejects noncontinuous sequences", RealScanExporterRejectsNoncontinuousSequencesAsync),
    ("Real scan exporter rejects screenshot hash mismatch", RealScanExporterRejectsScreenshotHashMismatchAsync),
    ("Real scan calibration selects diverse cases", RealScanCalibrationSelectsDiverseCasesAsync),
    ("Automatic core bootstrap builds accepted profile", AutomaticCoreBootstrapBuildsAcceptedProfileAsync),
    ("Calcy probe writes local evidence and report", CalcyProbeWritesEvidenceAsync),
    ("Automatic Calcy live check reaches appraisal and parses output", CalcyLiveCheckNavigatesAndParsesAsync),
    ("Calcy raw parser extracts complete observation", CalcyRawParserExtractsCompleteObservationAsync),
    ("Calcy raw parser preserves catch location", CalcyRawParserPreservesCatchLocationAsync),
    ("Calcy raw parser keeps incomplete output partial", CalcyRawParserKeepsPartialObservationAsync),
    ("Calcy raw parser detects conflicting fields", CalcyRawParserDetectsConflictingFieldsAsync),
    ("Profile driven provider preserves raw output", ProfileDrivenProviderPreservesRawOutputAsync),
    ("Verification template creates twenty cases", VerificationTemplateCreatesTwentyCasesAsync),
    ("Parsed observation evidence is ingested", ParsedObservationEvidenceIsIngestedAsync),
    ("Verification rejects evidence path traversal", VerificationRejectsEvidencePathTraversalAsync),
    ("Twenty exact observations pass provider gate", TwentyExactObservationsPassProviderGateAsync),
    ("Wrong Complete observation blocks provider gate", WrongCompleteBlocksProviderGateAsync),
    ("Safe incomplete observation is not false Complete", SafeIncompleteIsNotFalseCompleteAsync),
    ("Incorrect incomplete observation blocks recommendation", IncorrectIncompleteBlocksRecommendationAsync),
    ("Missing verification evidence is reported", MissingVerificationEvidenceIsReportedAsync),
    ("Provider selection locks verification hashes", ProviderSelectionLocksHashesAsync),
    ("Provider selection refuses rejected report", ProviderSelectionRefusesRejectedReportAsync),
    ("Image pretest accepts diverse portrait PNGs", ImagePretestAcceptsDiversePortraitPngsAsync),
    ("Image pretest detects exact duplicates", ImagePretestDetectsExactDuplicatesAsync),
    ("Image pretest rejects insufficient image count", ImagePretestRejectsInsufficientCountAsync),
    ("Image pretest rejects landscape screenshots", ImagePretestRejectsLandscapeAsync),
    ("Image pretest writes all reports", ImagePretestWritesReportsAsync),
    ("Image pretest ignores non-PNG files", ImagePretestIgnoresNonPngFilesAsync),
    ("Image pretest tolerates isolated decode failure", ImagePretestToleratesIsolatedDecodeFailureAsync),
    ("Image pretest rejects low decode rate", ImagePretestRejectsLowDecodeRateAsync),
    ("Region discovery creates grid and candidates", RegionDiscoveryCreatesGridAndCandidatesAsync),
    ("Region discovery is deterministic", RegionDiscoveryIsDeterministicAsync),
    ("Region discovery writes all reports", RegionDiscoveryWritesReportsAsync),
    ("Region discovery rejects mixed geometry", RegionDiscoveryRejectsMixedGeometryAsync),
    ("Region discovery retains isolated decode failure", RegionDiscoveryRetainsIsolatedDecodeFailureAsync),
    ("PNG encoder round-trips a decoded fixture", Sync(PngEncoderRoundTripsFixture)),
    ("Crop atlas creates crops and contact sheets", CropAtlasCreatesCropsAndSheetsAsync),
    ("Crop atlas selection is deterministic", CropAtlasSelectionIsDeterministicAsync),
    ("Crop atlas writes all reports", CropAtlasWritesAllReportsAsync),
    ("Crop atlas rejects a missing representative image", CropAtlasRejectsMissingRepresentativeAsync),
    ("Crop atlas identifies underrepresented clusters", CropAtlasIdentifiesUnderrepresentedClustersAsync),
    ("Semantic evidence creates one case per decoded image", SemanticEvidenceCreatesCasesAsync),
    ("Semantic truth template keeps fields null", SemanticTruthTemplateKeepsFieldsNullAsync),
    ("Semantic review pack excludes source screenshots", SemanticReviewPackExcludesSourceScreenshotsAsync),
    ("Semantic evidence ordering is deterministic", SemanticEvidenceOrderingIsDeterministicAsync),
    ("Semantic evidence rejects atlas path traversal", SemanticEvidenceRejectsAtlasPathTraversalAsync),
    ("Semantic evidence identifies cluster coverage gaps", SemanticEvidenceIdentifiesClusterCoverageGapsAsync),
    ("Appraisal analyzer estimates synthetic bars", Sync(AppraisalAnalyzerEstimatesSyntheticBars)),
    ("Appraisal analyzer accepts three zero IV tracks", Sync(AppraisalAnalyzerAcceptsThreeZeroIvTracks)),
    ("Appraisal analyzer auto fits shifted layout", Sync(AppraisalAnalyzerAutoFitsShiftedLayout)),
    ("Unverified appraisal profile never completes", Sync(UnverifiedAppraisalProfileNeverCompletes)),
    ("Verified appraisal profile can complete", Sync(VerifiedAppraisalProfileCanComplete)),
    ("Appraisal pretest writes review pack", AppraisalPretestWritesReviewPackAsync),
    ("Appraisal pretest keeps candidates in one cluster", AppraisalPretestKeepsCandidatesInOneClusterAsync),
    ("Appraisal pretest retains unsupported PNG diagnostics", AppraisalPretestRetainsUnsupportedPngAsync),
    ("Phone preparation writes generated profile", PhonePreparationWritesGeneratedProfileAsync),
    ("Phone preparation remains read only on non appraisal screen", PhonePreparationHandlesNonAppraisalScreenAsync),
    ("Appraisal profile rejects incomplete verification metadata", Sync(AppraisalProfileRejectsIncompleteVerificationMetadata))
    ,("Tag verification requires executor evidence", TagVerificationTests.RunAsync)
    ,("Dynamic identity separates evidence hash and instance ordinal", PokemonIdentityTests.RunHashAndInstanceAsync)
    ,("Dynamic identity ignores zero one and two mutable tags", PokemonIdentityTests.RunTagLayoutAsync)
    ,("Dynamic identity aligns lower details after tags", PokemonIdentityTests.RunAnchorAlignmentAsync)
    ,("Dynamic identity separates changed stable content", PokemonIdentityTests.RunSeparationAsync)
    ,("Dynamic identity consensus preserves duplicate instances", PokemonIdentityTests.RunConsensusAsync)
    ,("Dynamic identity consensus requires three frames", PokemonIdentityTests.RunConsensusContractAsync)
    ,("Verified sequence is bounded, checkpointed and fail-closed", VerifiedInventorySequenceTests.RunAsync)
    ,("Android sequence runtime guards remain named and fail-closed", VerifiedSequenceRuntimeTests.RunAsync)
    ,("Wrong-screen navigation and destructive confirmation authorization", WrongScreenAuthorizationTests.RunAsync)
    ,("Deterministic navigation safety trace is phase-aligned", NavigationSafetyTraceTests.RunAsync)
    ,("Cleanup proof persists partial observations and reopens SQLite", CleanupProofTests.RunAsync)
    ,("Cleanup value proof persists before appraisal and keeps review advisory", CleanupProofTests.RunValueProofAsync)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine();
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static Func<Task> Sync(Action action) =>
    () =>
    {
        action();
        return Task.CompletedTask;
    };

static void PerfectIsKept()
{
    var result = Analyze(
        P("A", "Pikachu", 500, 15, 15, 15));
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void TradeNicknameIsKept()
{
    var result = Analyze(
        P("A", "Bidoof", 100, 1, 1, 1) with { Nickname = "Trade distan" });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void OldPokemonIsKept()
{
    var result = Analyze(
        P("A", "Eevee", 300, 5, 5, 5) with { CatchDate = new DateOnly(2018, 1, 1) });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void PokemonObservationPreservesCatchLocation()
{
    var original = P("A", "Eevee", 300, 5, 5, 5) with
    {
        CatchLocation = "Holstebro, Danmark"
    };

    var json = JsonSerializer.Serialize(original);
    var roundTripped = JsonSerializer.Deserialize<PokemonObservation>(json) ??
        throw new InvalidOperationException("Round-tripped observation was null.");

    AssertEqual("Holstebro, Danmark", roundTripped.CatchLocation, "catch location");
}

static void ShadowIsReviewed()
{
    var result = Analyze(
        P("A", "Machop", 400, 5, 5, 5) with { IsShadow = true });
    AssertCategory(result, "A", DecisionCategory.Review);
}

static void InferiorDuplicateIsDeleted()
{
    var result = Analyze(
        P("A", "Machop", 900, 14, 14, 14),
        P("B", "Machop", 500, 8, 8, 8) with { SequenceNumber = 2 });
    AssertCategory(result, "A", DecisionCategory.Keep);
    AssertCategory(result, "B", DecisionCategory.Delete);
}

static void PvpCandidateIsReviewed()
{
    var result = Analyze(
        P("A", "Machop", 900, 14, 14, 14),
        P("B", "Machop", 500, 0, 15, 15) with { SequenceNumber = 2 });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static void NonExactIdentityCannotBeDeleted()
{
    var result = Analyze(
        P("A", "Rattata", 900, 14, 14, 14),
        P("B", "Rattata", 500, 8, 8, 8) with
        {
            SequenceNumber = 2,
            IdentityConfidence = IdentityConfidence.HighConfidence
        });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static void NormalAndPartyHatPikachuAreDistinctVariants()
{
    var normal = Variant(25, "Pikachu");
    var partyHat = Variant(25, "Pikachu", costumeId: "party-hat", costumeName: "Party Hat");
    AssertTrue(normal.VariantKey != partyHat.VariantKey, "normal and party-hat keys must differ");
}

static void DifferentPikachuCostumesAreDistinctVariants()
{
    var detective = Variant(25, "Pikachu", "detective-hat", "Detective Hat");
    var holiday = Variant(25, "Pikachu", "holiday", "Holiday");
    AssertTrue(detective.VariantKey != holiday.VariantKey, "costume keys must differ");
}

static void OrdinaryAndBackgroundPikachuAreDistinctVariants()
{
    var ordinary = Variant(25, "Pikachu");
    var background = Variant(
        25,
        "Pikachu",
        backgroundId: "location-card-2026",
        backgroundName: "Location Card 2026");
    AssertTrue(ordinary.VariantKey != background.VariantKey, "background key must be distinct");
}

static void NamedBackgroundsAreDistinctVariants()
{
    var sendai = Variant(25, "Pikachu", backgroundId: "sendai-2024", backgroundName: "Sendai 2024");
    var madrid = Variant(25, "Pikachu", backgroundId: "madrid-2024", backgroundName: "Madrid 2024");
    AssertTrue(sendai.VariantKey != madrid.VariantKey, "named background keys must differ");
}

static void ExactCostumeIdentityRemainsReviewProtected()
{
    var result = Analyze(P("A", "Pikachu", 500, 8, 8, 8) with
    {
        VariantIdentity = Variant(25, "Pikachu", "party-hat", "Party Hat")
    });
    AssertCategory(result, "A", DecisionCategory.Review);
}

static void ExactBackgroundIdentityRemainsKeepProtected()
{
    var result = Analyze(P("A", "Pikachu", 500, 8, 8, 8) with
    {
        VariantIdentity = Variant(
            25,
            "Pikachu",
            backgroundId: "sendai-2024",
            backgroundName: "Sendai 2024")
    });
    AssertCategory(result, "A", DecisionCategory.Keep);
}

static void UnknownCostumeIsNotConfirmedNone()
{
    var unknown = Variant(25, "Pikachu") with
    {
        CostumeId = null,
        CostumeName = null,
        VariantIdentityConfidence = IdentityConfidence.Unknown
    };
    AssertEqual((string?)null, unknown.VariantKey, "unknown costume variant key");
    AssertTrue(unknown.MissingVariantFields.Contains("CostumeId"), "costume ID must be missing");
}

static void UnknownBackgroundIsNotConfirmedNone()
{
    var unknown = Variant(25, "Pikachu") with
    {
        BackgroundId = null,
        BackgroundName = null,
        VariantIdentityConfidence = IdentityConfidence.Unknown
    };
    AssertEqual((string?)null, unknown.VariantKey, "unknown background variant key");
    AssertTrue(unknown.MissingVariantFields.Contains("BackgroundId"), "background ID must be missing");
}

static void UnknownVariantFieldsForbidDelete()
{
    var result = Analyze(
        P("A", "Pikachu", 900, 14, 14, 14),
        P("B", "Pikachu", 500, 8, 8, 8) with
        {
            SequenceNumber = 2,
            VariantIdentity = Variant(25, "Pikachu") with
            {
                CostumeId = null,
                CostumeName = null,
                VariantIdentityConfidence = IdentityConfidence.Unknown
            }
        });
    AssertCategory(result, "B", DecisionCategory.Review);
}

static void ContradictoryExactVariantMarkersAreRejected()
{
    var costume = Variant(25, "Pikachu") with { CostumeName = "Party Hat" };
    var background = Variant(25, "Pikachu") with { BackgroundName = "Sendai 2024" };

    AssertThrowsInvalidOperation(() => costume.Validate(), "contradictory costume");
    AssertThrowsInvalidOperation(() => background.Validate(), "contradictory background");
}

static void ExactSameVariantCanBeEvaluatedAsDuplicates()
{
    var result = Analyze(
        P("A", "Pikachu", 900, 14, 14, 14),
        P("B", "Pikachu", 500, 8, 8, 8) with { SequenceNumber = 2 });
    AssertCategory(result, "B", DecisionCategory.Delete);
}

static void ShinyAndNonShinyDoNotCollapse()
{
    var normal = Variant(25, "Pikachu", isShiny: false);
    var shiny = Variant(25, "Pikachu", isShiny: true);
    AssertTrue(normal.VariantKey != shiny.VariantKey, "shiny state must be part of the key");
}

static void InstanceFingerprintCannotReplaceVariantIdentity()
{
    var first = P("same-fingerprint-A", "Pikachu", 900, 14, 14, 14) with
    {
        VariantIdentity = new PokemonVariantIdentity()
    };
    var second = P("same-fingerprint-B", "Pikachu", 500, 8, 8, 8) with
    {
        SequenceNumber = 2,
        VariantIdentity = new PokemonVariantIdentity()
    };
    var result = Analyze(first, second);
    AssertCategory(result, "same-fingerprint-A", DecisionCategory.Review);
    AssertCategory(result, "same-fingerprint-B", DecisionCategory.Review);
    AssertTrue(first.GroupKey != second.GroupKey, "unknown variants must not share a duplicate group");
}

static void AdbDeviceListParserRecognisesStates()
{
    const string output = """
        List of devices attached
        ABC123 device product:foo model:Pixel_9 device:komodo transport_id:1
        DEF456 unauthorized usb:1-2 transport_id:2
        GHI789 offline transport_id:3
        """;

    var devices = AdbOutputParser.ParseDeviceList(output);
    AssertEqual(3, devices.Count, "device count");
    AssertEqual(AndroidDeviceState.Authorized, devices[0].State, "authorised state");
    AssertEqual("Pixel 9", devices[0].Model, "normalised model");
    AssertEqual(AndroidDeviceState.Unauthorized, devices[1].State, "unauthorised state");
    AssertEqual(AndroidDeviceState.Offline, devices[2].State, "offline state");
}

static void AdbMetadataParsersReadScreenAndBattery()
{
    var screen = AdbOutputParser.ParseScreenInfo("Physical size: 1080x2400\nOverride size: 720x1600");
    AssertEqual((int?)1080, screen.PhysicalWidth, "physical width");
    AssertEqual((int?)1600, screen.EffectiveHeight, "effective height");

    const string batteryOutput = """
        Current Battery Service state:
          AC powered: false
          USB powered: true
          Wireless powered: false
          status: 2
          health: 2
          present: true
          level: 85
          scale: 100
          temperature: 295
          technology: Li-ion
        """;

    var battery = AdbOutputParser.ParseBatteryInfo(batteryOutput);
    AssertEqual((int?)85, battery.LevelPercent, "battery percentage");
    AssertEqual((decimal?)29.5m, battery.TemperatureCelsius, "battery temperature");
    AssertEqual("Charging", battery.StatusName, "battery status");
    AssertEqual((bool?)true, battery.UsbPowered, "USB powered");
}

static async Task AdbTransportUsesExpectedCommandsAsync()
{
    static AdbProcessResult Text(string value) =>
        new()
        {
            ExitCode = 0,
            StandardOutput = System.Text.Encoding.UTF8.GetBytes(value),
            StandardError = string.Empty
        };

    var runner = new RecordingAdbProcessRunner(new[]
    {
        Text("List of devices attached\nABC device model:Pixel_9 transport_id:1\n"),
        Text("[ro.product.model]: [Pixel 9]\n[ro.product.manufacturer]: [Google]\n[ro.build.version.release]: [16]\n[ro.build.version.sdk]: [36]\n"),
        Text("Physical size: 1080x2400\n"),
        Text("level: 75\nscale: 100\ntemperature: 300\nstatus: 3\n"),
        new AdbProcessResult
        {
            ExitCode = 0,
            StandardOutput = FakeAndroidDeviceTransport.CreateDefaultScreenshotPng(),
            StandardError = string.Empty
        }
    });

    var options = new DeviceHarnessOptions
    {
        CommandTimeout = TimeSpan.FromSeconds(2)
    };
    var transport = new AdbAndroidDeviceTransport(runner, options);

    var devices = await transport.ListDevicesAsync();
    await transport.ReadMetadataAsync(devices.Single().Serial);
    await transport.CaptureScreenshotPngAsync(devices.Single().Serial);

    var expected = new[]
    {
        "devices -l",
        "-s ABC shell getprop",
        "-s ABC shell wm size",
        "-s ABC shell dumpsys battery",
        "-s ABC exec-out screencap -p"
    };
    var actual = runner.Commands.Select(x => string.Join(" ", x)).ToArray();

    AssertEqual(expected.Length, actual.Length, "ADB command count");
    for (var index = 0; index < expected.Length; index++)
    {
        AssertEqual(expected[index], actual[index], $"ADB command {index + 1}");
    }
}

static async Task AdbAutomationTransportUsesExpectedCommandsAsync()
{
    static AdbProcessResult Success() =>
        new()
        {
            ExitCode = 0,
            StandardOutput = Array.Empty<byte>(),
            StandardError = string.Empty
        };

    var runner = new RecordingAdbProcessRunner(new[]
    {
        Success(),
        Success()
    });
    var transport = new AdbAndroidDeviceTransport(
        runner,
        new DeviceHarnessOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(2)
        });

    await transport.TapAsync("ABC", 100, 200);
    await transport.SwipeAsync("ABC", 800, 1200, 200, 1200, 320);

    var actual = runner.Commands.Select(x => string.Join(" ", x)).ToArray();
    AssertEqual(2, actual.Length, "automation ADB command count");
    AssertEqual("-s ABC shell input tap 100 200", actual[0], "tap command");
    AssertEqual(
        "-s ABC shell input swipe 800 1200 200 1200 320",
        actual[1],
        "swipe command");
}

static async Task AdbKnownAppStopIsAllowListedAsync()
{
    var runner = new RecordingAdbProcessRunner(new[]
    {
        new AdbProcessResult
        {
            ExitCode = 0,
            StandardOutput = Array.Empty<byte>(),
            StandardError = string.Empty
        }
    });
    var transport = new AdbAndroidDeviceTransport(
        runner,
        new DeviceHarnessOptions
        {
            CommandTimeout = TimeSpan.FromSeconds(2)
        });

    await transport.StopKnownAppAsync("ABC", KnownAndroidPackage.Calcy);

    var actual = runner.Commands.Select(x => string.Join(" ", x)).Single();
    AssertEqual(
        "-s ABC shell am force-stop tesmath.calcy",
        actual,
        "allow-listed Calcy stop command");
}

static async Task RejectsNoAuthorisedDeviceAsync()
{
    var devices = new[]
    {
        FakeAndroidDeviceTransport.CreateDescriptor("A") with
        {
            State = AndroidDeviceState.Unauthorized
        }
    };

    await ExpectDeviceErrorAsync(
        DeviceErrorCode.NoAuthorizedDevice,
        () => RunSnapshotAsync(devices));
}

static async Task RejectsMultipleAuthorisedDevicesAsync()
{
    var devices = new[]
    {
        FakeAndroidDeviceTransport.CreateDescriptor("A"),
        FakeAndroidDeviceTransport.CreateDescriptor("B")
    };

    await ExpectDeviceErrorAsync(
        DeviceErrorCode.MultipleAuthorizedDevices,
        () => RunSnapshotAsync(devices));
}

static Task RequestedSerialSelectsDeviceAsync()
{
    var devices = new[]
    {
        FakeAndroidDeviceTransport.CreateDescriptor("A"),
        FakeAndroidDeviceTransport.CreateDescriptor("B")
    };

    var selected = DeviceSnapshotService.SelectDevice(devices, "B");
    AssertEqual("B", selected.Serial, "selected serial");
    return Task.CompletedTask;
}

static async Task FakeSnapshotWritesFilesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fake = FakeAndroidDeviceTransport.CreateSingleAuthorized();
        var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
        var result = await service.CaptureAsync(directory);

        AssertTrue(File.Exists(result.ScreenshotPath), "screenshot should exist");
        AssertTrue(File.Exists(result.MetadataPath), "metadata should exist");
        AssertTrue(File.Exists(result.ManifestPath), "manifest should exist");

        var screenshot = await File.ReadAllBytesAsync(result.ScreenshotPath);
        AssertTrue(
            screenshot.SequenceEqual(FakeAndroidDeviceTransport.CreateDefaultScreenshotPng()),
            "screenshot bytes should match fake PNG");

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(result.ManifestPath));
        var root = manifest.RootElement;
        AssertEqual(
            DeviceHarnessOptions.CurrentVersion,
            root.GetProperty("harnessVersion").GetString(),
            "harness version");
        AssertEqual(
            result.ScreenshotSha256,
            root.GetProperty("screenshotSha256").GetString(),
            "screenshot hash");
        AssertEqual(
            "FAKE-001",
            root.GetProperty("device").GetProperty("serial").GetString(),
            "manifest serial");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task MetadataSerialMismatchIsRejectedAsync()
{
    var device = FakeAndroidDeviceTransport.CreateDescriptor("A");
    var fake = new FakeAndroidDeviceTransport(
        new[] { device },
        new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
        {
            ["A"] = FakeAndroidDeviceTransport.CreateMetadata("WRONG")
        },
        FakeAndroidDeviceTransport.CreateDefaultScreenshotPng());
    var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
    var directory = CreateTemporaryDirectory();

    try
    {
        await ExpectDeviceErrorAsync(
            DeviceErrorCode.InvalidAdbOutput,
            () => service.CaptureAsync(directory));
        AssertEqual(0, Directory.GetFiles(directory).Length, "serial mismatch output file count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task InvalidScreenshotIsRejectedAsync()
{
    var device = FakeAndroidDeviceTransport.CreateDescriptor("A");
    var fake = new FakeAndroidDeviceTransport(
        new[] { device },
        new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
        {
            ["A"] = FakeAndroidDeviceTransport.CreateMetadata("A")
        },
        new byte[] { 1, 2, 3, 4 });
    var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
    var directory = CreateTemporaryDirectory();

    try
    {
        await ExpectDeviceErrorAsync(
            DeviceErrorCode.InvalidScreenshot,
            () => service.CaptureAsync(directory));
        AssertEqual(0, Directory.GetFiles(directory).Length, "invalid screenshot output file count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CancelledSnapshotDoesNotWriteFilesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var fake = FakeAndroidDeviceTransport.CreateSingleAuthorized();
        var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);

        try
        {
            await service.CaptureAsync(directory, cancellationToken: cancellationSource.Token);
            throw new InvalidOperationException("Expected OperationCanceledException.");
        }
        catch (OperationCanceledException)
        {
        }

        AssertEqual(0, Directory.GetFiles(directory).Length, "cancelled output file count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static void PngDecoderReadsSyntheticFixture()
{
    var image = LoadFixture("InventoryList.png");
    AssertEqual(180, image.Width, "fixture width");
    AssertEqual(360, image.Height, "fixture height");

    var region = new NormalizedRegion
    {
        X = 0.05,
        Y = 0.70,
        Width = 0.25,
        Height = 0.20
    };
    var fingerprint = FingerprintExtractor.Extract(
        image,
        region,
        FingerprintMode.Color,
        8,
        8);
    AssertEqual(8 * 8 * 3, fingerprint.Length, "color fingerprint length");
    AssertEqual(1d, FingerprintComparer.Similarity(fingerprint, fingerprint), "self similarity");
}

static void PngDecoderReconstructsPaethFilteredRow()
{
    var image = LoadFixture("PaethFilterRgba.png");
    AssertEqual(3, image.Width, "Paeth fixture width");
    AssertEqual(2, image.Height, "Paeth fixture height");
    AssertEqual(new Rgba32(10, 20, 30, 255), image.GetPixel(0, 0), "row 0 pixel 0");
    AssertEqual(new Rgba32(70, 80, 90, 255), image.GetPixel(2, 0), "row 0 pixel 2");
    AssertEqual(new Rgba32(15, 25, 35, 255), image.GetPixel(0, 1), "Paeth row pixel 0");
    AssertEqual(new Rgba32(45, 55, 65, 255), image.GetPixel(1, 1), "Paeth row pixel 1");
    AssertEqual(new Rgba32(75, 85, 95, 255), image.GetPixel(2, 1), "Paeth row pixel 2");
}

static async Task KnownScreenFixturesClassifyAsync()
{
    var profile = await LoadSyntheticProfileAsync();
    var detector = new ScreenStateDetector();
    var fixtures = new Dictionary<string, ScreenState>(StringComparer.Ordinal)
    {
        ["InventoryList.png"] = ScreenState.InventoryList,
        ["PokemonDetails.png"] = ScreenState.PokemonDetails,
        ["AppraisalOpen.png"] = ScreenState.AppraisalOpen,
        ["PokemonMenuOpen.png"] = ScreenState.PokemonMenuOpen,
        ["TagDialogOpen.png"] = ScreenState.TagDialogOpen,
        ["SearchOpen.png"] = ScreenState.SearchOpen,
        ["Loading.png"] = ScreenState.Loading,
        ["Popup.png"] = ScreenState.Popup,
        ["NetworkError.png"] = ScreenState.NetworkError
    };

    foreach (var fixture in fixtures)
    {
        var result = detector.Detect(
            LoadFixture(fixture.Key),
            profile,
            new DateTimeOffset(2026, 7, 13, 21, 0, 0, TimeSpan.Zero));
        AssertEqual(fixture.Value, result.State, $"state for {fixture.Key}");
        AssertTrue(result.Confidence >= 0.99, $"confidence for {fixture.Key}");
    }
}

static async Task IncompleteScreenReturnsUnknownAsync()
{
    var result = new ScreenStateDetector().Detect(
        LoadFixture("Incomplete.png"),
        await LoadSyntheticProfileAsync());

    AssertEqual(ScreenState.Unknown, result.State, "incomplete screen state");
    AssertTrue(
        result.Reasons.Contains("NoStatePassedRequiredAnchorsAndMinimumScore"),
        "incomplete screen reason");
}

static async Task ConflictingScreenReturnsUnknownAsync()
{
    var result = new ScreenStateDetector().Detect(
        LoadFixture("Conflict.png"),
        await LoadSyntheticProfileAsync());

    AssertEqual(ScreenState.Unknown, result.State, "conflicting screen state");
    AssertTrue(
        result.Reasons.Contains("ConflictingStateEvidence"),
        "conflicting screen reason");
    AssertTrue(
        result.States.Count(x => x.Eligible && x.Score >= 0.96) >= 2,
        "conflicting eligible state count");
}

static async Task LandscapeScreenFailsClosedAsync()
{
    var result = new ScreenStateDetector().Detect(
        LoadFixture("Landscape.png"),
        await LoadSyntheticProfileAsync());

    AssertEqual(ScreenState.Unknown, result.State, "landscape screen state");
    AssertEqual(ScreenOrientation.Landscape, result.Orientation, "landscape orientation");
    AssertTrue(
        result.Reasons.Any(x => x.StartsWith("UnsupportedOrientation:", StringComparison.Ordinal)),
        "landscape reason");
    AssertEqual(0, result.States.Count, "landscape evidence state count");
}

static async Task ConfidenceThresholdsAreDeterministicAsync()
{
    var profile = await LoadSyntheticProfileAsync();
    var image = LoadFixture("InventoryListNoisy.png");
    var detector = new ScreenStateDetector();

    var first = detector.Detect(image, profile);
    var second = detector.Detect(image, profile);

    AssertEqual(ScreenState.InventoryList, first.State, "noisy fixture state");
    AssertEqual(first.State, second.State, "repeated state");
    AssertEqual(first.Confidence, second.Confidence, "repeated confidence");
    AssertTrue(first.Confidence < 1d, "noisy confidence should be below one");
    AssertTrue(first.Confidence > profile.MinimumStateScore, "noisy confidence should pass base threshold");

    var stricter = profile with
    {
        MinimumStateScore = first.Confidence + 0.000001
    };
    var rejected = detector.Detect(image, stricter);
    AssertEqual(ScreenState.Unknown, rejected.State, "strict threshold state");
    AssertTrue(
        rejected.Reasons.Contains("NoStatePassedRequiredAnchorsAndMinimumScore"),
        "strict threshold reason");
}


static void InvalidPngIsRejected()
{
    try
    {
        _ = PngDecoder.Decode(new byte[] { 1, 2, 3, 4 });
        throw new InvalidOperationException("Expected invalid PNG rejection.");
    }
    catch (ScreenVisionException exception) when (exception.Code == VisionErrorCode.InvalidPng)
    {
    }
}

static async Task ScreenEvidenceReportWritesJsonAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var result = new ScreenStateDetector().Detect(
            LoadFixture("InventoryList.png"),
            await LoadSyntheticProfileAsync(),
            new DateTimeOffset(2026, 7, 13, 21, 30, 0, TimeSpan.Zero));
        var path = Path.Combine(directory, "evidence.json");

        await ScreenDetectionReportWriter.WriteAsync(result, path);

        AssertTrue(File.Exists(path), "screen evidence report should exist");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        AssertEqual("InventoryList", json.RootElement.GetProperty("state").GetString(), "reported state");
        AssertTrue(
            json.RootElement.GetProperty("states").GetArrayLength() >= 9,
            "reported state evidence count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationWorkspaceCreatesPrivateStructureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(
            Path.Combine(directory, "calibration"));

        AssertTrue(File.Exists(workspace.MarkerPath), "workspace marker should exist");
        AssertTrue(File.Exists(workspace.ManifestPath), "fixture manifest should exist");
        AssertTrue(File.Exists(workspace.AnchorPlanPath), "anchor plan should exist");
        AssertTrue(Directory.Exists(workspace.FixturesPath), "fixtures directory should exist");
        AssertTrue(
            Directory.Exists(Path.Combine(workspace.FixturesPath, "Unknown")),
            "Unknown negative-fixture directory should exist");

        var reopened = CalibrationWorkspace.Open(workspace.RootPath);
        AssertEqual(workspace.RootPath, reopened.RootPath, "reopened workspace path");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationIndexPreservesAndResetsApprovalsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(
            Path.Combine(directory, "calibration"));
        var target = Path.Combine(workspace.FixturesPath, "InventoryList", "one.png");
        File.Copy(RepositoryPath("data", "screen-fixtures", "InventoryList.png"), target);

        var first = await FixtureIndexer.IndexAsync(workspace);
        AssertEqual(1, first.NewFixtureCount, "new fixture count");

        var manifest = await FixtureManifestLoader.LoadAsync(workspace.ManifestPath);
        var approvedFixture = manifest.Fixtures.Single() with
        {
            SafetyReview = CompleteSafetyReview()
        };
        manifest = manifest with { Fixtures = new[] { approvedFixture } };
        await File.WriteAllTextAsync(
            workspace.ManifestPath,
            JsonSerializer.Serialize(
                manifest,
                CalibrationJson.CreateOptions(writeIndented: true)));

        var second = await FixtureIndexer.IndexAsync(workspace);
        AssertEqual(1, second.PreservedApprovalCount, "preserved approval count");

        File.Copy(
            RepositoryPath("data", "screen-fixtures", "InventoryListNoisy.png"),
            target,
            overwrite: true);
        var third = await FixtureIndexer.IndexAsync(workspace);
        AssertEqual(1, third.ChangedFixtureCount, "changed fixture count");

        var changed = await FixtureManifestLoader.LoadAsync(workspace.ManifestPath);
        AssertTrue(
            !changed.Fixtures.Single().SafetyReview.ApprovedForCalibration,
            "changed fixture approval should be reset");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static void CalibrationRejectsFixturePathTraversal()
{
    var manifest = new ScreenFixtureManifest
    {
        Name = "Unsafe",
        ProfileName = "Unsafe",
        Fixtures = new[]
        {
            new ScreenFixtureDefinition
            {
                Id = "escape",
                RelativePath = "../escape.png",
                ExpectedState = ScreenState.Unknown,
                Sha256 = new string('0', 64),
                SafetyReview = CompleteSafetyReview()
            }
        }
    };

    try
    {
        FixtureManifestLoader.Validate(manifest);
        throw new InvalidOperationException("Expected unsafe calibration path rejection.");
    }
    catch (CalibrationException exception) when (exception.Code == CalibrationErrorCode.UnsafePath)
    {
    }
}

static async Task SyntheticCalibrationProfileBuildsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var output = Path.Combine(directory, "profile.json");
        var profile = await BuildSyntheticCalibrationProfileAsync(output);

        AssertTrue(File.Exists(output), "generated profile should exist");
        AssertEqual(9, profile.States.Count, "generated state count");
        AssertEqual(18, profile.States.Sum(x => x.Anchors.Count), "generated anchor count");
        AssertEqual(
            "Synthetic generated calibration profile",
            profile.Name,
            "generated profile name");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SyntheticCalibrationAcceptancePassesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await BuildSyntheticCalibrationProfileAsync(
            Path.Combine(directory, "profile.json"));
        var manifest = await LoadSyntheticCalibrationManifestAsync();
        var report = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            RepositoryPath("data", "screen-fixtures"));

        AssertTrue(report.Accepted, "synthetic calibration should be accepted");
        AssertEqual(14, report.ApprovedFixtureCount, "approved synthetic fixture count");
        AssertEqual(0, report.FalsePositiveCount, "synthetic false positives");
        AssertEqual(0, report.MisclassificationCount, "synthetic misclassifications");
        AssertEqual(0, report.FalseNegativeCount, "synthetic false negatives");
        AssertEqual(0, report.WeakAnchorCount, "synthetic weak anchors");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationFalsePositiveFailsAcceptanceAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await BuildSyntheticCalibrationProfileAsync(
            Path.Combine(directory, "profile.json"));
        var source = await LoadSyntheticCalibrationManifestAsync();
        var inventory = source.Fixtures.Single(x => x.Id == "inventory-list") with
        {
            ExpectedState = ScreenState.Unknown
        };
        var manifest = source with
        {
            Acceptance = source.Acceptance with
            {
                MaximumWeakAnchors = 100,
                States = new[]
                {
                    new StateAcceptanceRequirement
                    {
                        State = ScreenState.Unknown,
                        MinimumApprovedFixtures = 1,
                        MinimumRecall = 1.0
                    }
                }
            },
            Fixtures = new[] { inventory }
        };

        var report = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            RepositoryPath("data", "screen-fixtures"));

        AssertTrue(!report.Accepted, "false-positive calibration should be rejected");
        AssertEqual(1, report.FalsePositiveCount, "false-positive count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationRejectsChangedApprovedFixtureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var target = Path.Combine(directory, "screen.png");
        var original = RepositoryPath("data", "screen-fixtures", "InventoryList.png");
        File.Copy(original, target);
        var hash = await CalibrationHash.Sha256Async(target);
        var manifest = new ScreenFixtureManifest
        {
            Name = "Hash test",
            ProfileName = "Hash test",
            Fixtures = new[]
            {
                new ScreenFixtureDefinition
                {
                    Id = "screen",
                    RelativePath = "screen.png",
                    ExpectedState = ScreenState.InventoryList,
                    Sha256 = hash,
                    SafetyReview = CompleteSafetyReview()
                }
            }
        };

        File.Copy(
            RepositoryPath("data", "screen-fixtures", "PokemonDetails.png"),
            target,
            overwrite: true);
        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.FixtureHashMismatch,
            () => FixtureRepository.LoadApprovedAsync(manifest, directory));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalibrationReportWritesAllFormatsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await BuildSyntheticCalibrationProfileAsync(
            Path.Combine(directory, "profile.json"));
        var manifest = await LoadSyntheticCalibrationManifestAsync();
        var report = await CalibrationAcceptanceRunner.RunAsync(
            manifest,
            profile,
            RepositoryPath("data", "screen-fixtures"));
        var output = Path.Combine(directory, "reports");

        await CalibrationReportWriter.WriteAsync(report, output);

        AssertTrue(
            File.Exists(Path.Combine(output, "calibration-acceptance.json")),
            "calibration JSON report should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "calibration-acceptance.md")),
            "calibration Markdown report should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "confusion-matrix.csv")),
            "confusion matrix should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "fixture-results.csv")),
            "fixture results should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task CaptureWorkspaceCreatesGuidedStructureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        AssertTrue(Directory.Exists(workspace.IncomingPath), "incoming directory should exist");
        AssertTrue(File.Exists(workspace.CapturePlanPath), "capture plan should exist");
        AssertTrue(
            Directory.Exists(Path.Combine(workspace.IncomingPath, ScreenState.InventoryList.ToString())),
            "incoming state directory should exist");

        var plan = await CalibrationCapturePlanLoader.LoadAsync(workspace.CapturePlanPath);
        AssertTrue(plan.Requirements.Any(x => x.State == ScreenState.Unknown), "capture plan should include Unknown negatives");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task GuidedCaptureWritesIncomingFileAndSessionAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var service = new CalibrationCaptureService(
            CreateCaptureFake("CAPTURE-A", "InventoryList.png"));

        var result = await service.CaptureAsync(
            workspace,
            plan,
            ScreenState.InventoryList);

        AssertTrue(File.Exists(result.AbsoluteImagePath), "incoming capture should exist");
        AssertTrue(File.Exists(workspace.CaptureSessionPath), "capture session should exist");
        AssertEqual(1, result.Status.UniqueCaptureCount, "unique capture count");
        AssertEqual("CAPTURE-A", result.Capture.DeviceSerial, "capture serial");
        AssertEqual(180, result.Capture.ImageWidth, "capture width");
        AssertEqual(360, result.Capture.ImageHeight, "capture height");
        AssertTrue(!result.Capture.IsDuplicate, "first capture should not be duplicate");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task DuplicateGuidedCaptureDoesNotAddCoverageAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var service = new CalibrationCaptureService(
            CreateCaptureFake("CAPTURE-A", "InventoryList.png"));

        var first = await service.CaptureAsync(workspace, plan, ScreenState.InventoryList);
        var second = await service.CaptureAsync(workspace, plan, ScreenState.InventoryList);

        AssertTrue(second.Capture.IsDuplicate, "second identical capture should be duplicate");
        AssertEqual(first.Capture.Id, second.Capture.DuplicateOfCaptureId, "duplicate source id");
        AssertEqual(1, second.Status.UniqueCaptureCount, "duplicate should not add unique coverage");
        AssertEqual(1, second.Status.DuplicateCaptureCount, "duplicate count");
        AssertEqual((ScreenState?)ScreenState.InventoryList, second.Status.NextRecommendedState, "state should still need variation");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CaptureSessionRejectsDeviceChangeAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        await new CalibrationCaptureService(CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.CaptureDeviceMismatch,
            () => new CalibrationCaptureService(CreateCaptureFake("CAPTURE-B", "InventoryListNoisy.png"))
                .CaptureAsync(workspace, plan, ScreenState.InventoryList));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CaptureSessionRejectsGeometryChangeAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        await new CalibrationCaptureService(CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.CaptureGeometryMismatch,
            () => new CalibrationCaptureService(CreateCaptureFake("CAPTURE-A", "Landscape.png"))
                .CaptureAsync(workspace, plan, ScreenState.InventoryList));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CaptureSessionRejectsChangedPlanAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        _ = await new CalibrationCaptureService(CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        var changedPlan = plan with
        {
            Requirements = plan.Requirements
                .Select(x => x.State == ScreenState.InventoryList
                    ? x with { RequiredUniqueCaptures = x.RequiredUniqueCaptures + 1 }
                    : x)
                .ToArray()
        };

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.InvalidCaptureSession,
            () => CalibrationCaptureSessionRepository.LoadOrCreateAsync(
                workspace.CaptureSessionPath,
                changedPlan));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ChangedIncomingCaptureIsRejectedAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var result = await new CalibrationCaptureService(
                CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        await File.WriteAllBytesAsync(
            result.AbsoluteImagePath,
            File.ReadAllBytes(RepositoryPath("data", "screen-fixtures", "PokemonDetails.png")));
        var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
            workspace.CaptureSessionPath,
            plan);

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.FixtureHashMismatch,
            () => CalibrationCaptureService.VerifyExistingCaptureFilesAsync(workspace, session));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CapturePromotionRequiresPrivacyReviewAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var capture = await new CalibrationCaptureService(
                CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.CaptureNotReviewable,
            () => CalibrationCapturePromotionService.PromoteAsync(
                workspace,
                plan,
                capture.Capture.Id,
                "Self-test",
                confirmedPrivateReview: false));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ReviewedCapturePromotesToFixtureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var capture = await new CalibrationCaptureService(
                CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        var promoted = await CalibrationCapturePromotionService.PromoteAsync(
            workspace,
            plan,
            capture.Capture.Id,
            "Self-test",
            confirmedPrivateReview: true);
        AssertTrue(File.Exists(promoted.FixturePath), "promoted fixture file should exist");

        var manifest = await FixtureManifestLoader.LoadAsync(workspace.ManifestPath);
        var fixture = manifest.Fixtures.Single(x => x.Id == promoted.FixtureId);
        AssertTrue(fixture.SafetyReview.IsComplete, "promoted fixture safety review should be complete");
        AssertEqual(ScreenState.InventoryList, fixture.ExpectedState, "promoted fixture state");
        AssertEqual(capture.Capture.Sha256, fixture.Sha256, "promoted fixture hash");

        var session = await CalibrationCaptureSessionRepository.LoadOrCreateAsync(
            workspace.CaptureSessionPath,
            plan);
        AssertEqual(
            promoted.FixtureId,
            session.Captures.Single().PromotedFixtureId,
            "session promoted fixture id");

        var second = await CalibrationCapturePromotionService.PromoteAsync(
            workspace,
            plan,
            capture.Capture.Id,
            "Self-test",
            confirmedPrivateReview: true);
        AssertTrue(second.AlreadyPromoted, "second promotion should be idempotent");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task PromotionRefusesUntrackedDestinationFileAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var capture = await new CalibrationCaptureService(
                CreateCaptureFake("CAPTURE-A", "InventoryList.png"))
            .CaptureAsync(workspace, plan, ScreenState.InventoryList);

        var fixtureId = $"guided-{capture.Capture.Id}";
        var untrackedPath = Path.Combine(
            workspace.FixturesPath,
            ScreenState.InventoryList.ToString(),
            $"{fixtureId}.png");
        await File.WriteAllBytesAsync(
            untrackedPath,
            File.ReadAllBytes(RepositoryPath("data", "screen-fixtures", "PokemonDetails.png")));

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.InvalidManifest,
            () => CalibrationCapturePromotionService.PromoteAsync(
                workspace,
                plan,
                capture.Capture.Id,
                "Self-test",
                confirmedPrivateReview: true));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task DuplicateCaptureCannotBePromotedAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var workspace = await CalibrationWorkspace.InitializeAsync(directory);
        var plan = TestCapturePlan();
        await WriteCapturePlanAsync(workspace, plan);
        var service = new CalibrationCaptureService(
            CreateCaptureFake("CAPTURE-A", "InventoryList.png"));
        _ = await service.CaptureAsync(workspace, plan, ScreenState.InventoryList);
        var duplicate = await service.CaptureAsync(workspace, plan, ScreenState.InventoryList);

        await ExpectCalibrationErrorAsync(
            CalibrationErrorCode.CaptureNotReviewable,
            () => CalibrationCapturePromotionService.PromoteAsync(
                workspace,
                plan,
                duplicate.Capture.Id,
                "Self-test",
                confirmedPrivateReview: true));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static void CaptureStatusPrioritisesRequiredStates()
{
    var plan = new CalibrationCapturePlan
    {
        Name = "Priority test",
        RequiredOrientation = ScreenOrientation.Portrait,
        MinimumWidth = 1,
        MinimumHeight = 1,
        Requirements = new[]
        {
            new CalibrationCaptureRequirement
            {
                State = ScreenState.Loading,
                RequiredUniqueCaptures = 1,
                Instruction = "Optional loading.",
                OptionalWhenUnavailable = true
            },
            new CalibrationCaptureRequirement
            {
                State = ScreenState.Unknown,
                RequiredUniqueCaptures = 1,
                Instruction = "Required negative."
            }
        }
    };
    var now = DateTimeOffset.UtcNow;
    var session = new CalibrationCaptureSession
    {
        Id = "priority",
        PlanName = plan.Name,
        PlanSha256 = CalibrationCapturePlanLoader.ComputeFingerprint(plan),
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    var status = CalibrationCaptureStatusBuilder.Build(plan, session);
    AssertEqual((ScreenState?)ScreenState.Unknown, status.NextRecommendedState, "required state priority");
    AssertTrue(!status.RequiredCoverageComplete, "required coverage should be incomplete");
}

static async Task CaptureStatusReportWritesFilesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var plan = TestCapturePlan();
        var now = DateTimeOffset.UtcNow;
        var session = new CalibrationCaptureSession
        {
            Id = "report",
            PlanName = plan.Name,
            PlanSha256 = CalibrationCapturePlanLoader.ComputeFingerprint(plan),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var status = CalibrationCaptureStatusBuilder.Build(plan, session);
        await CalibrationCaptureReportWriter.WriteAsync(status, directory);

        AssertTrue(File.Exists(Path.Combine(directory, "capture-status.json")), "capture status JSON should exist");
        AssertTrue(File.Exists(Path.Combine(directory, "capture-status.md")), "capture status Markdown should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task AdbAppInspectionUsesExpectedCommandsAsync()
{
    static AdbProcessResult Text(string value) =>
        new()
        {
            ExitCode = 0,
            StandardOutput = System.Text.Encoding.UTF8.GetBytes(value),
            StandardError = string.Empty
        };

    var runner = new RecordingAdbProcessRunner(new[]
    {
        Text("package dump"),
        Text("package:/data/app/tesmath.calcy/base.apk"),
        Text("4242"),
        Text("logcat"),
        Text("accessibility"),
        Text("appops"),
        Text("services")
    });
    var options = new DeviceHarnessOptions
    {
        CommandTimeout = TimeSpan.FromSeconds(2)
    };
    var transport = new AdbAndroidDeviceTransport(runner, options);

    _ = await transport.ReadPackageDumpAsync("ABC", "tesmath.calcy");
    _ = await transport.ReadPackagePathAsync("ABC", "tesmath.calcy");
    _ = await transport.ReadProcessIdAsync("ABC", "tesmath.calcy");
    _ = await transport.ReadRecentLogcatAsync("ABC", 250);
    _ = await transport.ReadAccessibilityStateAsync("ABC");
    _ = await transport.ReadAppOpsAsync("ABC", "tesmath.calcy");
    _ = await transport.ReadActivityServicesAsync("ABC", "tesmath.calcy");

    var expected = new[]
    {
        "-s ABC shell dumpsys package tesmath.calcy",
        "-s ABC shell pm path tesmath.calcy",
        "-s ABC shell pidof tesmath.calcy",
        "-s ABC logcat -d -v threadtime -t 250",
        "-s ABC shell dumpsys accessibility",
        "-s ABC shell appops get tesmath.calcy",
        "-s ABC shell dumpsys activity services tesmath.calcy"
    };
    var actual = runner.Commands.Select(command => string.Join(" ", command)).ToArray();
    AssertEqual(expected.Length, actual.Length, "app inspection command count");
    for (var index = 0; index < expected.Length; index++)
    {
        AssertEqual(expected[index], actual[index], $"app inspection command {index + 1}");
    }
}

static void CalcyPackageDumpParserReadsMetadata()
{
    var dump = File.ReadAllText(
        RepositoryPath("data", "calcy-probe", "package-dump.synthetic.txt"));
    var metadata = CalcyPackageDumpParser.Parse(
        "tesmath.calcy",
        dump,
        "package:/data/app/tesmath.calcy/base.apk");

    AssertTrue(metadata.IsInstalled, "Calcy package should be installed");
    AssertEqual("4.3.1", metadata.VersionName, "Calcy version name");
    AssertEqual((long?)403011, metadata.VersionCode, "Calcy version code");
    AssertEqual((int?)35, metadata.TargetSdk, "Calcy target SDK");
    AssertTrue(
        metadata.Activities.Contains("tesmath.calcy/.MainActivity", StringComparer.Ordinal),
        "main activity should be parsed");
    AssertTrue(
        metadata.Services.Contains("tesmath.calcy/.ScanService", StringComparer.Ordinal),
        "scan service should be parsed");
    AssertTrue(
        metadata.RequestedPermissions.Contains(
            "android.permission.SYSTEM_ALERT_WINDOW",
            StringComparer.Ordinal),
        "overlay permission should be parsed");
}

static void CalcyPackageDumpParserDetectsMissingPackage()
{
    var metadata = CalcyPackageDumpParser.Parse(
        "tesmath.calcy",
        "Unable to find package: tesmath.calcy",
        string.Empty);
    AssertTrue(!metadata.IsInstalled, "missing package should not be marked installed");
    AssertEqual((string?)null, metadata.VersionName, "missing package version");
}

static void CalcyLogcatFilterSelectsRelatedLines()
{
    var logcat = File.ReadAllText(
        RepositoryPath("data", "calcy-probe", "logcat.synthetic.txt"));
    var lines = CalcyLogcatFilter.Filter(logcat, "tesmath.calcy", "4242");
    AssertEqual(2, lines.Count, "filtered Calcy log line count");
    AssertTrue(
        lines.Any(line => line.Contains("species=Pikachu", StringComparison.Ordinal)),
        "filtered log should contain result line");
}

static async Task CalcyProbeWritesEvidenceAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var transport = CreateScriptedCalcyInspectionTransport();
        var result = await new CalcyProbeRunner(transport).RunAsync(
            directory,
            new CalcyProbeOptions());

        AssertEqual(CalcyProbeDecision.CandidateEvidenceFound, result.Report.Decision, "probe decision");
        AssertEqual("4.3.1", result.Report.Package.VersionName, "probe version");
        AssertEqual(2, result.Report.FilteredLogLineCount, "probe filtered lines");
        AssertTrue(File.Exists(result.JsonReportPath), "probe JSON report should exist");
        AssertTrue(File.Exists(result.MarkdownReportPath), "probe Markdown report should exist");
        AssertTrue(
            File.Exists(result.EvidenceFiles["package-dump.txt"]),
            "package dump evidence should exist");
        AssertTrue(
            File.Exists(result.EvidenceFiles["screen.png"]),
            "screenshot evidence should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalcyLiveCheckNavigatesAndParsesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var automationProfile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var screenProfile = await LoadSyntheticProfileAsync();
        var parserProfile = await CalcyTextParserProfileLoader.LoadAsync(
            RepositoryPath("data", "calcy-parser-profile.synthetic.json"));
        var automationTransport = CreateScriptedAutomationTransport();
        var inspectionTransport = CreateScriptedCalcyInspectionTransport("FAKE-AUTO-001");

        var result = await new CalcyLiveCheckRunner(
            automationTransport,
            inspectionTransport).RunAsync(
                directory,
                automationProfile,
                screenProfile,
                new CalcyProbeOptions(),
                parserProfile,
                settleTime: TimeSpan.Zero);

        AssertEqual(1, result.Navigation.Checkpoint.Items.Count, "live-check navigation item count");
        AssertEqual(CalcyObservationStatus.Complete, result.ParsedObservation?.Status, "live-check parsed status");
        AssertEqual("Pikachu", result.ParsedObservation?.Species, "live-check parsed species");
        AssertTrue(
            result.ParsedObservationPath is not null && File.Exists(result.ParsedObservationPath),
            "live-check parsed observation file should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CalcyRawParserExtractsCompleteObservationAsync()
{
    var profile = await CalcyTextParserProfileLoader.LoadAsync(
        RepositoryPath("data", "calcy-parser-profile.synthetic.json"));
    var raw = File.ReadAllText(
        RepositoryPath("data", "calcy-output.synthetic.txt"));
    var observation = new CalcyRawTextParser().Parse(
        profile,
        Bundle(raw),
        "SelfTestParser");

    AssertEqual(CalcyObservationStatus.Complete, observation.Status, "complete parser status");
    AssertEqual("Pikachu", observation.Species, "parsed species");
    AssertEqual((int?)501, observation.Cp, "parsed CP");
    AssertEqual((int?)15, observation.AttackIv, "parsed attack IV");
    AssertEqual((int?)14, observation.DefenseIv, "parsed defense IV");
    AssertEqual((int?)13, observation.HpIv, "parsed HP IV");
    AssertTrue(observation.RawProviderOutputSha256 is not null, "raw output hash should exist");
}

static async Task CalcyRawParserKeepsPartialObservationAsync()
{
    var profile = await CalcyTextParserProfileLoader.LoadAsync(
        RepositoryPath("data", "calcy-parser-profile.synthetic.json"));
    var observation = new CalcyRawTextParser().Parse(
        profile,
        Bundle("CalcyScan: species=Wooper cp=413"),
        "SelfTestParser");

    AssertEqual(CalcyObservationStatus.Partial, observation.Status, "partial parser status");
    AssertEqual("Wooper", observation.Species, "partial species");
    AssertEqual((int?)null, observation.AttackIv, "partial attack IV");
}

static async Task CalcyRawParserDetectsConflictingFieldsAsync()
{
    var profile = await CalcyTextParserProfileLoader.LoadAsync(
        RepositoryPath("data", "calcy-parser-profile.synthetic.json"));
    var raw = "CalcyScan: species=Pikachu cp=501 atk=15 def=14 sta=13\n" +
              "CalcyScan: species=Pikachu cp=502 atk=15 def=14 sta=13";
    var observation = new CalcyRawTextParser().Parse(
        profile,
        Bundle(raw),
        "SelfTestParser");

    AssertEqual(CalcyObservationStatus.Conflicting, observation.Status, "conflicting parser status");
    AssertEqual((int?)null, observation.Cp, "conflicting CP should remain unknown");
    AssertTrue(
        observation.Warnings.Any(warning => warning.Contains("Cp", StringComparison.Ordinal)),
        "conflict warning should name CP");
}

static async Task ProfileDrivenProviderPreservesRawOutputAsync()
{
    var profile = await CalcyTextParserProfileLoader.LoadAsync(
        RepositoryPath("data", "calcy-parser-profile.synthetic.json"));
    var raw = File.ReadAllText(
        RepositoryPath("data", "calcy-output.synthetic.txt"));
    var source = new ScriptedCalcyRawOutputSource(
        new Dictionary<int, CalcyRawOutputBundle>
        {
            [1] = Bundle(raw)
        });
    var provider = new ProfileDrivenCalcyObservationProvider(source, profile);
    var request = new CalcyObservationRequest
    {
        SequenceNumber = 1,
        DeviceSerial = "TEST",
        CapturedAtUtc = DateTimeOffset.UtcNow,
        ScreenshotPng = FakeAndroidDeviceTransport.CreateDefaultScreenshotPng(),
        ScreenshotSha256 = "test"
    };

    var observation = await provider.ObserveAsync(request);
    AssertEqual(CalcyObservationStatus.Complete, observation.Status, "profile provider status");
    AssertTrue(
        observation.RawProviderOutput?.Contains("species=Pikachu", StringComparison.Ordinal) == true,
        "profile provider should preserve raw output");
    AssertTrue(observation.RawProviderOutputSha256 is not null, "profile provider raw hash");
}

static CalcyRawOutputBundle Bundle(string raw) =>
    new()
    {
        Sources = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["logcat"] = raw
        }
    };

static ScriptedAndroidAppInspectionTransport CreateScriptedCalcyInspectionTransport(
    string serial = "CALCY-TEST")
{
    var packageName = CalcyProbeOptions.DefaultPackageName;
    return new ScriptedAndroidAppInspectionTransport(
        new[] { FakeAndroidDeviceTransport.CreateDescriptor(serial) },
        FakeAndroidDeviceTransport.CreateMetadata(serial),
        File.ReadAllBytes(RepositoryPath("data", "screen-fixtures", "AppraisalOpen.png")),
        packageDumps: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [packageName] = File.ReadAllText(
                RepositoryPath("data", "calcy-probe", "package-dump.synthetic.txt"))
        },
        packagePaths: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [packageName] = "package:/data/app/tesmath.calcy/base.apk"
        },
        processIds: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [packageName] = "4242"
        },
        appOps: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [packageName] = "SYSTEM_ALERT_WINDOW: allow"
        },
        activityServices: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [packageName] = "ServiceRecord{ tesmath.calcy/.ScanService }"
        },
        logcat: File.ReadAllText(
            RepositoryPath("data", "calcy-probe", "logcat.synthetic.txt")),
        accessibilityState: "Enabled services: tesmath.calcy/.ScanService");
}

static async Task AutomationProfileLoadsAndValidatesAsync()
{
    var profile = await AutomationProfileLoader.LoadAsync(
        RepositoryPath("data", "automation-profile.synthetic.json"));
    AssertEqual(
        "Synthetic deterministic inventory navigation",
        profile.Name,
        "automation profile name");
    AssertEqual(12000, profile.DefaultMaximumItems, "default maximum items");
}

static async Task AutomaticScanCapturesThreeItemsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var transport = CreateScriptedAutomationTransport();
        var profile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var screenProfile = await LoadSyntheticProfileAsync();
        var runner = new InventoryAutomationRunner(
            transport,
            observationProvider: new FakeCalcyObservationProvider());

        var result = await runner.RunAsync(
            directory,
            profile,
            screenProfile,
            maximumItems: 10);

        AssertEqual(AutomationRunStatus.Completed, result.Checkpoint.Status, "scan status");
        AssertEqual(
            AutomationStopReason.EndOfInventoryDetected,
            result.Checkpoint.StopReason,
            "scan stop reason");
        AssertEqual(3, result.Checkpoint.Items.Count, "captured item count");
        AssertEqual("2.0", result.Checkpoint.SchemaVersion, "checkpoint schema");
        AssertEqual(3, Directory.GetFiles(result.CaptureDirectory, "*.png").Length, "capture file count");
        AssertTrue(
            transport.Actions.Count(action => action.StartsWith("tap:", StringComparison.Ordinal)) == 3,
            "automatic setup should use exactly three allow-listed taps");
        AssertTrue(
            transport.Actions.All(action =>
                action.StartsWith("tap:", StringComparison.Ordinal) ||
                action.StartsWith("swipe:", StringComparison.Ordinal)),
            "scripted transport should record only tap and swipe actions");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task AutomaticScanRespectsMaximumItemLimitAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var transport = CreateScriptedAutomationTransport();
        var profile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var screenProfile = await LoadSyntheticProfileAsync();
        var runner = new InventoryAutomationRunner(transport);

        var result = await runner.RunAsync(
            directory,
            profile,
            screenProfile,
            maximumItems: 2);

        AssertEqual(AutomationRunStatus.Completed, result.Checkpoint.Status, "limited scan status");
        AssertEqual(
            AutomationStopReason.MaximumItemsReached,
            result.Checkpoint.StopReason,
            "limited scan stop reason");
        AssertEqual(2, result.Checkpoint.Items.Count, "limited scan item count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CompletedAutomaticCheckpointIsIdempotentAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var screenProfile = await LoadSyntheticProfileAsync();
        var firstTransport = CreateScriptedAutomationTransport();
        var first = await new InventoryAutomationRunner(firstTransport).RunAsync(
            directory,
            profile,
            screenProfile,
            maximumItems: 2);

        var secondTransport = CreateScriptedAutomationTransport();
        var second = await new InventoryAutomationRunner(secondTransport).RunAsync(
            directory,
            profile,
            screenProfile,
            maximumItems: 2);

        AssertEqual(first.Checkpoint.RunId, second.Checkpoint.RunId, "idempotent run id");
        AssertEqual(2, second.Checkpoint.Items.Count, "idempotent item count");
        AssertEqual(0, secondTransport.Actions.Count, "idempotent input action count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task AutomaticCoreBootstrapBuildsAcceptedProfileAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var automationProfile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var anchorPlan = await AnchorPlanLoader.LoadAsync(
            RepositoryPath("data", "calibration", "core-anchor-plan.synthetic.json"));
        var transport = CreateScriptedAutomationTransport();
        var result = await new CoreProfileBootstrapRunner(transport).RunAsync(
            directory,
            automationProfile,
            anchorPlan);

        AssertTrue(result.Acceptance.Accepted, "automatic core profile should be accepted");
        AssertEqual(6, result.CapturedFiles.Count, "bootstrap capture count");
        AssertEqual(4, result.Profile.States.Count, "bootstrap state count");
        AssertEqual(0, result.Acceptance.FalsePositiveCount, "bootstrap false positives");
        AssertEqual(0, result.Acceptance.MisclassificationCount, "bootstrap misclassifications");
        AssertTrue(File.Exists(result.ProfilePath), "bootstrap profile should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task FakeScanAttachesCompleteObservationsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var screenProfile = await LoadSyntheticProfileAsync();
        var result = await new InventoryAutomationRunner(
                CreateScriptedAutomationTransport(),
                observationProvider: new FakeCalcyObservationProvider())
            .RunAsync(directory, profile, screenProfile, maximumItems: 3);

        AssertTrue(
            result.Checkpoint.Items.All(item =>
                item.Observation.Status == CalcyObservationStatus.Complete),
            "all fake observations should be complete");
        AssertEqual("Pikachu", result.Checkpoint.Items[0].Observation.Species, "first species");
        AssertEqual((int?)0, result.Checkpoint.Items[1].Observation.AttackIv, "second attack IV");
        AssertTrue(
            result.Checkpoint.Items.All(item =>
                item.Observation.RawProviderOutputSha256 is { Length: 64 }),
            "all fake observations should have a raw-output hash");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task PartialObservationStaysPartialAsync()
{
    var partial = CalcyObservation.WithRawOutput(new CalcyObservation
    {
        ProviderName = "PartialProvider",
        Status = CalcyObservationStatus.Partial,
        Confidence = 0.7,
        Species = "Wooper",
        Cp = 311,
        RawProviderOutput = "species=Wooper;cp=311"
    });
    var result = await RunOneItemWithProviderAsync(
        new ScriptedCalcyObservationProvider(
            new Dictionary<int, CalcyObservation> { [1] = partial }));

    AssertEqual(CalcyObservationStatus.Partial, result.Status, "partial status");
    AssertEqual("Wooper", result.Species, "partial species");
    AssertEqual((int?)null, result.AttackIv, "unknown partial attack IV");
}

static async Task ConflictingObservationStaysConflictingAsync()
{
    var conflicting = CalcyObservation.WithRawOutput(new CalcyObservation
    {
        ProviderName = "ConflictProvider",
        Status = CalcyObservationStatus.Conflicting,
        Confidence = 0.2,
        Species = "Eevee",
        Cp = 500,
        RawProviderOutput = "species candidates=Eevee,Vaporeon",
        Warnings = new[] { "SpeciesConflict" }
    });
    var result = await RunOneItemWithProviderAsync(
        new ScriptedCalcyObservationProvider(
            new Dictionary<int, CalcyObservation> { [1] = conflicting }));

    AssertEqual(CalcyObservationStatus.Conflicting, result.Status, "conflicting status");
    AssertTrue(result.Warnings.Contains("SpeciesConflict"), "conflict warning");
}

static async Task ObservationProviderFailureIsRecordedAsync()
{
    var result = await RunOneItemWithProviderAsync(
        new ScriptedCalcyObservationProvider(
            exceptions: new Dictionary<int, Exception>
            {
                [1] = new InvalidOperationException("simulated provider failure")
            }));

    AssertEqual(CalcyObservationStatus.Failed, result.Status, "failed status");
    AssertEqual("InvalidOperationException", result.ErrorCode, "provider error code");
    AssertTrue(
        result.ErrorDetail?.Contains("simulated provider failure", StringComparison.Ordinal) == true,
        "provider failure detail");
}

static void KnownModalRequiresAllowListAndPostState()
{
    var modal = new KnownPokemonGoModal
    {
        Id = KnownPokemonGoModalId.NewMegaLevelAvailable,
        Confidence = 0.99,
        BeforeScreenshotSha256 = "before",
        DismissalAllowed = true,
        StateAfter = ScreenState.AppraisalOpen
    };
    modal.Validate();

    AssertThrowsInvalidOperation(
        () => new KnownPokemonGoModal
        {
            Id = KnownPokemonGoModalId.NewMegaLevelAvailable,
            Confidence = 0.99,
            BeforeScreenshotSha256 = "before",
            DismissalAllowed = true
        }.Validate(),
        "dismissible modal without expected state");
}

static void InventorySearchQueryIsSafelyValidated()
{
    AssertEqual(InventorySearchQuery.Unindexed,
        InventorySearchQuery.Validate(" !#AI-Indexed "),
        "trimmed authoritative queue query");
    AssertThrowsInvalidOperation(
        () => InventorySearchQuery.Validate("!#AI-Indexed\n"),
        "newline in search query");
    AssertThrowsInvalidOperation(
        () => InventorySearchQuery.Validate(new string('x', InventorySearchQuery.MaximumLength + 1)),
        "oversized search query");
}

static Task CalcyRawParserPreservesCatchLocationAsync()
{
    var profile = new CalcyTextParserProfile
    {
        Name = "Catch location self-test",
        ProviderVersion = "self-test",
        Patterns = new[]
        {
            new CalcyTextPatternDefinition
            {
                Field = CalcyTextField.CatchLocation,
                Pattern = @"caught\s+at\s+(?<value>.+)$"
            }
        }
    };

    var observation = new CalcyRawTextParser().Parse(
        profile,
        Bundle("caught at Holstebro, Denmark"),
        "SelfTestParser");

    AssertEqual("Holstebro, Denmark", observation.CatchLocation, "parsed catch location");
    AssertEqual(CalcyObservationStatus.Partial, observation.Status, "location-only parser status");
    return Task.CompletedTask;
}

static async Task AppraisalProviderPropagatesCancellationAsync()
{
    var provider = new AppraisalProfileObservationProvider(TestAppraisalProfile());
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await provider.ObserveAsync(
            new CalcyObservationRequest
            {
                SequenceNumber = 1,
                DeviceSerial = "SELFTEST",
                CapturedAtUtc = DateTimeOffset.UtcNow,
                ScreenshotPng = Array.Empty<byte>(),
                ScreenshotSha256 = new string('0', 64)
            },
            cancellation.Token);
        throw new InvalidOperationException("Expected OperationCanceledException.");
    }
    catch (OperationCanceledException)
    {
    }
}

static async Task RealScanExporterAcceptsValidTwentyItemEvidenceAsync()
{
    var fixture = await CreateRealScanExportFixtureAsync();
    try
    {
        var result = await ExportRealScanFixtureAsync(fixture);
        AssertTrue(result.Manifest.RealPhoneDemoPassed, "valid evidence export should pass");
        AssertEqual(20, result.Manifest.ActualItemCount, "exported item count");
        AssertEqual(20, result.Manifest.UniqueScreenshotCount, "unique screenshot count");
        AssertEqual(20, result.Manifest.UniqueFingerprintCount, "unique fingerprint count");
        AssertEqual(0, result.Manifest.CompleteObservations, "complete appraisal count");
        AssertEqual(0, result.Manifest.Delete, "delete recommendation count");
        AssertEqual(20, result.Manifest.Review, "review recommendation count");
        AssertTrue(File.Exists(result.ReportPath), "night report should exist");
    }
    finally
    {
        DeleteDirectory(fixture.Root);
    }
}

static async Task RealScanExporterRejectsWrongCaptureStateAsync()
{
    var fixture = await CreateRealScanExportFixtureAsync();
    try
    {
        var actions = fixture.Checkpoint.Actions.ToArray();
        var captureIndex = Array.FindIndex(
            actions,
            action => action.Kind == AutomationActionKind.CaptureEvidence);
        actions[captureIndex] = actions[captureIndex] with
        {
            StateAfter = ScreenState.PokemonDetails
        };
        await InventoryScanCheckpointRepository.SaveAsync(
            fixture.Source,
            fixture.Checkpoint with { Actions = actions });

        await ExpectInvalidOperationAsync(async () =>
            await ExportRealScanFixtureAsync(fixture));
    }
    finally
    {
        DeleteDirectory(fixture.Root);
    }
}

static async Task RealScanExporterRejectsNoncontinuousSequencesAsync()
{
    var fixture = await CreateRealScanExportFixtureAsync();
    try
    {
        var items = fixture.Checkpoint.Items.ToArray();
        items[1] = items[1] with { SequenceNumber = 1 };
        var invalid = fixture.Checkpoint with { Items = items };
        await File.WriteAllTextAsync(
            fixture.CheckpointPath,
            JsonSerializer.Serialize(
                invalid,
                AutomationJson.CreateOptions(writeIndented: true)));

        await ExpectAnyFailureAsync(async () =>
            await ExportRealScanFixtureAsync(fixture));
    }
    finally
    {
        DeleteDirectory(fixture.Root);
    }
}

static async Task RealScanExporterRejectsScreenshotHashMismatchAsync()
{
    var fixture = await CreateRealScanExportFixtureAsync();
    try
    {
        var screenshot = Path.Combine(fixture.Source, "captures", "000001.png");
        await using (var stream = new FileStream(
            screenshot,
            FileMode.Append,
            FileAccess.Write,
            FileShare.None))
        {
            await stream.WriteAsync(new byte[] { 1 });
        }
        await ExpectInvalidOperationAsync(async () =>
            await ExportRealScanFixtureAsync(fixture));
    }
    finally
    {
        DeleteDirectory(fixture.Root);
    }
}

static async Task RealScanCalibrationSelectsDiverseCasesAsync()
{
    var fixture = await CreateRealScanExportFixtureAsync();
    try
    {
        var result = await ExportRealScanFixtureAsync(fixture);
        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(result.CalibrationJsonPath));
        var cases = document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Select(item => new[]
            {
                item.GetProperty("attackIv").GetInt32(),
                item.GetProperty("defenseIv").GetInt32(),
                item.GetProperty("hpIv").GetInt32()
            })
            .ToArray();
        AssertEqual(3, cases.Length, "calibration case count");
        AssertEqual(3, cases.Select(item => string.Join('/', item)).Distinct().Count(),
            "distinct calibration IV triplets");
        var largestDistance = cases
            .SelectMany((left, index) => cases.Skip(index + 1).Select(right =>
                Math.Sqrt(left.Zip(right).Sum(pair => Math.Pow(pair.First - pair.Second, 2)))))
            .Max();
        AssertTrue(largestDistance >= 10, "calibration cases should span diverse IV values");
    }
    finally
    {
        DeleteDirectory(fixture.Root);
    }
}

static async Task<(
    string Root,
    string Source,
    string CheckpointPath,
    string ProfilePath,
    string Output,
    string Calibration,
    InventoryScanCheckpoint Checkpoint)> CreateRealScanExportFixtureAsync()
{
    var root = CreateTemporaryDirectory();
    var source = Path.Combine(root, "source");
    var captures = Path.Combine(source, "captures");
    var output = Path.Combine(root, "export");
    var calibration = Path.Combine(root, "calibration");
    var profilePath = Path.Combine(root, "appraisal-profile.json");
    Directory.CreateDirectory(captures);
    var profile = TestAppraisalProfile();
    await AppraisalProfileLoader.WriteAsync(profile, profilePath);

    var started = new DateTimeOffset(2026, 7, 19, 0, 0, 0, TimeSpan.Zero);
    var items = new List<InventoryScanItem>();
    var actions = new List<AutomationActionRecord>();
    var actionSequence = 1;
    for (var sequence = 1; sequence <= 20; sequence++)
    {
        var attack = 1 + ((sequence - 1) * 7 % 15);
        var defense = 1 + ((sequence - 1) * 11 % 15);
        var hp = 1 + ((sequence - 1) * 13 % 15);
        var image = CreateSyntheticAppraisalImage(
            profile,
            new AppraisalLayoutTransform(),
            attack,
            defense,
            hp);
        var pixels = image.RgbaBytes.ToArray();
        pixels[(sequence - 1) * 4] = (byte)sequence;
        var png = PngEncoder.Encode(new PixelImage(image.Width, image.Height, pixels));
        var relativePath = $"captures/{sequence:D6}.png";
        await File.WriteAllBytesAsync(
            Path.Combine(captures, $"{sequence:D6}.png"),
            png);
        var fingerprint = BitConverter.GetBytes(sequence * 104729);
        var timestamp = started.AddSeconds(sequence);
        items.Add(new InventoryScanItem
        {
            SequenceNumber = sequence,
            CapturedAtUtc = timestamp,
            ScreenshotFileName = relativePath,
            ScreenshotSha256 = Sha256(png),
            IdentityFingerprintBase64 = Convert.ToBase64String(fingerprint),
            IdentityFingerprintSha256 = Sha256(fingerprint),
            ScreenStateConfidence = 0.99,
            Observation = new CalcyObservation
            {
                ProviderName = "AppraisalProfile:self-test",
                ProviderVersion = profile.ProfileId,
                Status = CalcyObservationStatus.Partial,
                Confidence = 0.95,
                AttackIv = attack,
                DefenseIv = defense,
                HpIv = hp
            }
        });
        actions.Add(new AutomationActionRecord
        {
            SequenceNumber = actionSequence++,
            Kind = AutomationActionKind.CaptureEvidence,
            StartedAtUtc = timestamp.AddMilliseconds(-20),
            CompletedAtUtc = timestamp,
            StateBefore = ScreenState.AppraisalOpen,
            StateAfter = ScreenState.AppraisalOpen,
            Detail = relativePath
        });
        if (sequence < 20)
        {
            actions.Add(new AutomationActionRecord
            {
                SequenceNumber = actionSequence++,
                Kind = AutomationActionKind.SwipeNextPokemon,
                StartedAtUtc = timestamp.AddMilliseconds(10),
                CompletedAtUtc = timestamp.AddMilliseconds(30),
                StateBefore = ScreenState.AppraisalOpen,
                StateAfter = ScreenState.AppraisalOpen,
                Detail = $"item {sequence} to {sequence + 1}"
            });
        }
    }

    var completed = started.AddSeconds(21);
    var checkpoint = new InventoryScanCheckpoint
    {
        RunId = "self-test-real-scan",
        AutomationProfileName = "self-test",
        AutomationProfileSha256 = new string('a', 64),
        ScreenProfileSha256 = new string('b', 64),
        DeviceSerial = "SELFTEST",
        ScreenWidth = 400,
        ScreenHeight = 800,
        StartedAtUtc = started,
        UpdatedAtUtc = completed,
        CompletedAtUtc = completed,
        Status = AutomationRunStatus.Completed,
        StopReason = AutomationStopReason.MaximumItemsReached,
        Items = items,
        Actions = actions
    };
    var checkpointPath = await InventoryScanCheckpointRepository.SaveAsync(source, checkpoint);
    return (root, source, checkpointPath, profilePath, output, calibration, checkpoint);
}

static Task<RealScanExportResult> ExportRealScanFixtureAsync((
    string Root,
    string Source,
    string CheckpointPath,
    string ProfilePath,
    string Output,
    string Calibration,
    InventoryScanCheckpoint Checkpoint) fixture) =>
    RealScanEvidenceExporter.ExportAsync(
        fixture.CheckpointPath,
        fixture.ProfilePath,
        fixture.Output,
        fixture.Calibration,
        new RealScanExportOptions
        {
            ExpectedItems = 20,
            RequestedMaximumItems = 20,
            GenerateOverlays = false,
            CopyScreenshots = false,
            GenerateCheckpointEvidence = false
        });

static string Sha256(byte[] bytes) =>
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))
        .ToLowerInvariant();

static void LegacyCheckpointMigratesToSchema2()
{
    const string json = """
        {
          "schemaVersion": "1.0",
          "runId": "legacy-run",
          "automationProfileName": "legacy-profile",
          "automationProfileSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
          "screenProfileSha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
          "deviceSerial": "LEGACY",
          "screenWidth": 180,
          "screenHeight": 360,
          "startedAtUtc": "2026-07-14T00:00:00Z",
          "updatedAtUtc": "2026-07-14T00:00:01Z",
          "status": "Running",
          "stopReason": "None",
          "items": [
            {
              "sequenceNumber": 1,
              "capturedAtUtc": "2026-07-14T00:00:01Z",
              "screenshotFileName": "captures/000001.png",
              "screenshotSha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
              "identityFingerprintBase64": "AA==",
              "identityFingerprintSha256": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
              "screenStateConfidence": 1.0
            }
          ],
          "actions": []
        }
        """;

    var migrated = InventoryScanCheckpointRepository.DeserializeAndMigrate(json, "self-test");
    AssertEqual("2.0", migrated.SchemaVersion, "migrated schema");
    AssertEqual("1.0", migrated.MigratedFromSchemaVersion, "source schema");
    AssertEqual(
        CalcyObservationStatus.Unavailable,
        migrated.Items.Single().Observation.Status,
        "migrated observation status");
}

static async Task<CalcyObservation> RunOneItemWithProviderAsync(
    IPokemonObservationProvider provider)
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = await AutomationProfileLoader.LoadAsync(
            RepositoryPath("data", "automation-profile.synthetic.json"));
        var screenProfile = await LoadSyntheticProfileAsync();
        var result = await new InventoryAutomationRunner(
                CreateScriptedAutomationTransport(),
                observationProvider: provider)
            .RunAsync(directory, profile, screenProfile, maximumItems: 1);
        return result.Checkpoint.Items.Single().Observation;
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task ImagePretestAcceptsDiversePortraitPngsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(directory, "PokemonDetails.png", "IMG_0002.png");
        CopyPretestFixture(directory, "AppraisalOpen.png", "IMG_0003.png");

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 3 });

        AssertTrue(report.Accepted, "diverse portrait screenshot set should pass");
        AssertEqual(3, report.ImageCount, "pretest image count");
        AssertEqual(3, report.DecodedCount, "pretest decoded count");
        AssertEqual(3, report.PortraitCount, "pretest portrait count");
        AssertTrue(report.ClusterCount >= 1, "pretest should create visual clusters");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestDetectsExactDuplicatesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0002.png");

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 2 });

        AssertEqual(1, report.ExactDuplicatePairCount, "exact duplicate pair count");
        AssertEqual(1, report.DistinctFileHashCount, "distinct duplicate hash count");
        AssertTrue(!report.Accepted, "all-identical screenshot set should not pass");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestRejectsInsufficientCountAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(directory, "PokemonDetails.png", "IMG_0002.png");

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 3 });

        AssertTrue(!report.Accepted, "insufficient screenshot count should fail gate");
        AssertTrue(
            report.GateDetail.Contains("at least 3", StringComparison.Ordinal),
            "insufficient gate should explain minimum count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestRejectsLandscapeAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(directory, "Landscape.png", "IMG_0002.png");

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 2 });

        AssertTrue(!report.Accepted, "landscape screenshot should fail gate");
        AssertEqual(1, report.LandscapeCount, "landscape screenshot count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestWritesReportsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var input = Path.Combine(directory, "input");
        var output = Path.Combine(directory, "output");
        Directory.CreateDirectory(input);
        CopyPretestFixture(input, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(input, "PokemonDetails.png", "IMG_0002.png");

        var report = await ImagePretestRunner.RunAsync(
            input,
            new ImagePretestOptions { MinimumImageCount = 2 });
        await ImagePretestReportWriter.WriteAsync(report, output);

        AssertTrue(
            File.Exists(Path.Combine(output, "iphone-image-pretest.json")),
            "pretest JSON report should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "iphone-image-pretest.md")),
            "pretest Markdown report should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "iphone-images.csv")),
            "pretest image CSV should exist");
        AssertTrue(
            File.Exists(Path.Combine(output, "iphone-similarity.csv")),
            "pretest similarity CSV should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestIgnoresNonPngFilesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(directory, "PokemonDetails.png", "IMG_0002.PNG");
        await File.WriteAllTextAsync(Path.Combine(directory, "notes.txt"), "not an image");

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 2 });

        AssertEqual(2, report.ImageCount, "non-PNG files should be ignored");
        AssertTrue(report.Accepted, "two distinct PNG screenshots should pass");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestToleratesIsolatedDecodeFailureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixtures = new[]
        {
            "InventoryList.png",
            "PokemonDetails.png",
            "AppraisalOpen.png"
        };
        for (var index = 0; index < 10; index++)
        {
            CopyPretestFixture(
                directory,
                fixtures[index % fixtures.Length],
                $"IMG_{index + 1:D4}.png");
        }
        await File.WriteAllBytesAsync(
            Path.Combine(directory, "IMG_0011.png"),
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 10 });

        AssertTrue(report.Accepted, "one rejected image should not block a strong pretest set");
        AssertEqual(10, report.DecodedCount, "isolated failure decoded count");
        AssertEqual(1, report.FailedCount, "isolated failure count");
        AssertTrue(report.DecodeRate >= 0.90, "isolated failure decode rate");
        AssertTrue(
            report.GateDetail.Contains("retained in diagnostics", StringComparison.Ordinal),
            "accepted gate should mention retained rejection diagnostics");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ImagePretestRejectsLowDecodeRateAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyPretestFixture(directory, "InventoryList.png", "IMG_0001.png");
        CopyPretestFixture(directory, "PokemonDetails.png", "IMG_0002.png");
        await File.WriteAllBytesAsync(
            Path.Combine(directory, "IMG_0003.png"),
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var report = await ImagePretestRunner.RunAsync(
            directory,
            new ImagePretestOptions { MinimumImageCount = 2 });

        AssertTrue(!report.Accepted, "a low decode rate should fail the gate");
        AssertEqual(2, report.DecodedCount, "low-rate decoded count");
        AssertEqual(1, report.FailedCount, "low-rate failure count");
        AssertTrue(
            report.GateDetail.Contains("below required", StringComparison.Ordinal),
            "low-rate gate should explain decode-rate failure");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task RegionDiscoveryCreatesGridAndCandidatesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyRegionFixtures(directory);
        var report = await RegionDiscoveryRunner.RunAsync(
            directory,
            TestRegionDiscoveryOptions());

        AssertTrue(report.Accepted, "region discovery fixture set should pass");
        AssertEqual(32, report.CellCount, "region discovery cell count");
        AssertTrue(report.ClusterCount >= 2, "region discovery cluster count");
        foreach (var kind in Enum.GetValues<RegionCandidateKind>())
        {
            AssertTrue(
                report.CandidateCount(kind) >= 1,
                $"region discovery should produce {kind} candidate");
        }
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task RegionDiscoveryIsDeterministicAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyRegionFixtures(directory);
        var options = TestRegionDiscoveryOptions();
        var first = await RegionDiscoveryRunner.RunAsync(directory, options);
        var second = await RegionDiscoveryRunner.RunAsync(directory, options);

        AssertEqual(first.CellCount, second.CellCount, "deterministic cell count");
        for (var index = 0; index < first.Cells.Count; index++)
        {
            AssertEqual(
                first.Cells[index].ScreenStateScore,
                second.Cells[index].ScreenStateScore,
                $"deterministic state score {index}");
            AssertEqual(
                first.Cells[index].DynamicContentScore,
                second.Cells[index].DynamicContentScore,
                $"deterministic dynamic score {index}");
        }
        AssertEqual(
            string.Join("|", first.Candidates.Select(candidate => candidate.Id)),
            string.Join("|", second.Candidates.Select(candidate => candidate.Id)),
            "deterministic candidate ids");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task RegionDiscoveryWritesReportsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var input = Path.Combine(directory, "input");
        var output = Path.Combine(directory, "output");
        CopyRegionFixtures(input);
        var report = await RegionDiscoveryRunner.RunAsync(
            input,
            TestRegionDiscoveryOptions());
        await RegionDiscoveryReportWriter.WriteAsync(report, output);

        foreach (var fileName in new[]
        {
            "iphone-region-discovery.json",
            "iphone-region-discovery.md",
            "iphone-region-cells.csv",
            "iphone-region-candidates.csv",
            "iphone-region-image-clusters.csv"
        })
        {
            AssertTrue(
                File.Exists(Path.Combine(output, fileName)),
                $"region discovery report {fileName} should exist");
        }
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task RegionDiscoveryRejectsMixedGeometryAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyRegionFixtures(directory);
        CopyPretestFixture(directory, "Landscape.png", "IMG_0007.png");
        var report = await RegionDiscoveryRunner.RunAsync(
            directory,
            TestRegionDiscoveryOptions() with
            {
                MinimumDecodedImages = 6,
                MinimumDecodeRate = 0.80
            });

        AssertTrue(!report.Accepted, "mixed geometry should reject region discovery");
        AssertTrue(
            report.GateDetail.Contains("geometry", StringComparison.OrdinalIgnoreCase) ||
            report.GateDetail.Contains("pretest", StringComparison.OrdinalIgnoreCase),
            "mixed geometry gate should explain rejection");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task RegionDiscoveryRetainsIsolatedDecodeFailureAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        CopyRegionFixtures(directory);
        await File.WriteAllBytesAsync(
            Path.Combine(directory, "IMG_0007.png"),
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var report = await RegionDiscoveryRunner.RunAsync(
            directory,
            TestRegionDiscoveryOptions() with
            {
                MinimumDecodeRate = 0.80
            });

        AssertTrue(report.Accepted, "isolated decoder failure should not block discovery");
        AssertEqual(6, report.DecodedCount, "region discovery decoded count");
        AssertEqual(1, report.FailedCount, "region discovery failed count");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static void PngEncoderRoundTripsFixture()
{
    var source = LoadFixture("PokemonDetails.png");
    var encoded = PngEncoder.Encode(source);
    var decoded = PngDecoder.Decode(encoded);

    AssertEqual(source.Width, decoded.Width, "PNG encoder width");
    AssertEqual(source.Height, decoded.Height, "PNG encoder height");
    AssertEqual(
        source.GetPixel(source.Width / 2, source.Height / 2),
        decoded.GetPixel(decoded.Width / 2, decoded.Height / 2),
        "PNG encoder centre pixel");
}

static async Task CropAtlasCreatesCropsAndSheetsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateCropAtlasFixtureAsync(directory);
        var report = await CropAtlasRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.AtlasOutput,
            TestCropAtlasOptions());
        await CropAtlasReportWriter.WriteAsync(
            report,
            fixture.AtlasOutput);

        AssertTrue(report.Accepted, "crop atlas fixture should pass");
        AssertTrue(
            report.SelectedRegionCount >= 3,
            "crop atlas should select at least three regions");
        AssertTrue(
            report.CropCount >= report.SelectedRegionCount * report.ClusterCount,
            "crop atlas should cover every cluster");
        AssertTrue(
            File.Exists(Path.Combine(
                fixture.AtlasOutput,
                report.OverviewFile)),
            "crop atlas overview should exist");
        foreach (var region in report.SelectedRegions)
        {
            AssertTrue(
                File.Exists(Path.Combine(
                    fixture.AtlasOutput,
                    region.SheetFile.Replace(
                        '/',
                        Path.DirectorySeparatorChar))),
                $"crop atlas sheet {region.SheetFile} should exist");
        }
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CropAtlasSelectionIsDeterministicAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateCropAtlasFixtureAsync(directory);
        var firstOutput = Path.Combine(directory, "atlas-first");
        var secondOutput = Path.Combine(directory, "atlas-second");
        var first = await CropAtlasRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            firstOutput,
            TestCropAtlasOptions());
        var second = await CropAtlasRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            secondOutput,
            TestCropAtlasOptions());

        AssertEqual(
            string.Join("|", first.SelectedRegions.Select(region =>
                region.CandidateId)),
            string.Join("|", second.SelectedRegions.Select(region =>
                region.CandidateId)),
            "deterministic crop atlas region ids");
        AssertEqual(
            string.Join("|", first.Crops.Select(crop =>
                $"{crop.CandidateId}:{crop.ClusterId}:{crop.SourceFile}")),
            string.Join("|", second.Crops.Select(crop =>
                $"{crop.CandidateId}:{crop.ClusterId}:{crop.SourceFile}")),
            "deterministic crop atlas crop order");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CropAtlasWritesAllReportsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateCropAtlasFixtureAsync(directory);
        var report = await CropAtlasRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.AtlasOutput,
            TestCropAtlasOptions());
        await CropAtlasReportWriter.WriteAsync(
            report,
            fixture.AtlasOutput);

        foreach (var fileName in new[]
        {
            "iphone-crop-atlas.json",
            "iphone-crop-atlas.md",
            "iphone-crop-atlas-regions.csv",
            "iphone-crop-atlas-crops.csv",
            "iphone-crop-atlas-clusters.csv",
            "cluster-overview.png"
        })
        {
            AssertTrue(
                File.Exists(Path.Combine(fixture.AtlasOutput, fileName)),
                $"crop atlas report {fileName} should exist");
        }
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CropAtlasRejectsMissingRepresentativeAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateCropAtlasFixtureAsync(directory);
        var regionJson = await File.ReadAllTextAsync(
            fixture.RegionReportPath);
        var regionReport = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            regionJson,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "Missing test region report.");
        File.Delete(Path.Combine(
            fixture.Input,
            regionReport.Images[0].FileName));

        await ExpectFileNotFoundAsync(() => CropAtlasRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.AtlasOutput,
            TestCropAtlasOptions()));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task CropAtlasIdentifiesUnderrepresentedClustersAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateCropAtlasFixtureAsync(directory);
        var regionJson = await File.ReadAllTextAsync(
            fixture.RegionReportPath);
        var regionReport = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            regionJson,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "Missing test region report.");
        var rewrittenImages = regionReport.Images
            .Select((image, index) => image with
            {
                ClusterId = index == regionReport.Images.Count - 1
                    ? "cluster-singleton"
                    : "cluster-main"
            })
            .ToArray();
        var rewritten = regionReport with
        {
            ClusterCount = 2,
            Images = rewrittenImages
        };
        await File.WriteAllTextAsync(
            fixture.RegionReportPath,
            JsonSerializer.Serialize(
                rewritten,
                RegionDiscoveryJson.CreateOptions(writeIndented: true)));

        var report = await CropAtlasRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.AtlasOutput,
            TestCropAtlasOptions());

        AssertTrue(
            report.Readiness.NeedsMoreImages,
            "singleton cluster should request more images");
        AssertTrue(
            report.Readiness.UnderrepresentedClusters.Contains(
                "cluster-singleton",
                StringComparer.Ordinal),
            "singleton cluster id should be listed");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static CropAtlasOptions TestCropAtlasOptions() =>
    new()
    {
        MaximumCandidates = 6,
        RepresentativesPerCluster = 2,
        MaximumCropWidth = 160,
        MaximumCropHeight = 160,
        OverviewThumbnailWidth = 80,
        OverviewThumbnailHeight = 160,
        MaximumSameKindOverlap = 0.50
    };

static async Task<(
    string Input,
    string RegionReportPath,
    string AtlasOutput)> CreateCropAtlasFixtureAsync(
        string directory)
{
    var input = Path.Combine(directory, "input");
    var regionOutput = Path.Combine(directory, "region");
    var atlasOutput = Path.Combine(directory, "atlas");
    CopyRegionFixtures(input);
    var regionReport = await RegionDiscoveryRunner.RunAsync(
        input,
        TestRegionDiscoveryOptions());
    Directory.CreateDirectory(regionOutput);
    await RegionDiscoveryReportWriter.WriteAsync(
        regionReport,
        regionOutput);
    return (
        input,
        Path.Combine(regionOutput, "iphone-region-discovery.json"),
        atlasOutput);
}

static async Task ExpectFileNotFoundAsync(Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException(
            "Expected FileNotFoundException.");
    }
    catch (FileNotFoundException)
    {
    }
}


static async Task SemanticEvidenceCreatesCasesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateSemanticEvidenceFixtureAsync(directory);
        var report = await SemanticEvidenceRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.CropAtlasReportPath,
            fixture.SemanticOutput,
            TestSemanticEvidenceOptions());

        var regionJson = await File.ReadAllTextAsync(
            fixture.RegionReportPath);
        var regionReport = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            regionJson,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "Missing test region report.");

        AssertTrue(report.Accepted, "semantic evidence fixture should pass");
        AssertEqual(
            regionReport.DecodedCount,
            report.CaseCount,
            "semantic evidence case count");
        AssertEqual(
            report.CaseCount * report.SelectedRegionCount,
            report.CropCount,
            "semantic evidence crop count");
        AssertTrue(
            report.Readiness.ReadyForExternalVisualReview,
            "semantic evidence should be ready for external review");
        AssertTrue(
            !report.Readiness.ReadyForAutomatedExtraction,
            "semantic evidence must not enable automated extraction");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SemanticTruthTemplateKeepsFieldsNullAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateSemanticEvidenceFixtureAsync(directory);
        var report = await SemanticEvidenceRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.CropAtlasReportPath,
            fixture.SemanticOutput,
            TestSemanticEvidenceOptions());
        await SemanticEvidenceReportWriter.WriteAsync(
            report,
            fixture.SemanticOutput);

        using var document = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(
                fixture.SemanticOutput,
                "semantic-truth-template.json")));
        var first = document.RootElement
            .GetProperty("cases")[0];

        AssertEqual(
            JsonValueKind.Null,
            first.GetProperty("expectedScreenState").ValueKind,
            "truth screen state");
        AssertEqual(
            JsonValueKind.Null,
            first.GetProperty("expectedSpecies").ValueKind,
            "truth species");
        AssertEqual(
            JsonValueKind.Null,
            first.GetProperty("expectedCp").ValueKind,
            "truth CP");
        AssertTrue(
            !first.GetProperty("reviewed").GetBoolean(),
            "truth case should start unreviewed");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SemanticReviewPackExcludesSourceScreenshotsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateSemanticEvidenceFixtureAsync(directory);
        var report = await SemanticEvidenceRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.CropAtlasReportPath,
            fixture.SemanticOutput,
            TestSemanticEvidenceOptions());
        await SemanticEvidenceReportWriter.WriteAsync(
            report,
            fixture.SemanticOutput);

        using var archive = System.IO.Compression.ZipFile.OpenRead(
            Path.Combine(
                fixture.SemanticOutput,
                report.ReviewPackFile));
        AssertTrue(
            archive.Entries.Count > 4,
            "semantic review pack should contain evidence");
        AssertTrue(
            archive.Entries.All(entry =>
                !entry.FullName.StartsWith(
                    "data/iphone-images/",
                    StringComparison.OrdinalIgnoreCase) &&
                !entry.FullName.StartsWith(
                    "source/",
                    StringComparison.OrdinalIgnoreCase)),
            "semantic review pack must not include source screenshots");
        AssertTrue(
            archive.Entries.Any(entry =>
                entry.FullName.Equals(
                    "semantic-truth-template.json",
                    StringComparison.Ordinal)),
            "semantic review pack should contain the truth template");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SemanticEvidenceOrderingIsDeterministicAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateSemanticEvidenceFixtureAsync(directory);
        var first = await SemanticEvidenceRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.CropAtlasReportPath,
            Path.Combine(directory, "semantic-first"),
            TestSemanticEvidenceOptions());
        var second = await SemanticEvidenceRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.CropAtlasReportPath,
            Path.Combine(directory, "semantic-second"),
            TestSemanticEvidenceOptions());

        AssertEqual(
            string.Join("|", first.Cases.Select(item =>
                $"{item.SequenceNumber}:{item.CaseId}:{item.ClusterId}")),
            string.Join("|", second.Cases.Select(item =>
                $"{item.SequenceNumber}:{item.CaseId}:{item.ClusterId}")),
            "semantic evidence case order");
        AssertEqual(
            string.Join("|", first.Cases.SelectMany(item =>
                item.Crops.Select(crop =>
                    $"{item.CaseId}:{crop.Kind}:{crop.CandidateId}"))),
            string.Join("|", second.Cases.SelectMany(item =>
                item.Crops.Select(crop =>
                    $"{item.CaseId}:{crop.Kind}:{crop.CandidateId}"))),
            "semantic evidence crop order");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SemanticEvidenceRejectsAtlasPathTraversalAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateSemanticEvidenceFixtureAsync(directory);
        var json = await File.ReadAllTextAsync(
            fixture.CropAtlasReportPath);
        var atlas = JsonSerializer.Deserialize<CropAtlasReport>(
            json,
            CropAtlasJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "Missing test crop atlas report.");
        var changed = atlas with
        {
            OverviewFile = "../escape.png"
        };
        await File.WriteAllTextAsync(
            fixture.CropAtlasReportPath,
            JsonSerializer.Serialize(
                changed,
                CropAtlasJson.CreateOptions(writeIndented: true)));

        await ExpectInvalidOperationAsync(() =>
            SemanticEvidenceRunner.RunAsync(
                fixture.Input,
                fixture.RegionReportPath,
                fixture.CropAtlasReportPath,
                fixture.SemanticOutput,
                TestSemanticEvidenceOptions()));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SemanticEvidenceIdentifiesClusterCoverageGapsAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateSemanticEvidenceFixtureAsync(directory);
        var regionJson = await File.ReadAllTextAsync(
            fixture.RegionReportPath);
        var regionReport = JsonSerializer.Deserialize<RegionDiscoveryReport>(
            regionJson,
            RegionDiscoveryJson.CreateOptions(writeIndented: false))
            ?? throw new InvalidOperationException(
                "Missing test region report.");
        var rewrittenImages = regionReport.Images
            .Select((image, index) => image with
            {
                ClusterId = index == regionReport.Images.Count - 1
                    ? "cluster-singleton"
                    : "cluster-main"
            })
            .ToArray();
        var rewritten = regionReport with
        {
            ClusterCount = 2,
            Images = rewrittenImages
        };
        await File.WriteAllTextAsync(
            fixture.RegionReportPath,
            JsonSerializer.Serialize(
                rewritten,
                RegionDiscoveryJson.CreateOptions(writeIndented: true)));

        var report = await SemanticEvidenceRunner.RunAsync(
            fixture.Input,
            fixture.RegionReportPath,
            fixture.CropAtlasReportPath,
            fixture.SemanticOutput,
            TestSemanticEvidenceOptions() with
            {
                MinimumCasesPerCluster = 2
            });

        AssertTrue(report.Accepted, "coverage warning should not block pack creation");
        AssertTrue(
            report.Readiness.NeedsMoreImages,
            "singleton cluster should request more images");
        AssertTrue(
            report.Readiness.UnderrepresentedClusters.Contains(
                "cluster-singleton",
                StringComparer.Ordinal),
            "singleton cluster should be named");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static SemanticEvidenceOptions TestSemanticEvidenceOptions() =>
    new()
    {
        MinimumCaseCount = 1,
        MinimumCasesPerCluster = 1,
        MaximumCropWidth = 160,
        MaximumCropHeight = 160
    };

static async Task<(
    string Input,
    string RegionReportPath,
    string CropAtlasReportPath,
    string SemanticOutput)> CreateSemanticEvidenceFixtureAsync(
        string directory)
{
    var cropFixture = await CreateCropAtlasFixtureAsync(directory);
    var atlasReport = await CropAtlasRunner.RunAsync(
        cropFixture.Input,
        cropFixture.RegionReportPath,
        cropFixture.AtlasOutput,
        TestCropAtlasOptions());
    await CropAtlasReportWriter.WriteAsync(
        atlasReport,
        cropFixture.AtlasOutput);

    return (
        cropFixture.Input,
        cropFixture.RegionReportPath,
        Path.Combine(
            cropFixture.AtlasOutput,
            "iphone-crop-atlas.json"),
        Path.Combine(directory, "semantic"));
}


static void AppraisalAnalyzerEstimatesSyntheticBars()
{
    var profile = TestAppraisalProfile();
    var image = CreateSyntheticAppraisalImage(
        profile,
        new AppraisalLayoutTransform(),
        15,
        10,
        5);
    var result = new AppraisalAnalyzer().Analyze(image, profile);

    AssertEqual(
        AppraisalAnalysisStatus.Candidate,
        result.Status,
        "unverified appraisal status");
    AssertWithin(15, result.AttackIv, 1, "attack IV");
    AssertWithin(10, result.DefenseIv, 1, "defense IV");
    AssertWithin(5, result.HpIv, 1, "HP IV");
}

static void AppraisalAnalyzerAcceptsThreeZeroIvTracks()
{
    var profile = TestAppraisalProfile();
    var result = new AppraisalAnalyzer().Analyze(
        CreateSyntheticAppraisalImage(
            profile,
            new AppraisalLayoutTransform(),
            0,
            0,
            0),
        profile);

    AssertEqual(AppraisalAnalysisStatus.Candidate, result.Status, "zero IV appraisal status");
    AssertEqual((int?)0, result.AttackIv, "zero attack IV");
    AssertEqual((int?)0, result.DefenseIv, "zero defense IV");
    AssertEqual((int?)0, result.HpIv, "zero HP IV");
}

static void AppraisalAnalyzerAutoFitsShiftedLayout()
{
    var profile = TestAppraisalProfile();
    var expected = new AppraisalLayoutTransform
    {
        XOffset = 0.02,
        YOffset = -0.03,
        Scale = 1.05
    };
    var image = CreateSyntheticAppraisalImage(
        profile,
        expected,
        14,
        11,
        7);
    var result = new AppraisalAnalyzer().Analyze(image, profile);

    AssertTrue(result.IsAppraisal, "shifted appraisal should be detected");
    AssertNear(1.05, result.Transform.Scale, 0.0001, "fitted scale");
    AssertNear(0.02, result.Transform.XOffset, 0.0001, "fitted X offset");
    AssertNear(-0.03, result.Transform.YOffset, 0.011, "fitted Y offset");
}

static void UnverifiedAppraisalProfileNeverCompletes()
{
    var profile = TestAppraisalProfile();
    var result = new AppraisalAnalyzer().Analyze(
        CreateSyntheticAppraisalImage(
            profile,
            new AppraisalLayoutTransform(),
            15,
            15,
            15),
        profile);
    AssertEqual(
        AppraisalAnalysisStatus.Candidate,
        result.Status,
        "unverified profile status");
    AssertTrue(!result.IsComplete, "unverified profile cannot complete");
}

static void VerifiedAppraisalProfileCanComplete()
{
    var profile = TestAppraisalProfile() with
    {
        Verified = true,
        VerificationReportSha256 = new string('a', 64),
        VerifiedAtUtc = new DateTimeOffset(
            2026,
            7,
            14,
            12,
            0,
            0,
            TimeSpan.Zero)
    };
    profile.Validate();
    var result = new AppraisalAnalyzer().Analyze(
        CreateSyntheticAppraisalImage(
            profile,
            new AppraisalLayoutTransform(),
            15,
            14,
            13),
        profile,
        allowComplete: true);
    AssertEqual(
        AppraisalAnalysisStatus.Complete,
        result.Status,
        "verified profile status");
}

static async Task AppraisalPretestWritesReviewPackAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateAppraisalPretestFixtureAsync(directory);
        var report = await AppraisalPretestRunner.RunAsync(
            fixture.Input,
            fixture.ProfilePath,
            fixture.Output,
            new AppraisalPretestOptions
            {
                MinimumDecodedImages = 20,
                MinimumCandidateImages = 5,
                MinimumDominantClusterShare = 0.70
            },
            fixture.RegionReportPath);
        await AppraisalPretestReportWriter.WriteAsync(
            report,
            fixture.Output);

        AssertTrue(report.Accepted, "appraisal pretest should pass");
        AssertEqual(6, report.CandidateCount, "appraisal candidate count");
        AssertEqual(0, report.CompleteCount, "appraisal Complete count");
        AssertTrue(
            File.Exists(Path.Combine(
                fixture.Output,
                "appraisal-review-pack.zip")),
            "appraisal review pack should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task AppraisalPretestKeepsCandidatesInOneClusterAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateAppraisalPretestFixtureAsync(directory);
        var report = await AppraisalPretestRunner.RunAsync(
            fixture.Input,
            fixture.ProfilePath,
            fixture.Output,
            new AppraisalPretestOptions
            {
                MinimumDecodedImages = 20,
                MinimumCandidateImages = 5,
                MinimumDominantClusterShare = 0.70
            },
            fixture.RegionReportPath);

        AssertEqual(
            "cluster-appraisal",
            report.DominantCandidateCluster,
            "dominant appraisal cluster");
        AssertEqual(
            1d,
            report.DominantCandidateClusterShare,
            "dominant appraisal cluster share");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task AppraisalPretestRetainsUnsupportedPngAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var fixture = await CreateAppraisalPretestFixtureAsync(directory);
        var unsupportedPath = Path.Combine(
            fixture.Input,
            "IMG_9999.png");
        var unsupported = PngEncoder.Encode(
            new PixelImage(
                1,
                1,
                new byte[] { 255, 255, 255, 255 }));
        unsupported[24] = 16;
        await File.WriteAllBytesAsync(
            unsupportedPath,
            unsupported);

        var report = await AppraisalPretestRunner.RunAsync(
            fixture.Input,
            fixture.ProfilePath,
            fixture.Output,
            new AppraisalPretestOptions
            {
                MinimumDecodedImages = 20,
                MinimumCandidateImages = 5,
                MinimumDominantClusterShare = 0.70
            },
            fixture.RegionReportPath);

        AssertTrue(
            report.Accepted,
            "one unsupported PNG must not reject a strong appraisal set");
        AssertEqual(
            21,
            report.ImageCount,
            "appraisal pretest total image count");
        AssertEqual(
            20,
            report.DecodedCount,
            "appraisal pretest decoded image count");
        var rejected = report.Images.Single(item =>
            item.FileName.Equals(
                "IMG_9999.png",
                StringComparison.Ordinal));
        AssertTrue(
            !rejected.Decoded,
            "unsupported PNG should remain a rejected diagnostic");
        AssertEqual(
            "ScreenVisionException",
            rejected.ErrorCode,
            "unsupported PNG error type");
        AssertTrue(
            rejected.ErrorDetail?.Contains(
                "Only 8-bit PNG",
                StringComparison.Ordinal) == true,
            "unsupported PNG error detail");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task PhonePreparationWritesGeneratedProfileAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = TestAppraisalProfile();
        var image = CreateSyntheticAppraisalImage(
            profile,
            new AppraisalLayoutTransform
            {
                XOffset = 0.02,
                YOffset = -0.03,
                Scale = 1.05
            },
            15,
            12,
            9);
        var screenshot = PngEncoder.Encode(image);
        var device = FakeAndroidDeviceTransport.CreateDescriptor("PHONE-001");
        var metadata = FakeAndroidDeviceTransport.CreateMetadata(device.Serial) with
        {
            Screen = new AndroidScreenInfo
            {
                PhysicalWidth = image.Width,
                PhysicalHeight = image.Height
            }
        };
        var transport = new FakeAndroidDeviceTransport(
            new[] { device },
            new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
            {
                [device.Serial] = metadata
            },
            screenshot);
        var report = await new PhonePreparationRunner(transport).RunAsync(
            directory,
            profile);

        AssertTrue(report.AdbReady, "phone ADB readiness");
        AssertTrue(
            report.AppraisalCalibrationReady,
            "phone appraisal calibration readiness");
        AssertTrue(
            !report.AutomaticNavigationReady,
            "phone automatic navigation must remain false");
        AssertTrue(
            report.GeneratedProfileFile is not null &&
            File.Exists(Path.Combine(
                directory,
                report.GeneratedProfileFile)),
            "generated phone profile should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task PhonePreparationHandlesNonAppraisalScreenAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var profile = TestAppraisalProfile();
        var blank = new PixelImage(
            400,
            800,
            Enumerable.Repeat((byte)255, 400 * 800 * 4).ToArray());
        var device = FakeAndroidDeviceTransport.CreateDescriptor("PHONE-002");
        var metadata = FakeAndroidDeviceTransport.CreateMetadata(device.Serial) with
        {
            Screen = new AndroidScreenInfo
            {
                PhysicalWidth = blank.Width,
                PhysicalHeight = blank.Height
            }
        };
        var transport = new FakeAndroidDeviceTransport(
            new[] { device },
            new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
            {
                [device.Serial] = metadata
            },
            PngEncoder.Encode(blank));
        var report = await new PhonePreparationRunner(transport).RunAsync(
            directory,
            profile);

        AssertTrue(report.AdbReady, "blank-screen ADB readiness");
        AssertTrue(
            !report.AppraisalCalibrationReady,
            "blank screen should not calibrate appraisal");
        AssertTrue(
            report.GeneratedProfileFile is null,
            "blank screen should not generate profile");
        AssertTrue(
            report.NextActions.Any(action =>
                action.Contains(
                    "appraisal screen",
                    StringComparison.OrdinalIgnoreCase)),
            "blank screen should request manual appraisal navigation");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static void AppraisalProfileRejectsIncompleteVerificationMetadata()
{
    var profile = TestAppraisalProfile() with
    {
        Verified = true,
        VerificationReportSha256 = null,
        VerifiedAtUtc = null
    };

    try
    {
        profile.Validate();
        throw new InvalidOperationException(
            "Expected appraisal profile validation failure.");
    }
    catch (InvalidOperationException exception) when (
        exception.Message !=
        "Expected appraisal profile validation failure.")
    {
    }
}

static AppraisalVisualProfile TestAppraisalProfile() =>
    new()
    {
        ProfileId = "appraisal-self-test",
        ReferenceAspectRatio = 0.5,
        Bars = new[]
        {
            new AppraisalBarDefinition
            {
                Kind = AppraisalBarKind.Attack,
                Region = new NormalizedRegion
                {
                    X = 0.10,
                    Y = 0.65,
                    Width = 0.50,
                    Height = 0.025
                }
            },
            new AppraisalBarDefinition
            {
                Kind = AppraisalBarKind.Defense,
                Region = new NormalizedRegion
                {
                    X = 0.10,
                    Y = 0.72,
                    Width = 0.50,
                    Height = 0.025
                }
            },
            new AppraisalBarDefinition
            {
                Kind = AppraisalBarKind.Hp,
                Region = new NormalizedRegion
                {
                    X = 0.10,
                    Y = 0.79,
                    Width = 0.50,
                    Height = 0.025
                }
            }
        },
        SearchXOffsets = new[]
        {
            -0.04,
            -0.03,
            -0.02,
            -0.01,
            0d,
            0.01,
            0.02,
            0.03,
            0.04
        },
        SearchYOffsets = new[]
        {
            -0.05,
            -0.04,
            -0.03,
            -0.02,
            -0.01,
            0d,
            0.01,
            0.02,
            0.03,
            0.04,
            0.05
        },
        SearchScales = new[] { 0.95, 1d, 1.05 },
        Colors = new AppraisalColorProfile
        {
            OrangeRedMinimum = 180,
            OrangeGreenMinimum = 55,
            OrangeGreenMaximum = 220,
            OrangeBlueMaximum = 175,
            OrangeRedGreenDeltaMinimum = 20,
            OrangeGreenBlueDeltaMinimum = 15,
            TrackChannelMinimum = 125,
            TrackChannelMaximum = 245,
            TrackMaximumChannelSpread = 34,
            MinimumColumnCoverage = 0.12,
            MinimumTrackWidthFraction = 0.42,
            MaximumTrackGapFraction = 0.08
        },
        CandidateScoreMinimum = 0.82,
        MinimumOrangeBars = 2,
        MinimumTrackBars = 2,
        CompleteBarConfidenceMinimum = 0.72,
        Verified = false
    };

static PixelImage CreateSyntheticAppraisalImage(
    AppraisalVisualProfile profile,
    AppraisalLayoutTransform transform,
    int attack,
    int defense,
    int hp)
{
    const int width = 400;
    const int height = 800;
    var rgba = new byte[width * height * 4];
    for (var index = 0; index < rgba.Length; index += 4)
    {
        rgba[index] = 248;
        rgba[index + 1] = 248;
        rgba[index + 2] = 248;
        rgba[index + 3] = 255;
    }

    var values = new Dictionary<AppraisalBarKind, int>
    {
        [AppraisalBarKind.Attack] = attack,
        [AppraisalBarKind.Defense] = defense,
        [AppraisalBarKind.Hp] = hp
    };

    foreach (var definition in profile.Bars)
    {
        var region = AppraisalAnalyzer.TransformRegion(
            definition.Region,
            transform);
        var rectangle = region.ToPixels(width, height);
        var trackLeft = rectangle.X +
            Math.Max(1, (int)Math.Round(rectangle.Width * 0.08));
        var trackRight = rectangle.X + rectangle.Width -
            Math.Max(2, (int)Math.Round(rectangle.Width * 0.08));
        var trackTop = rectangle.Y +
            Math.Max(1, rectangle.Height / 4);
        var trackBottom = rectangle.Y + rectangle.Height -
            Math.Max(2, rectangle.Height / 4);
        var trackWidth = trackRight - trackLeft + 1;
        var fillWidth = (int)Math.Round(
            trackWidth *
            values[definition.Kind] /
            15d,
            MidpointRounding.AwayFromZero);

        for (var y = trackTop; y <= trackBottom; y++)
        {
            for (var x = trackLeft; x <= trackRight; x++)
            {
                var filled = x < trackLeft + fillWidth;
                SetSyntheticPixel(
                    rgba,
                    width,
                    height,
                    x,
                    y,
                    filled ? (byte)240 : (byte)205,
                    filled ? (byte)145 : (byte)205,
                    filled ? (byte)45 : (byte)205,
                    255);
            }
        }

        foreach (var fraction in new[] { 1d / 3d, 2d / 3d })
        {
            var separator = trackLeft +
                (int)Math.Round(trackWidth * fraction);
            for (var y = trackTop; y <= trackBottom; y++)
            {
                SetSyntheticPixel(
                    rgba,
                    width,
                    height,
                    separator,
                    y,
                    252,
                    252,
                    252,
                    255);
            }
        }
    }

    return new PixelImage(width, height, rgba);
}

static void SetSyntheticPixel(
    byte[] rgba,
    int width,
    int height,
    int x,
    int y,
    byte red,
    byte green,
    byte blue,
    byte alpha)
{
    if ((uint)x >= (uint)width ||
        (uint)y >= (uint)height)
    {
        return;
    }

    var index = (y * width + x) * 4;
    rgba[index] = red;
    rgba[index + 1] = green;
    rgba[index + 2] = blue;
    rgba[index + 3] = alpha;
}

static async Task<(
    string Input,
    string ProfilePath,
    string Output,
    string RegionReportPath)> CreateAppraisalPretestFixtureAsync(
        string directory)
{
    var input = Path.Combine(directory, "input");
    var output = Path.Combine(directory, "output");
    var regionOutput = Path.Combine(directory, "region");
    var profilePath = Path.Combine(directory, "profile.json");
    Directory.CreateDirectory(input);

    var profile = TestAppraisalProfile();
    await AppraisalProfileLoader.WriteAsync(
        profile,
        profilePath);

    for (var index = 0; index < 20; index++)
    {
        var image = index < 6
            ? CreateSyntheticAppraisalImage(
                profile,
                new AppraisalLayoutTransform(),
                15 - index % 3,
                12 - index % 2,
                8 + index % 4)
            : CreateSyntheticNoiseImage(
                400,
                800,
                index + 500);
        await File.WriteAllBytesAsync(
            Path.Combine(input, $"IMG_{index:0000}.png"),
            PngEncoder.Encode(image));
    }

    var discovery = await RegionDiscoveryRunner.RunAsync(
        input,
        new RegionDiscoveryOptions
        {
            MinimumDecodedImages = 20,
            MinimumDecodeRate = 1,
            NearDuplicateThreshold = 0.99999,
            ClusterThreshold = 0.90,
            GridColumns = 8,
            GridRows = 16,
            MaximumCandidatesPerKind = 4
        });
    var adjustedImages = discovery.Images
        .Select((item, index) => item with
        {
            ClusterId = index < 6
                ? "cluster-appraisal"
                : "cluster-other"
        })
        .ToArray();
    var adjusted = discovery with
    {
        ClusterCount = 2,
        Images = adjustedImages
    };
    Directory.CreateDirectory(regionOutput);
    await File.WriteAllTextAsync(
        Path.Combine(
            regionOutput,
            "iphone-region-discovery.json"),
        JsonSerializer.Serialize(
            adjusted,
            RegionDiscoveryJson.CreateOptions(writeIndented: true)));

    return (
        input,
        profilePath,
        output,
        Path.Combine(
            regionOutput,
            "iphone-region-discovery.json"));
}


static PixelImage CreateSyntheticNoiseImage(
    int width,
    int height,
    int seed)
{
    var random = new Random(seed);
    var rgba = new byte[checked(width * height * 4)];
    for (var index = 0; index < rgba.Length; index += 4)
    {
        var baseValue = (byte)random.Next(20, 170);
        rgba[index] = baseValue;
        rgba[index + 1] = (byte)Math.Clamp(
            baseValue + random.Next(-12, 13),
            0,
            255);
        rgba[index + 2] = (byte)Math.Clamp(
            baseValue + random.Next(-12, 13),
            0,
            255);
        rgba[index + 3] = 255;
    }

    return new PixelImage(width, height, rgba);
}

static void AssertNear(
    double expected,
    double actual,
    double tolerance,
    string description)
{
    if (Math.Abs(actual - expected) > tolerance)
    {
        throw new InvalidOperationException(
            $"Expected {description} within {tolerance} of {expected}, got {actual}.");
    }
}

static void AssertWithin(
    int expected,
    int? actual,
    int tolerance,
    string description)
{
    if (actual is null ||
        Math.Abs(actual.Value - expected) > tolerance)
    {
        throw new InvalidOperationException(
            $"Expected {description} within {tolerance} of {expected}, got " +
            $"{actual?.ToString(CultureInfo.InvariantCulture) ?? "null"}.");
    }
}

static RegionDiscoveryOptions TestRegionDiscoveryOptions() =>
    new()
    {
        MinimumDecodedImages = 6,
        MinimumDecodeRate = 0.90,
        ClusterThreshold = 0.970,
        GridColumns = 4,
        GridRows = 8,
        CellFingerprintWidth = 4,
        CellFingerprintHeight = 4,
        MaximumCandidatesPerKind = 3,
        MinimumCandidateScore = 0.01
    };

static void CopyRegionFixtures(string directory)
{
    var fixtures = new[]
    {
        "InventoryList.png",
        "PokemonDetails.png",
        "PokemonMenuOpen.png",
        "AppraisalOpen.png",
        "SearchOpen.png",
        "TagDialogOpen.png"
    };
    for (var index = 0; index < fixtures.Length; index++)
    {
        CopyPretestFixture(
            directory,
            fixtures[index],
            $"IMG_{index + 1:D4}.png");
    }
}

static void CopyPretestFixture(
    string targetDirectory,
    string sourceFixture,
    string targetFileName)
{
    Directory.CreateDirectory(targetDirectory);
    File.Copy(
        RepositoryPath("data", "screen-fixtures", sourceFixture),
        Path.Combine(targetDirectory, targetFileName),
        overwrite: true);
}

static ScriptedAndroidAutomationTransport CreateScriptedAutomationTransport()
{
    var screens = new Dictionary<ScreenState, byte[]>
    {
        [ScreenState.InventoryList] = File.ReadAllBytes(
            RepositoryPath("data", "screen-fixtures", "InventoryList.png")),
        [ScreenState.PokemonDetails] = File.ReadAllBytes(
            RepositoryPath("data", "screen-fixtures", "PokemonDetails.png")),
        [ScreenState.PokemonMenuOpen] = File.ReadAllBytes(
            RepositoryPath("data", "screen-fixtures", "PokemonMenuOpen.png"))
    };
    var appraisal = new[]
    {
        "AppraisalOpen.png",
        "AppraisalOpenItem2.png",
        "AppraisalOpenItem3.png"
    }.Select(name => File.ReadAllBytes(
        RepositoryPath("data", "screen-fixtures", name))).ToArray();
    return new ScriptedAndroidAutomationTransport(screens, appraisal);
}

static CalibrationCapturePlan TestCapturePlan() =>
    new()
    {
        Name = "Self-test capture plan",
        RequiredOrientation = ScreenOrientation.Portrait,
        MinimumWidth = 100,
        MinimumHeight = 200,
        LockDeviceSerial = true,
        LockExactGeometry = true,
        Requirements = new[]
        {
            new CalibrationCaptureRequirement
            {
                State = ScreenState.InventoryList,
                RequiredUniqueCaptures = 2,
                Instruction = "Open inventory list."
            },
            new CalibrationCaptureRequirement
            {
                State = ScreenState.Unknown,
                RequiredUniqueCaptures = 1,
                Instruction = "Capture a negative screen."
            }
        }
    };

static async Task WriteCapturePlanAsync(
    CalibrationWorkspace workspace,
    CalibrationCapturePlan plan)
{
    await File.WriteAllTextAsync(
        workspace.CapturePlanPath,
        JsonSerializer.Serialize(
            plan,
            CalibrationJson.CreateOptions(writeIndented: true)));
}

static FakeAndroidDeviceTransport CreateCaptureFake(
    string serial,
    string fixtureName)
{
    var device = FakeAndroidDeviceTransport.CreateDescriptor(serial);
    var metadata = FakeAndroidDeviceTransport.CreateMetadata(serial);
    return new FakeAndroidDeviceTransport(
        new[] { device },
        new Dictionary<string, AndroidDeviceMetadata>(StringComparer.Ordinal)
        {
            [serial] = metadata
        },
        File.ReadAllBytes(RepositoryPath("data", "screen-fixtures", fixtureName)));
}

static FixtureSafetyReview CompleteSafetyReview() =>
    new()
    {
        AccountIdentitySafe = true,
        LocationSafe = true,
        NotificationsSafe = true,
        OtherPersonalDataSafe = true,
        ApprovedForCalibration = true,
        ReviewedBy = "Self-test",
        ReviewedAtUtc = new DateTimeOffset(2026, 7, 13, 20, 30, 0, TimeSpan.Zero)
    };

static async Task<ScreenDetectionProfile> BuildSyntheticCalibrationProfileAsync(
    string outputPath)
{
    var manifest = await LoadSyntheticCalibrationManifestAsync();
    var plan = await AnchorPlanLoader.LoadAsync(
        RepositoryPath("data", "calibration", "anchor-plan.synthetic.json"));
    return await CalibrationProfileBuilder.BuildAsync(
        manifest,
        plan,
        RepositoryPath("data", "screen-fixtures"),
        outputPath);
}

static Task<ScreenFixtureManifest> LoadSyntheticCalibrationManifestAsync() =>
    FixtureManifestLoader.LoadAsync(
        RepositoryPath("data", "calibration", "fixture-manifest.synthetic.json"));

static async Task ExpectCalibrationErrorAsync(
    CalibrationErrorCode expectedCode,
    Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected CalibrationException {expectedCode}.");
    }
    catch (CalibrationException exception) when (exception.Code == expectedCode)
    {
    }
}

static PixelImage LoadFixture(string name)
{
    var path = RepositoryPath("data", "screen-fixtures", name);
    return PngDecoder.Decode(File.ReadAllBytes(path));
}

static Task<ScreenDetectionProfile> LoadSyntheticProfileAsync() =>
    ScreenProfileLoader.LoadAsync(
        RepositoryPath("data", "screen-profile.synthetic.json"));

static string RepositoryPath(params string[] parts)
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null &&
           !File.Exists(Path.Combine(directory.FullName, "PogoInventoryAssistant.sln")))
    {
        directory = directory.Parent;
    }

    if (directory is null)
    {
        throw new InvalidOperationException("Repository root could not be located.");
    }

    return parts.Aggregate(directory.FullName, Path.Combine);
}

static async Task RunSnapshotAsync(IReadOnlyList<AndroidDeviceDescriptor> devices)
{
    var metadata = devices.ToDictionary(
        x => x.Serial,
        x => FakeAndroidDeviceTransport.CreateMetadata(x.Serial),
        StringComparer.Ordinal);
    var fake = new FakeAndroidDeviceTransport(
        devices,
        metadata,
        FakeAndroidDeviceTransport.CreateDefaultScreenshotPng());
    var service = new DeviceSnapshotService(fake, DeviceHarnessOptions.CurrentVersion);
    var directory = CreateTemporaryDirectory();

    try
    {
        await service.CaptureAsync(directory);
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ExpectDeviceErrorAsync(
    DeviceErrorCode expectedCode,
    Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected DeviceHarnessException {expectedCode}.");
    }
    catch (DeviceHarnessException exception) when (exception.Code == expectedCode)
    {
    }
}

static InventoryAnalysisResult Analyze(params PokemonObservation[] observations) =>
    new InventoryAnalyzer().Analyze(observations, new RulePolicy());


static async Task VerificationTemplateCreatesTwentyCasesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var path = await CalcyVerificationTemplateWriter.InitializeAsync(directory, 20);
        var json = await File.ReadAllTextAsync(path);
        var manifest = JsonSerializer.Deserialize<CalcyVerificationManifest>(
            json,
            VerificationJson.CreateOptions()) ?? throw new InvalidOperationException("Missing template manifest.");
        AssertEqual(20, manifest.Cases.Count, "verification template case count");
        AssertTrue(File.Exists(Path.Combine(directory, "README.md")), "verification README should exist");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}


static async Task ParsedObservationEvidenceIsIngestedAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var observation = CalcyObservation.WithRawOutput(new CalcyObservation
        {
            ProviderName = "Self-test",
            ProviderVersion = "1",
            Status = CalcyObservationStatus.Complete,
            Confidence = 1,
            Species = "Pikachu",
            PokedexNumber = 25,
            Cp = 501,
            AttackIv = 15,
            DefenseIv = 14,
            HpIv = 13,
            RawProviderOutput = "self-test"
        });
        var observationPath = Path.Combine(directory, "observation.json");
        await File.WriteAllTextAsync(
            observationPath,
            JsonSerializer.Serialize(
                observation,
                VerificationJson.CreateOptions(writeIndented: true)));
        var manifest = new CalcyVerificationManifest
        {
            Name = "Parsed observation test",
            Mechanism = CalcyProviderMechanism.LocalText,
            ProviderVersion = "1",
            MinimumCases = 1,
            MinimumExactCompleteRate = 1,
            Cases = new[]
            {
                new CalcyVerificationCase
                {
                    Id = "001",
                    ObservationPath = "observation.json",
                    Expected = new ExpectedPokemonObservation
                    {
                        Species = "Pikachu",
                        PokedexNumber = 25,
                        Cp = 501,
                        AttackIv = 15,
                        DefenseIv = 14,
                        HpIv = 13
                    }
                }
            }
        };
        var manifestPath = Path.Combine(directory, "manifest.json");
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(
                manifest,
                VerificationJson.CreateOptions(writeIndented: true)));
        var report = await new CalcyVerificationRunner().RunAsync(
            manifestPath,
            directory,
            Path.Combine(directory, "out"));
        AssertEqual(1, report.ExactCompleteCount, "parsed observation exact count");
        AssertTrue(report.RecommendedForLongScan, "parsed observation should pass one-case test gate");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task VerificationRejectsEvidencePathTraversalAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var manifest = VerificationManifest(
            directory,
            Enumerable.Range(1, 20).Select(index => VerificationCase(index, "../outside.txt")).ToArray());
        var profile = await LoadSyntheticCalcyParserProfileAsync();
        await ExpectInvalidOperationAsync(() => new CalcyVerificationRunner().RunAsync(
            manifest,
            directory,
            Path.Combine(directory, "out"),
            profile));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task TwentyExactObservationsPassProviderGateAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var (manifest, profile) = await CreateVerificationFixtureAsync(directory, wrongCase: null, partialCase: null);
        var report = await new CalcyVerificationRunner().RunAsync(
            manifest,
            directory,
            Path.Combine(directory, "out"),
            profile);
        AssertEqual(20, report.ExactCompleteCount, "exact verification count");
        AssertEqual(0, report.WrongCompleteCount, "wrong Complete count");
        AssertTrue(report.RecommendedForLongScan, "provider should pass long-scan gate");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task WrongCompleteBlocksProviderGateAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var (manifest, profile) = await CreateVerificationFixtureAsync(directory, wrongCase: 7, partialCase: null);
        var report = await new CalcyVerificationRunner().RunAsync(
            manifest,
            directory,
            Path.Combine(directory, "out"),
            profile);
        AssertEqual(1, report.WrongCompleteCount, "wrong Complete count");
        AssertTrue(!report.ZeroFalseComplete, "zero false Complete flag");
        AssertTrue(!report.RecommendedForLongScan, "wrong Complete must block provider");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task SafeIncompleteIsNotFalseCompleteAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var (manifest, profile) = await CreateVerificationFixtureAsync(directory, wrongCase: null, partialCase: 5);
        var report = await new CalcyVerificationRunner().RunAsync(
            manifest,
            directory,
            Path.Combine(directory, "out"),
            profile);
        AssertEqual(1, report.SafeIncompleteCount, "safe incomplete count");
        AssertEqual(0, report.WrongCompleteCount, "wrong Complete count");
        AssertTrue(report.ZeroFalseComplete, "partial result is not false Complete");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task IncorrectIncompleteBlocksRecommendationAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var cases = new List<CalcyVerificationCase>();
        for (var index = 1; index <= 20; index++)
        {
            var id = index.ToString("000", CultureInfo.InvariantCulture);
            var path = $"cases/{id}.txt";
            Directory.CreateDirectory(Path.Combine(directory, "cases"));
            var raw = index == 6
                ? "species=Pikachu dex=25 cp=999 atk=15"
                : $"species=Pikachu dex=25 cp={500 + index} atk=15 def=14 sta=13";
            await File.WriteAllTextAsync(Path.Combine(directory, path.Replace('/', Path.DirectorySeparatorChar)), raw);
            cases.Add(new CalcyVerificationCase
            {
                Id = id,
                Sources = new Dictionary<string, string> { ["logcat"] = path },
                Expected = Expected(index)
            });
        }
        var manifest = VerificationManifest(directory, cases.ToArray());
        var report = await new CalcyVerificationRunner().RunAsync(
            manifest,
            directory,
            Path.Combine(directory, "out"),
            await LoadSyntheticCalcyParserProfileAsync());
        AssertEqual(1, report.IncorrectIncompleteCount, "incorrect incomplete count");
        AssertTrue(!report.RecommendedForLongScan, "incorrect incomplete must block recommendation");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task MissingVerificationEvidenceIsReportedAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var cases = Enumerable.Range(1, 20)
            .Select(index => VerificationCase(index, $"cases/{index:000}.txt"))
            .ToArray();
        var manifest = VerificationManifest(directory, cases);
        var report = await new CalcyVerificationRunner().RunAsync(
            manifest,
            directory,
            Path.Combine(directory, "out"),
            await LoadSyntheticCalcyParserProfileAsync());
        AssertEqual(20, report.InvalidEvidenceCount, "invalid evidence count");
        AssertTrue(!report.SafeForLongScan, "missing evidence must block safe gate");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ProviderSelectionLocksHashesAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var (manifest, profile) = await CreateVerificationFixtureAsync(directory, wrongCase: null, partialCase: null);
        var output = Path.Combine(directory, "out");
        _ = await new CalcyVerificationRunner().RunAsync(manifest, directory, output, profile);
        var reportPath = Path.Combine(output, "verification-report.json");
        var parserPath = RepositoryPath("data", "calcy-parser-profile.synthetic.json");
        var selectionPath = Path.Combine(directory, "provider-selection.json");
        var selection = await CalcyProviderSelectionService.SelectAsync(
            reportPath,
            CalcyProviderMechanism.PidWindowedLogcat,
            "4.3.1",
            selectionPath,
            parserPath);
        AssertTrue(File.Exists(selectionPath), "provider selection should exist");
        AssertEqual(20, selection.VerifiedCaseCount, "verified selection count");
        AssertTrue(!string.IsNullOrWhiteSpace(selection.VerificationReportSha256), "report hash");
        AssertTrue(!string.IsNullOrWhiteSpace(selection.ParserProfileSha256), "parser hash");
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task ProviderSelectionRefusesRejectedReportAsync()
{
    var directory = CreateTemporaryDirectory();
    try
    {
        var (manifest, profile) = await CreateVerificationFixtureAsync(directory, wrongCase: 1, partialCase: null);
        var output = Path.Combine(directory, "out");
        _ = await new CalcyVerificationRunner().RunAsync(manifest, directory, output, profile);
        await ExpectInvalidOperationAsync(() => CalcyProviderSelectionService.SelectAsync(
            Path.Combine(output, "verification-report.json"),
            CalcyProviderMechanism.PidWindowedLogcat,
            "4.3.1",
            Path.Combine(directory, "selection.json"),
            RepositoryPath("data", "calcy-parser-profile.synthetic.json")));
    }
    finally
    {
        DeleteDirectory(directory);
    }
}

static async Task<(string ManifestPath, CalcyTextParserProfile Profile)> CreateVerificationFixtureAsync(
    string directory,
    int? wrongCase,
    int? partialCase)
{
    Directory.CreateDirectory(Path.Combine(directory, "cases"));
    var cases = new List<CalcyVerificationCase>();
    for (var index = 1; index <= 20; index++)
    {
        var id = index.ToString("000", CultureInfo.InvariantCulture);
        var relative = $"cases/{id}.txt";
        var cp = wrongCase == index ? 9000 : 500 + index;
        var raw = partialCase == index
            ? $"species=Pikachu dex=25 cp={cp} atk=15 def=14"
            : $"species=Pikachu dex=25 cp={cp} atk=15 def=14 sta=13";
        await File.WriteAllTextAsync(
            Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar)),
            raw);
        cases.Add(new CalcyVerificationCase
        {
            Id = id,
            Sources = new Dictionary<string, string> { ["logcat"] = relative },
            Expected = Expected(index)
        });
    }

    var manifestPath = VerificationManifest(directory, cases.ToArray());
    return (manifestPath, await LoadSyntheticCalcyParserProfileAsync());
}

static string VerificationManifest(string directory, IReadOnlyList<CalcyVerificationCase> cases)
{
    var manifest = new CalcyVerificationManifest
    {
        Name = "Self-test verification",
        Mechanism = CalcyProviderMechanism.PidWindowedLogcat,
        ProviderVersion = "4.3.1",
        MinimumCases = 20,
        MinimumExactCompleteRate = 0.95,
        Cases = cases
    };
    var path = Path.Combine(directory, "verification-manifest.json");
    File.WriteAllText(
        path,
        JsonSerializer.Serialize(manifest, VerificationJson.CreateOptions(writeIndented: true)));
    return path;
}

static CalcyVerificationCase VerificationCase(int index, string path) =>
    new()
    {
        Id = index.ToString("000", CultureInfo.InvariantCulture),
        Sources = new Dictionary<string, string> { ["logcat"] = path },
        Expected = Expected(index)
    };

static ExpectedPokemonObservation Expected(int index) =>
    new()
    {
        Species = "Pikachu",
        PokedexNumber = 25,
        Cp = 500 + index,
        AttackIv = 15,
        DefenseIv = 14,
        HpIv = 13
    };

static Task<CalcyTextParserProfile> LoadSyntheticCalcyParserProfileAsync() =>
    CalcyTextParserProfileLoader.LoadAsync(
        RepositoryPath("data", "calcy-parser-profile.synthetic.json"));

static async Task ExpectInvalidOperationAsync(Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException("Expected InvalidOperationException.");
    }
    catch (InvalidOperationException exception) when (
        exception.Message != "Expected InvalidOperationException.")
    {
    }
}

static async Task ExpectAnyFailureAsync(Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException("Expected failure.");
    }
    catch (Exception exception) when (exception.Message != "Expected failure.")
    {
    }
}

static void AssertThrowsInvalidOperation(Action action, string description)
{
    try
    {
        action();
        throw new InvalidOperationException($"Expected {description} to fail.");
    }
    catch (InvalidOperationException exception) when (
        exception.Message != $"Expected {description} to fail.")
    {
    }
}

static PokemonObservation P(
    string key,
    string species,
    int cp,
    int attack,
    int defense,
    int hp) =>
    new()
    {
        ExternalKey = key,
        SequenceNumber = 1,
        Species = species,
        Form = "Normal",
        Costume = "None",
        Cp = cp,
        AttackIv = attack,
        DefenseIv = defense,
        HpIv = hp,
        CatchDate = new DateOnly(2026, 7, 1),
        CatchLocation = "Holstebro, Danmark",
        IsShiny = false,
        IsMythical = false,
        IsBackground = false,
        IsFavorite = false,
        IsLegendary = false,
        IsUltraBeast = false,
        IsShadow = false,
        IsPurified = false,
        IsLucky = false,
        IsCostume = false,
        IsDynamax = false,
        IsGigantamax = false,
        HasSpecialMove = false,
        IsXxl = false,
        IsXxs = false,
        IdentityConfidence = IdentityConfidence.Exact,
        VariantIdentity = Variant(
            species.Equals("Pikachu", StringComparison.OrdinalIgnoreCase) ? 25 :
            species.Equals("Eevee", StringComparison.OrdinalIgnoreCase) ? 133 :
            species.Equals("Machop", StringComparison.OrdinalIgnoreCase) ? 66 :
            species.Equals("Rattata", StringComparison.OrdinalIgnoreCase) ? 19 :
            species.Equals("Bidoof", StringComparison.OrdinalIgnoreCase) ? 399 : 1,
            species)
    };

static PokemonVariantIdentity Variant(
    int speciesId,
    string speciesName,
    string costumeId = "none",
    string costumeName = "None",
    string backgroundId = "none",
    string backgroundName = "None",
    bool isShiny = false) =>
    new()
    {
        SpeciesId = speciesId,
        SpeciesName = speciesName,
        FormId = "normal",
        FormName = "Normal",
        CostumeId = costumeId,
        CostumeName = costumeName,
        BackgroundId = backgroundId,
        BackgroundName = backgroundName,
        GenderVariant = "not-variant-relevant",
        IsShiny = isShiny,
        ShadowState = "normal",
        LuckyState = "not-lucky",
        DynamaxState = "none",
        SpecialVariantId = "none",
        SpecialVariantName = "None",
        VariantIdentityConfidence = IdentityConfidence.Exact
    };

static void AssertCategory(
    InventoryAnalysisResult result,
    string externalKey,
    DecisionCategory expected)
{
    var actual = result.Decisions.Single(x => x.ExternalKey == externalKey).Category;
    AssertEqual(expected, actual, $"decision for {externalKey}");
}

static void AssertEqual<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"Expected {description} to be '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static string CreateTemporaryDirectory()
{
    var path = Path.Combine(
        Path.GetTempPath(),
        "PogoInventoryAssistant",
        Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void DeleteDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
}
