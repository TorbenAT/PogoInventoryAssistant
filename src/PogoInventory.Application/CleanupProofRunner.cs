using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Automation.Timing;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
using PogoInventory.HeaderText;
using PogoInventory.Persistence;

namespace PogoInventory.Application;

public sealed record CleanupProofRequest
{
    public required string SpeciesQuery { get; init; }
    public required int ItemLimit { get; init; }
    public required string DatabasePath { get; init; }
    public required string OutputDirectory { get; init; }
    public required string DeviceSerial { get; init; }
    public bool ContinueOnPartial { get; init; }
    public int MaximumCaptureFrames { get; init; } = 8;
    public int MinimumCompleteFrames { get; init; } = 3;
    public int MinimumPartialFrames { get; init; } = 2;

    /// <summary>
    /// Optional species reference used to classify <see cref="SpeciesQuery"/> as
    /// <see cref="SearchQueryKind.ExactSpecies"/> versus
    /// <see cref="SearchQueryKind.BroadFilter"/>, and to validate OCR'd header
    /// text against known species. When null, the query is always treated as a
    /// broad filter (so the raw query is never trusted as a species name) and
    /// header OCR species text cannot be validated.
    /// </summary>
    public ISpeciesReference? SpeciesReference { get; init; }

    /// <summary>
    /// Optional header (species/CP/nickname) OCR analyzer. When null (e.g. on a
    /// non-Windows host, or when no OCR engine is available), the runner
    /// behaves exactly as before except species assignment follows the
    /// query-classification rule instead of the raw query text.
    /// </summary>
    public PokemonHeaderAnalyzer? HeaderAnalyzer { get; init; }

    /// <summary>Rule policy used for the recommendation analysis. Defaults to built-in defaults when null.</summary>
    public RulePolicy? Policy { get; init; }

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SpeciesQuery);
        if (ItemLimit is < 6 or > 50) throw new ArgumentOutOfRangeException(nameof(ItemLimit));
        ArgumentException.ThrowIfNullOrWhiteSpace(DatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(DeviceSerial);
        if (MaximumCaptureFrames is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(MaximumCaptureFrames));
        if (MinimumCompleteFrames < 3 || MinimumCompleteFrames > MaximumCaptureFrames)
            throw new ArgumentOutOfRangeException(nameof(MinimumCompleteFrames));
        if (MinimumPartialFrames < 2 || MinimumPartialFrames > MinimumCompleteFrames)
            throw new ArgumentOutOfRangeException(nameof(MinimumPartialFrames));
    }
}

public sealed record CleanupProofRunResult
{
    public required string RunId { get; init; }
    public required string Status { get; init; }
    public required string StopReason { get; init; }
    public required int CapturedItems { get; init; }
    public required int CompleteItems { get; init; }
    public required int PartialItems { get; init; }
    public required int UnresolvedItems { get; init; }
    public required CleanupProofSqlSummary SqlSummary { get; init; }
    public required IReadOnlyDictionary<string, int> RecommendationCounts { get; init; }
}

public sealed class CleanupProofRunner
{
    /// <summary>
    /// Reason recorded in <see cref="VerifiedTagObservation.Evidence"/> when the
    /// tag read was short-circuited because the phone was in the Appraisal
    /// carousel (where PokemonDetails, and therefore tag pills, can never be
    /// observed). Kept in sync with
    /// AndroidVerifiedInventoryNamedOperations.TagReadSkippedAppraisalCarouselReason.
    /// </summary>
    private const string TagReadSkippedAppraisalCarouselReason = "TagReadSkipped:AppraisalCarousel";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<CleanupProofRunResult> RunAsync(
        ICleanupProofNamedOperations operations,
        CleanupProofRequest request,
        IOperationTimingCollector? timing = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        timing ??= NullOperationTimingCollector.Instance;
        timing.MarkRunStart();
        var output = Path.GetFullPath(request.OutputDirectory);
        var database = Path.GetFullPath(request.DatabasePath);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "evidence"));
        var runId = $"cleanup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..35];
        var persistence = new InventoryPersistenceService(database);
        await persistence.StartCleanupRunAsync(new CleanupProofRunStart
        {
            RunId = runId,
            SearchQuery = request.SpeciesQuery,
            StartedAtUtc = DateTimeOffset.UtcNow,
            DeviceSerial = request.DeviceSerial,
            RequestedItems = request.ItemLimit,
            SourceDirectory = output
        }, cancellationToken);

        var captures = new List<CleanupProofObservationRecord>();
        var stopReason = "ItemLimitReached";
        var safeState = false;
        try
        {
            var inventory = await operations.EnsureFilteredInventoryAsync(request.SpeciesQuery, cancellationToken);
            if (inventory != VerifiedSequenceState.Inventory)
            {
                stopReason = "FilteredInventoryNotVerified";
                return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, timing, cancellationToken);
            }
            var opened = await operations.OpenFirstPokemonAsync(cancellationToken);
            if (opened != VerifiedSequenceState.PokemonDetails)
            {
                stopReason = "FirstPokemonDetailsNotVerified";
                return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, timing, cancellationToken);
            }

            var appraisalOpen = false;
            for (var ordinal = 1; ordinal <= request.ItemLimit; ordinal++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                timing.BeginItem(ordinal);
                var identity = ordinal == 1
                    ? await operations.CaptureCleanupIdentityAsync(
                        request.MaximumCaptureFrames,
                        request.MinimumCompleteFrames,
                        request.MinimumPartialFrames,
                        cancellationToken)
                    : await operations.CaptureCleanupAppraisalIdentityAsync(cancellationToken);
                if (identity.Status == CleanupProofObservationStatus.Unresolved)
                {
                    var retry = ordinal == 1
                        ? await operations.CaptureCleanupIdentityAsync(
                            request.MaximumCaptureFrames,
                            request.MinimumCompleteFrames,
                            request.MinimumPartialFrames,
                            cancellationToken)
                        : await operations.CaptureCleanupAppraisalIdentityAsync(cancellationToken);
                    if (retry.Status != CleanupProofObservationStatus.Unresolved)
                        identity = retry;
                    else
                    {
                        stopReason = "UNRESOLVED_DETAILS:" + string.Join(';', retry.FailureReasons);
                        return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, timing, cancellationToken);
                    }
                }

                var tags = await operations.ReadTagObservationAsync(cancellationToken);
                var headerScreen = ordinal == 1 ? HeaderScreenType.PokemonDetails : HeaderScreenType.AppraisalBars;
                var extraction = await BuildSemanticExtractionAsync(
                    runId, ordinal, request, identity, tags, headerScreen, timing, cancellationToken);
                var observation = extraction.Observation;
                var record = new CleanupProofObservationRecord
                {
                    RunId = runId,
                    Ordinal = ordinal,
                    LocalPokemonId = $"{runId}:{ordinal:D6}",
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                    Observation = observation,
                    ObservationStatus = identity.Status.ToString(),
                    IdentityConfidenceValue = IdentityConfidenceValue(identity.Status),
                    ProtectionConfidenceValue = 0.10,
                    StableFingerprint = identity.Consensus.StableFingerprintSha256,
                    ScreenshotPaths = identity.ScreenshotPaths,
                    ScreenshotHashes = identity.ScreenshotHashes,
                    AppraisalEvidence = new[] { "AppraisalStatus:Pending" },
                    FieldEvidenceSources = FieldEvidence(
                        observation, tags, "Pending",
                        extraction.SpeciesEvidence, extraction.CpEvidence,
                        "Unknown", "Unknown", "Unknown")
                };
                captures.Add(record);
                await persistence.RecordCleanupObservationAsync(record, cancellationToken);

                // Identity and tags are durable before appraisal begins. A
                // later best-effort appraisal or navigation failure therefore
                // cannot erase the real phone item from SQLite.
                CleanupProofAppraisalCapture appraisal;
                try
                {
                    appraisal = appraisalOpen
                        ? await operations.CaptureCurrentCleanupAppraisalAsync(cancellationToken)
                        : await operations.CaptureCleanupAppraisalAsync(cancellationToken);
                    // The named operation has entered/confirmed the carousel
                    // even when semantic bars are unavailable; retain the
                    // single-exit lifecycle and let the bounded loop decide
                    // whether to continue on partial evidence.
                    appraisalOpen = true;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    appraisal = new CleanupProofAppraisalCapture
                    {
                        Status = "Unavailable",
                        FailureReasons = new[] { exception.GetType().Name + ":" + exception.Message }
                    };
                }

                var appraisalOutcome = ApplyAppraisal(observation, appraisal);
                var enrichedObservation = appraisalOutcome.Observation;
                var criticalTripleKnown =
                    !string.Equals(enrichedObservation.Species, "Unknown", StringComparison.Ordinal) &&
                    enrichedObservation.Cp is not null &&
                    enrichedObservation.AttackIv is not null &&
                    enrichedObservation.DefenseIv is not null &&
                    enrichedObservation.HpIv is not null;
                var enrichedRecord = record with
                {
                    Observation = enrichedObservation,
                    ObservationStatus = criticalTripleKnown ? "Complete" : record.ObservationStatus,
                    AppraisalEvidence = appraisal.EvidencePaths.Count == 0
                        ? new[] { "AppraisalStatus:" + appraisal.Status }
                        : appraisal.EvidencePaths,
                    FieldEvidenceSources = FieldEvidence(
                        enrichedObservation, tags, appraisal.Status,
                        extraction.SpeciesEvidence, extraction.CpEvidence,
                        appraisalOutcome.AttackEvidence, appraisalOutcome.DefenseEvidence, appraisalOutcome.HpEvidence)
                };
                await persistence.EnrichCleanupAppraisalAsync(
                    runId,
                    record.LocalPokemonId,
                    enrichedObservation,
                    appraisal,
                    enrichedRecord.FieldEvidenceSources,
                    cancellationToken,
                    enrichedRecord.ObservationStatus);
                captures[^1] = enrichedRecord;

                // EndItem is deliberately deferred until after the carousel
                // advance (or the decision not to advance) below: the advance's
                // captures/swipe/settle delays belong to the item they advance
                // FROM, so honest per-item timing must include them. The
                // try/finally guarantees EndItem still fires exactly once for
                // every loop exit from this point (last-item break, no-effect
                // break, unknown-stop return, or a propagating exception).
                try
                {
                    if (ordinal >= request.ItemLimit)
                    {
                        safeState = true;
                        break;
                    }

                    var advanced = await operations.AdvanceToNextPokemonInAppraisalAsync(
                        identity.Consensus.StableFingerprintSha256,
                        appraisal,
                        cancellationToken);
                    if (advanced == AppraisalCarouselAdvanceResult.NO_EFFECT_OR_FILTER_END)
                    {
                        stopReason = "FilterExhaustedAfterStableNoEffect";
                        safeState = true;
                        break;
                    }
                    if (advanced is AppraisalCarouselAdvanceResult.UNKNOWN_STOP)
                    {
                        stopReason = "CursorProgression:" + advanced;
                        return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, timing, cancellationToken);
                    }
                }
                finally
                {
                    timing.EndItem(ordinal);
                }
            }

            if (!safeState)
                safeState = true;
            if (safeState)
            {
                if (appraisalOpen)
                {
                    var exited = await operations.ExitAppraisalAsync(cancellationToken);
                    if (exited != VerifiedSequenceState.PokemonDetails)
                    {
                        stopReason = "AppraisalExitFailed:" + exited;
                        return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, timing, cancellationToken);
                    }
                }
                var inventoryState = await operations.ReturnToInventoryAsync(cancellationToken);
                if (inventoryState == VerifiedSequenceState.Inventory)
                {
                    var finalState = await operations.CloseInventoryAsync(cancellationToken);
                    if (!string.Equals(finalState, "GameplayMap", StringComparison.Ordinal))
                        stopReason = "FinalMapNotVerified:" + finalState;
                }
                else
                {
                    stopReason = "FinalInventoryNotVerified:" + inventoryState;
                }
            }
            var finalStatus = captures.Count >= request.ItemLimit ? "Completed" : "CompletedPartial";
            return await FinishAsync(finalStatus, stopReason, captures, runId, persistence, request, timing, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopReason = exception.GetType().Name + ":" + exception.Message;
            return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, timing, cancellationToken);
        }
    }

    private sealed record SemanticExtractionResult(
        PokemonObservation Observation,
        string SpeciesEvidence,
        string CpEvidence);

    private sealed record AppraisalApplicationResult(
        PokemonObservation Observation,
        string AttackEvidence,
        string DefenseEvidence,
        string HpEvidence);

    /// <summary>
    /// Resolves Species/Cp/Nickname for one captured item.
    ///
    /// Species resolution order:
    /// 1. The search query classifies as <see cref="SearchQueryKind.ExactSpecies"/>
    ///    (a single species token, optionally combined with non-species filters) ->
    ///    Species is the validated species, evidence "QueryDerived".
    /// 2. Otherwise, header OCR multi-frame consensus (&gt;= 2 of the captured
    ///    frames agree) resolves a species -> evidence "Automated".
    /// 3. Otherwise Species stays "Unknown" (never guessed), evidence "Unknown".
    ///
    /// The raw query text itself is never persisted as Species: a broad-filter
    /// query (e.g. "age0-1825") can only ever reach case 3.
    /// </summary>
    private static async Task<SemanticExtractionResult> BuildSemanticExtractionAsync(
        string runId,
        int ordinal,
        CleanupProofRequest request,
        CleanupProofIdentityCapture identity,
        VerifiedTagObservation tags,
        HeaderScreenType headerScreen,
        IOperationTimingCollector timing,
        CancellationToken cancellationToken)
    {
        var speciesReference = request.SpeciesReference ?? new StaticSpeciesReference(Array.Empty<string>());
        var classification = SearchQueryClassifier.Classify(request.SpeciesQuery, speciesReference);

        PokemonHeaderConsensusResult? headerConsensus = null;
        if (request.HeaderAnalyzer is not null && identity.ScreenshotPaths.Count >= 2)
        {
            headerConsensus = await AnalyzeHeaderConsensusAsync(
                request.HeaderAnalyzer, identity.ScreenshotPaths, headerScreen, timing, cancellationToken);
        }

        var species = "Unknown";
        var speciesEvidence = "Unknown";
        if (classification is { Kind: SearchQueryKind.ExactSpecies, Species: not null })
        {
            species = classification.Species;
            speciesEvidence = "QueryDerived";
        }
        else if (headerConsensus?.Species is not null)
        {
            species = headerConsensus.Species;
            speciesEvidence = "Automated";
        }

        // Hard guard: a broad-filter query (e.g. "age0-1825") must never be
        // persisted as the species. Species can only come from an explicit
        // ExactSpecies query classification or a validated OCR consensus; this
        // is a defensive regression check against reintroducing the raw-query
        // assignment bug.
        if (classification.Kind == SearchQueryKind.BroadFilter &&
            string.Equals(species, request.SpeciesQuery, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to persist the raw broad-filter search query '{request.SpeciesQuery}' as Species.");
        }

        int? cp = null;
        var cpEvidence = "Unknown";
        if (headerConsensus?.Cp is not null)
        {
            cp = headerConsensus.Cp;
            cpEvidence = "Automated";
        }

        string? nickname = null;
        if (headerConsensus is not null && headerConsensus.Species is null)
        {
            nickname = headerConsensus.Frames
                .Select(frame => frame.Nickname)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .GroupBy(text => text, StringComparer.Ordinal)
                .Where(group => group.Count() >= 2)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault();
        }

        var observation = new PokemonObservation
        {
            ExternalKey = $"{runId}:{ordinal:D6}",
            SequenceNumber = ordinal,
            Species = species,
            Nickname = nickname,
            Cp = cp,
            Tags = tags.NamesComplete ? tags.KnownTagNames : Array.Empty<string>(),
            IdentityConfidence = identity.Status switch
            {
                CleanupProofObservationStatus.Complete => IdentityConfidence.HighConfidence,
                CleanupProofObservationStatus.Partial => IdentityConfidence.Medium,
                _ => IdentityConfidence.Unknown
            }
        };
        return new SemanticExtractionResult(observation, speciesEvidence, cpEvidence);
    }

    private static async Task<PokemonHeaderConsensusResult?> AnalyzeHeaderConsensusAsync(
        PokemonHeaderAnalyzer analyzer,
        IReadOnlyList<string> screenshotPaths,
        HeaderScreenType screen,
        IOperationTimingCollector timing,
        CancellationToken cancellationToken)
    {
        var frames = new List<PokemonHeaderResult>();
        foreach (var path in screenshotPaths)
        {
            byte[] bytes;
            try
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            }
            catch (IOException)
            {
                continue;
            }
            using var _ = timing.Measure(TimingCategory.Ocr, "HeaderFrame");
            frames.Add(await analyzer.AnalyzeAsync(bytes, screen, cancellationToken));
        }
        return frames.Count < 2 ? null : PokemonHeaderAnalyzer.Consensus(frames);
    }

    /// <summary>
    /// Accepts measured IVs as trusted only when multi-frame consensus holds:
    /// at least two of the independently analyzed appraisal evidence frames
    /// agree on the same (attack, defense, hp) triple and each of those
    /// agreeing frames met the per-bar confidence threshold
    /// (<see cref="AppraisalFrameIv.BarsConfident"/>). This deliberately does
    /// not depend on <c>AppraisalAnalyzer</c>'s own Calcy-verified-profile
    /// gate (which is left untouched) - consensus is computed here instead.
    /// </summary>
    private static AppraisalApplicationResult ApplyAppraisal(
        PokemonObservation observation,
        CleanupProofAppraisalCapture appraisal)
    {
        var consensus = ComputeIvConsensus(appraisal);
        if (consensus is null)
        {
            return new AppraisalApplicationResult(observation, "Unknown", "Unknown", "Unknown");
        }

        var updated = observation with
        {
            AttackIv = consensus.Value.Attack,
            DefenseIv = consensus.Value.Defense,
            HpIv = consensus.Value.Hp
        };
        return new AppraisalApplicationResult(updated, "Automated", "Automated", "Automated");
    }

    private static (int Attack, int Defense, int Hp)? ComputeIvConsensus(CleanupProofAppraisalCapture appraisal)
    {
        var confidentTriples = appraisal.Frames
            .Where(frame => frame.BarsConfident &&
                frame.AttackIv is not null && frame.DefenseIv is not null && frame.HpIv is not null)
            .Select(frame => (Attack: frame.AttackIv!.Value, Defense: frame.DefenseIv!.Value, Hp: frame.HpIv!.Value))
            .ToArray();
        if (confidentTriples.Length < 2) return null;

        var groups = confidentTriples
            .GroupBy(triple => triple)
            .Where(group => group.Count() >= 2)
            .OrderByDescending(group => group.Count())
            .ToArray();
        return groups.Length == 0 ? null : groups[0].Key;
    }

    private static IReadOnlyDictionary<string, string> FieldEvidence(
        PokemonObservation observation,
        VerifiedTagObservation tags,
        string appraisalStatus,
        string speciesEvidence,
        string cpEvidence,
        string attackEvidence,
        string defenseEvidence,
        string hpEvidence) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Species"] = speciesEvidence,
        ["Cp"] = cpEvidence,
        ["AttackIv"] = attackEvidence,
        ["DefenseIv"] = defenseEvidence,
        ["HpIv"] = hpEvidence,
        ["Form"] = "Unknown",
        ["Costume"] = "Unknown",
        ["Background"] = "Unknown",
        ["Shiny"] = "Unknown",
        ["Shadow"] = "Unknown",
        ["Lucky"] = "Unknown",
        ["Dynamax"] = "Unknown",
        ["Nickname"] = observation.Nickname is null ? "Unknown" : "Automated",
        ["CatchDateOrAge"] = "Unknown",
        ["CatchLocation"] = "Unknown",
        ["ExistingTags"] = tags.Evidence.Contains(TagReadSkippedAppraisalCarouselReason, StringComparer.Ordinal)
            ? TagReadSkippedAppraisalCarouselReason
            : tags.NamesComplete ? "EvidenceReviewed" : "Unknown",
        ["ObservationStatus"] = "Automated",
        ["IdentityConfidence"] = "Automated",
        ["ProtectionConfidence"] = "Automated",
        ["StableFingerprint"] = "Automated",
        ["AppraisalEvidence"] = appraisalStatus == "AppraisalBarsObserved" ? "EvidenceReviewed" : "Unknown"
    };

    private static double IdentityConfidenceValue(CleanupProofObservationStatus status) => status switch
    {
        CleanupProofObservationStatus.Complete => 0.85,
        CleanupProofObservationStatus.Partial => 0.55,
        _ => 0
    };

    private async Task<CleanupProofRunResult> FinishAsync(
        string status,
        string stopReason,
        IReadOnlyList<CleanupProofObservationRecord> captures,
        string runId,
        InventoryPersistenceService persistence,
        CleanupProofRequest request,
        IOperationTimingCollector timing,
        CancellationToken cancellationToken)
    {
        await persistence.CompleteCleanupRunAsync(
            runId,
            captures.Count,
            status,
            stopReason,
            DateTimeOffset.UtcNow,
            cancellationToken);

        // Deliberately create a new service instance after the write connection
        // has been disposed. All policy results and reports below use rows
        // loaded from this fresh SQLite connection.
        var reloadedPersistence = new InventoryPersistenceService(Path.GetFullPath(request.DatabasePath));
        var reloadedBeforeAnalysis = await reloadedPersistence.LoadCleanupProofRowsAsync(runId, cancellationToken);
        var analysis = new InventoryAnalyzer().Analyze(
            reloadedBeforeAnalysis.Select(row => row.Observation).ToArray(),
            request.Policy ?? new RulePolicy());
        foreach (var decision in analysis.Decisions)
            await reloadedPersistence.UpdateRecommendationAsync(runId, decision, cancellationToken);

        var rows = await new InventoryPersistenceService(Path.GetFullPath(request.DatabasePath))
            .LoadCleanupProofRowsAsync(runId, cancellationToken);
        var sqlSummary = await new InventoryPersistenceService(Path.GetFullPath(request.DatabasePath))
            .ReadCleanupProofSqlSummaryAsync(cancellationToken);
        var comparative = CleanupProofComparativeAnalyzer.BuildComparativeSuggestions(rows);
        ValidateReports(rows, sqlSummary, request.OutputDirectory);
        await WriteReportsAsync(request, runId, status, stopReason, captures, rows, sqlSummary, comparative, timing, cancellationToken);
        var counts = rows.GroupBy(row => row.CurrentRecommendation, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        return new CleanupProofRunResult
        {
            RunId = runId,
            Status = status,
            StopReason = stopReason,
            CapturedItems = rows.Count,
            CompleteItems = rows.Count(row => row.ObservationStatus == "Complete"),
            PartialItems = rows.Count(row => row.ObservationStatus == "Partial"),
            UnresolvedItems = rows.Count(row => row.ObservationStatus == "Unresolved"),
            SqlSummary = sqlSummary,
            RecommendationCounts = counts
        };
    }

    private static void ValidateReports(
        IReadOnlyList<CleanupProofDatabaseRow> rows,
        CleanupProofSqlSummary summary,
        string output)
    {
        if (!string.Equals(summary.IntegrityCheck, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQLite integrity_check did not return ok.");
        if (summary.ObservationCount != rows.Count || summary.PokemonRecordCount != rows.Count)
            throw new InvalidOperationException("SQLite observation and record counts do not match the reloaded batch.");
        if (rows.Select(row => (row.RunId, row.Ordinal)).Distinct().Count() != rows.Count)
            throw new InvalidOperationException("Duplicate RunId/Ordinal in cleanup proof rows.");
        foreach (var path in rows.SelectMany(row => row.ScreenshotPaths.Concat(row.AppraisalEvidence)))
        {
            if (path.StartsWith("AppraisalStatus:", StringComparison.Ordinal)) continue;
            if (!File.Exists(path))
                throw new InvalidOperationException($"Missing evidence path: {path}");
        }
        _ = output;
    }

    private static async Task WriteReportsAsync(
        CleanupProofRequest request,
        string runId,
        string status,
        string stopReason,
        IReadOnlyList<CleanupProofObservationRecord> captures,
        IReadOnlyList<CleanupProofDatabaseRow> rows,
        CleanupProofSqlSummary sql,
        IReadOnlyList<CleanupProofComparativeSuggestion> comparative,
        IOperationTimingCollector timing,
        CancellationToken cancellationToken)
    {
        var output = Path.GetFullPath(request.OutputDirectory);
        var timingReport = timing.BuildReport();
        if (!timingReport.IsEmpty)
        {
            await File.WriteAllTextAsync(Path.Combine(output, "timing-report.json"),
                JsonSerializer.Serialize(timingReport, JsonOptions), cancellationToken);
        }
        await File.WriteAllTextAsync(Path.Combine(output, "captured-observations.json"),
            JsonSerializer.Serialize(captures, JsonOptions), cancellationToken);
        var review = rows.Select(row => SemanticReview(row, request.SpeciesQuery)).ToArray();
        await File.WriteAllTextAsync(Path.Combine(output, "semantic-review.template.json"),
            JsonSerializer.Serialize(review, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "semantic-review.json"),
            JsonSerializer.Serialize(review, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "db-roundtrip.json"),
            JsonSerializer.Serialize(new
            {
                runId,
                databaseReopenedBeforeAnalysis = true,
                rowsLoadedFromDatabase = rows.Count,
                sql
            }, JsonOptions), cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("LocalPokemonId,Ordinal,Species,Cp,IV,Variant,ObservationStatus,IdentityConfidence,ProtectionConfidence,Recommendation,RecommendationReason,ComparatorLocalPokemonId,EvidencePath");
        foreach (var row in rows)
        {
            var iv = row.Observation.TotalIv?.ToString(CultureInfo.InvariantCulture) ?? "Unknown";
            var variant = row.Observation.VariantIdentity?.VariantKey ?? "Unknown";
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(row.LocalPokemonId), row.Ordinal.ToString(CultureInfo.InvariantCulture), Csv(row.Observation.Species),
                Csv(row.Observation.Cp?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"), Csv(iv), Csv(variant),
                Csv(row.ObservationStatus), Csv(row.Observation.IdentityConfidence.ToString()),
                row.ProtectionConfidenceValue.ToString("F2", CultureInfo.InvariantCulture), Csv(row.CurrentRecommendation),
                Csv(row.RecommendationReason), Csv(row.ComparatorLocalPokemonId ?? string.Empty),
                Csv(row.ScreenshotPaths.FirstOrDefault() ?? string.Empty)
            }));
        }
        await File.WriteAllTextAsync(Path.Combine(output, "recommendations.csv"), csv.ToString(), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(output, "strict-recommendations.csv"), csv.ToString(), cancellationToken);

        var comparativeCsv = new StringBuilder();
        comparativeCsv.AppendLine("LocalPokemonId,Species,Classification,Label,CP,TotalIv,Ordinal,ComparatorLocalPokemonId,MissingProtectionChecks");
        foreach (var item in comparative)
        {
            comparativeCsv.AppendLine(string.Join(',', new[]
            {
                Csv(item.LocalPokemonId), Csv(item.Species), Csv(item.Classification), Csv(item.Label),
                Csv(item.Cp?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"),
                Csv(item.TotalIv?.ToString(CultureInfo.InvariantCulture) ?? "Unknown"),
                item.Ordinal.ToString(CultureInfo.InvariantCulture), Csv(item.ComparatorLocalPokemonId ?? string.Empty),
                Csv(string.Join(";", item.MissingProtectionChecks))
            }));
        }
        await File.WriteAllTextAsync(Path.Combine(output, "comparative-cleanup-suggestions.csv"), comparativeCsv.ToString(), cancellationToken);

        var markdown = new StringBuilder("# Recommendations\n\n| LocalPokemonId | Ordinal | Species | CP | IV | Status | Recommendation | Reason | Comparator | Evidence |\n|---|---:|---|---:|---:|---|---|---|---|---|\n");
        foreach (var row in rows)
        {
            markdown.AppendLine($"| {row.LocalPokemonId} | {row.Ordinal} | {row.Observation.Species} | {row.Observation.Cp?.ToString() ?? "Unknown"} | {row.Observation.TotalIv?.ToString() ?? "Unknown"} | {row.ObservationStatus} | {row.CurrentRecommendation} | {row.RecommendationReason.Replace('|', '/')} | {row.ComparatorLocalPokemonId ?? ""} | {row.ScreenshotPaths.FirstOrDefault() ?? ""} |");
        }
        await File.WriteAllTextAsync(Path.Combine(output, "recommendations.md"), markdown.ToString(), cancellationToken);

        await File.WriteAllTextAsync(Path.Combine(output, "group-summary.json"),
            JsonSerializer.Serialize(rows.GroupBy(row => row.Observation.GroupKey, StringComparer.Ordinal)
                .Select(group => new
                {
                    groupKey = group.Key,
                    count = group.Count(),
                    recommendations = group.GroupBy(row => row.CurrentRecommendation, StringComparer.Ordinal)
                        .ToDictionary(item => item.Key, item => item.Count(), StringComparer.Ordinal),
                    localPokemonIds = group.Select(row => row.LocalPokemonId).ToArray()
                }), JsonOptions), cancellationToken);

        var counts = rows.GroupBy(row => row.CurrentRecommendation, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var tagReadSkippedCount = rows.Count(row =>
            row.FieldEvidenceSources.TryGetValue("ExistingTags", out var existingTags) &&
            existingTags == TagReadSkippedAppraisalCarouselReason);
        var proof = new StringBuilder();
        proof.AppendLine("# Real cleanup proof");
        proof.AppendLine();
        proof.AppendLine($"- RunId: `{runId}`");
        proof.AppendLine($"- Chosen species/query: `{request.SpeciesQuery}`");
        proof.AppendLine($"- Requested items: {request.ItemLimit}");
        proof.AppendLine($"- Phone records captured: {rows.Count}");
        proof.AppendLine($"- Complete / Partial / Unresolved: {rows.Count(row => row.ObservationStatus == "Complete")} / {rows.Count(row => row.ObservationStatus == "Partial")} / {rows.Count(row => row.ObservationStatus == "Unresolved")}");
        proof.AppendLine($"- SQLite ScanRuns / Observations / PokemonRecords / InventoryEvents: {sql.ScanRunCount} / {sql.ObservationCount} / {sql.PokemonRecordCount} / {sql.InventoryEventCount}");
        proof.AppendLine($"- KEEP / REVIEW / DELETE-CANDIDATE: {Count(counts, "KEEP")} / {Count(counts, "REVIEW")} / {Count(counts, "DELETE-CANDIDATE")}");
        proof.AppendLine($"- Comparative RETAINED / LIKELY_DELETE / INSUFFICIENT: {comparative.Count(item => item.Classification == "RETAINED_COMPARATOR")} / {comparative.Count(item => item.Classification == "LIKELY_DELETE_SUGGESTION")} / {comparative.Count(item => item.Classification == "INSUFFICIENT_COMPARISON_DATA")}");
        proof.AppendLine($"- Stop reason: `{stopReason}`");
        if (tagReadSkippedCount > 0)
            proof.AppendLine($"- Tag reads skipped (appraisal carousel): {tagReadSkippedCount}");
        proof.AppendLine();
        proof.AppendLine("## Recommendations generated from reloaded SQLite rows");
        proof.AppendLine();
        proof.AppendLine("| LocalPokemonId | Ordinal | Species | CP | IV | Status | Recommendation | Reason | Comparator |");
        proof.AppendLine("|---|---:|---|---:|---:|---|---|---|---|");
        foreach (var row in rows)
            proof.AppendLine($"| {row.LocalPokemonId} | {row.Ordinal} | {row.Observation.Species} | {row.Observation.Cp?.ToString() ?? "Unknown"} | {row.Observation.TotalIv?.ToString() ?? "Unknown"} | {row.ObservationStatus} | {row.CurrentRecommendation} | {row.RecommendationReason.Replace('|', '/')} | {row.ComparatorLocalPokemonId ?? ""} |");
        proof.AppendLine();
        proof.AppendLine("## Three sample database rows");
        proof.AppendLine();
        foreach (var row in rows.Take(3))
            proof.AppendLine($"- `{row.LocalPokemonId}` ordinal {row.Ordinal}, species `{row.Observation.Species}`, status `{row.ObservationStatus}`, recommendation `{row.CurrentRecommendation}`, evidence `{row.ScreenshotPaths.FirstOrDefault() ?? "none"}`.");
        proof.AppendLine();
        proof.AppendLine("## Comparative cleanup suggestions");
        proof.AppendLine();
        foreach (var item in comparative)
            proof.AppendLine($"- `{item.LocalPokemonId}` `{item.Label}` comparator `{item.ComparatorLocalPokemonId ?? "none"}`; missing protection checks: `{string.Join(", ", item.MissingProtectionChecks.DefaultIfEmpty("none"))}`.");
        proof.AppendLine();
        proof.AppendLine("## SQL integrity checks");
        proof.AppendLine();
        proof.AppendLine($"- `PRAGMA integrity_check;` -> `{sql.IntegrityCheck}`");
        proof.AppendLine($"- `SELECT COUNT(*) FROM ScanRuns;` -> {sql.ScanRunCount}");
        proof.AppendLine($"- `SELECT COUNT(*) FROM Observations;` -> {sql.ObservationCount}");
        proof.AppendLine($"- `SELECT COUNT(*) FROM PokemonRecords;` -> {sql.PokemonRecordCount}");
        proof.AppendLine($"- `SELECT COUNT(*) FROM InventoryEvents;` -> {sql.InventoryEventCount}");
        proof.AppendLine();
        proof.AppendLine("## Safety counters");
        proof.AppendLine();
        proof.AppendLine("- TAG_MUTATIONS=0; TRANSFER_ACTIONS=0; DELETE_ACTIONS=0; POWER_UP_ACTIONS=0; EVOLVE_ACTIONS=0; PURIFY_ACTIONS=0; PURCHASE_ACTIONS=0; FAVORITE_CHANGES=0; CALCY_ACTIONS=0.");
        proof.AppendLine();
        if (!timingReport.IsEmpty)
        {
            AppendTimingSection(proof, timingReport);
        }
        proof.AppendLine("## Limitations");
        proof.AppendLine();
        proof.AppendLine("- Species is QueryDerived from an exact species search, or Automated from >=2-frame header OCR consensus; CP is Automated from header OCR consensus; variant fields, catch date and location remain Unknown unless separately evidence-reviewed.");
        proof.AppendLine("- IVs are Automated only when >=2 independently analyzed appraisal evidence frames agree on the same (attack, defense, hp) triple with per-bar confidence at or above the visual profile threshold; no Calcy surface is used.");
        proof.AppendLine("- No tag, transfer, delete, power-up, evolve, purify, purchase or location-changing action is exposed by this command.");
        await File.WriteAllTextAsync(Path.Combine(output, "proof-summary.md"), proof.ToString(), cancellationToken);
    }

    private static void AppendTimingSection(StringBuilder proof, TimingReport report)
    {
        var breakdown = report.WallClockBreakdown();
        proof.AppendLine("## Timing");
        proof.AppendLine();
        proof.AppendLine($"- Wall clock: {report.WallClockMilliseconds:F0} ms");
        proof.AppendLine($"- Per-item mean: {report.PerItemMeanMilliseconds:F0} ms ({report.Items.Count} items)");
        proof.AppendLine(
            $"- capture-transfer {breakdown.ScreenCapturePercent:F1}% / " +
            $"fixed-wait {breakdown.FixedWaitPercent:F1}% / " +
            $"OCR {breakdown.OcrPercent:F1}% / " +
            $"other {breakdown.OtherPercent:F1}%");
        proof.AppendLine();
        proof.AppendLine("| Operation | Count | Total ms | Mean ms |");
        proof.AppendLine("|---|---:|---:|---:|");
        foreach (var operation in report.Operations)
            proof.AppendLine($"| {operation.Name} | {operation.Count} | {operation.TotalMilliseconds:F0} | {operation.MeanMilliseconds:F0} |");
        proof.AppendLine();
    }

    private static object SemanticReview(CleanupProofDatabaseRow row, string query)
    {
        var evidence = row.ScreenshotPaths.FirstOrDefault() ?? string.Empty;
        static object Field(string? value, string source, string path, string note) => new
        {
            value,
            source,
            evidencePath = path,
            reviewNote = note
        };
        var fields = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Species"] = Field(
                string.Equals(row.Observation.Species, "Unknown", StringComparison.Ordinal) ? null : row.Observation.Species,
                row.FieldEvidenceSources.TryGetValue("Species", out var speciesSource) ? speciesSource : "Unknown",
                evidence,
                "QueryDerived from an exact species search, or Automated from >=2-frame header OCR consensus."),
            ["Cp"] = Field(
                row.Observation.Cp?.ToString(CultureInfo.InvariantCulture),
                row.FieldEvidenceSources.TryGetValue("Cp", out var cpSource) ? cpSource : "Unknown",
                row.Observation.Cp is null ? string.Empty : evidence,
                "Automated from >=2-frame header OCR consensus; otherwise requires bounded human evidence review."),
            ["AttackIv"] = Field(row.Observation.AttackIv?.ToString(CultureInfo.InvariantCulture), row.Observation.AttackIv is null ? "Unknown" : "Automated", row.AppraisalEvidence.FirstOrDefault() ?? string.Empty, "Verified appraisal only when profile accepted Complete."),
            ["DefenseIv"] = Field(row.Observation.DefenseIv?.ToString(CultureInfo.InvariantCulture), row.Observation.DefenseIv is null ? "Unknown" : "Automated", row.AppraisalEvidence.FirstOrDefault() ?? string.Empty, "Verified appraisal only when profile accepted Complete."),
            ["HpIv"] = Field(row.Observation.HpIv?.ToString(CultureInfo.InvariantCulture), row.Observation.HpIv is null ? "Unknown" : "Automated", row.AppraisalEvidence.FirstOrDefault() ?? string.Empty, "Verified appraisal only when profile accepted Complete."),
            ["Form"] = Field(null, "Unknown", string.Empty, "Requires bounded human evidence review."),
            ["Costume"] = Field(null, "Unknown", string.Empty, "Requires bounded human evidence review."),
            ["Background"] = Field(null, "Unknown", string.Empty, "Requires bounded human evidence review."),
            ["Shiny"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Shadow"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Purified"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Lucky"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Dynamax"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Gigantamax"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Favorite"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["SpecialMove"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Xxl"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Xxs"] = Field(null, "Unknown", string.Empty, "Unknown is not false."),
            ["Nickname"] = Field(null, "Unknown", string.Empty, "Requires bounded human evidence review."),
            ["CatchDateOrAge"] = Field(null, "Unknown", string.Empty, "Requires bounded human evidence review."),
            ["ExistingTags"] = Field(row.Observation.Tags.Count == 0 ? null : string.Join(",", row.Observation.Tags), row.Observation.Tags.Count == 0 ? "Unknown" : "Automated", evidence, "Tag mutation is disabled; names are read-only evidence.")
        };
        return new { row.LocalPokemonId, row.Ordinal, Fields = fields, Query = query };
    }

    private static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var value) ? value : 0;

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
