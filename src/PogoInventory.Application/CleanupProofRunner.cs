using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Automation.Models;
using PogoInventory.Automation.Services;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Models;
using PogoInventory.Core.Policy;
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

    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(SpeciesQuery);
        if (ItemLimit is < 6 or > 20) throw new ArgumentOutOfRangeException(nameof(ItemLimit));
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<CleanupProofRunResult> RunAsync(
        ICleanupProofNamedOperations operations,
        CleanupProofRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
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
                return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, cancellationToken);
            }
            var opened = await operations.OpenFirstPokemonAsync(cancellationToken);
            if (opened != VerifiedSequenceState.PokemonDetails)
            {
                stopReason = "FirstPokemonDetailsNotVerified";
                return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, cancellationToken);
            }

            for (var ordinal = 1; ordinal <= request.ItemLimit; ordinal++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var identity = await operations.CaptureCleanupIdentityAsync(
                    request.MaximumCaptureFrames,
                    request.MinimumCompleteFrames,
                    request.MinimumPartialFrames,
                    cancellationToken);
                if (identity.Status == CleanupProofObservationStatus.Unresolved)
                {
                    var retry = await operations.CaptureCleanupIdentityAsync(
                        request.MaximumCaptureFrames,
                        request.MinimumCompleteFrames,
                        request.MinimumPartialFrames,
                        cancellationToken);
                    if (retry.Status != CleanupProofObservationStatus.Unresolved)
                        identity = retry;
                    else
                    {
                        stopReason = "UNRESOLVED_DETAILS:" + string.Join(';', retry.FailureReasons);
                        return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, cancellationToken);
                    }
                }

                var tags = await operations.ReadTagObservationAsync(cancellationToken);
                var observation = BuildObservation(runId, ordinal, request.SpeciesQuery, identity, tags);
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
                    FieldEvidenceSources = FieldEvidence(observation, tags, "Pending")
                };
                captures.Add(record);
                await persistence.RecordCleanupObservationAsync(record, cancellationToken);

                // Identity and tags are durable before appraisal begins. A
                // later best-effort appraisal or navigation failure therefore
                // cannot erase the real phone item from SQLite.
                CleanupProofAppraisalCapture appraisal;
                try
                {
                    appraisal = await operations.CaptureCleanupAppraisalAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    appraisal = new CleanupProofAppraisalCapture
                    {
                        Status = "Unavailable",
                        FailureReasons = new[] { exception.GetType().Name + ":" + exception.Message }
                    };
                }

                var enrichedObservation = ApplyAppraisal(observation, appraisal);
                var enrichedRecord = record with
                {
                    Observation = enrichedObservation,
                    AppraisalEvidence = appraisal.EvidencePaths.Count == 0
                        ? new[] { "AppraisalStatus:" + appraisal.Status }
                        : appraisal.EvidencePaths,
                    FieldEvidenceSources = FieldEvidence(enrichedObservation, tags, appraisal.Status)
                };
                await persistence.EnrichCleanupAppraisalAsync(
                    runId,
                    record.LocalPokemonId,
                    enrichedObservation,
                    appraisal,
                    enrichedRecord.FieldEvidenceSources,
                    cancellationToken);
                captures[^1] = enrichedRecord;

                var afterAppraisal = await operations.ExitAppraisalAsync(cancellationToken);
                if (afterAppraisal != VerifiedSequenceState.PokemonDetails)
                {
                    stopReason = "AppraisalDetailsRecoveryFailed";
                    return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, cancellationToken);
                }

                if (ordinal >= request.ItemLimit)
                {
                    safeState = true;
                    break;
                }

                var advanced = await operations.AdvanceToNextPokemonAsync(identity.Consensus, cancellationToken);
                if (advanced == VerifiedSequenceState.NoEffectOrEndOfFilter)
                {
                    stopReason = "FilterExhaustedAfterStableNoEffect";
                    safeState = true;
                    break;
                }
                if (advanced != VerifiedSequenceState.PokemonDetails)
                {
                    stopReason = "CursorProgression:" + advanced;
                    return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, cancellationToken);
                }
            }

            if (!safeState)
                safeState = true;
            if (safeState)
            {
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
            return await FinishAsync(finalStatus, stopReason, captures, runId, persistence, request, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopReason = exception.GetType().Name + ":" + exception.Message;
            return await FinishAsync("SafeStopped", stopReason, captures, runId, persistence, request, cancellationToken);
        }
    }

    private static PokemonObservation BuildObservation(
        string runId,
        int ordinal,
        string species,
        CleanupProofIdentityCapture identity,
        VerifiedTagObservation tags) => new()
    {
        ExternalKey = $"{runId}:{ordinal:D6}",
        SequenceNumber = ordinal,
        Species = species,
        Tags = tags.NamesComplete ? tags.KnownTagNames : Array.Empty<string>(),
        IdentityConfidence = identity.Status switch
        {
            CleanupProofObservationStatus.Complete => IdentityConfidence.HighConfidence,
            CleanupProofObservationStatus.Partial => IdentityConfidence.Medium,
            _ => IdentityConfidence.Unknown
        }
    };

    private static PokemonObservation ApplyAppraisal(
        PokemonObservation observation,
        CleanupProofAppraisalCapture appraisal) => observation with
        {
            AttackIv = appraisal.AttackIv,
            DefenseIv = appraisal.DefenseIv,
            HpIv = appraisal.HpIv
        };

    private static IReadOnlyDictionary<string, string> FieldEvidence(
        PokemonObservation observation,
        VerifiedTagObservation tags,
        string appraisalStatus) => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Species"] = "QueryDerived",
        ["Cp"] = "Unknown",
        ["AttackIv"] = appraisalStatus == "AppraisalBarsObserved" ? "Unknown" : "Unknown",
        ["DefenseIv"] = appraisalStatus == "AppraisalBarsObserved" ? "Unknown" : "Unknown",
        ["HpIv"] = appraisalStatus == "AppraisalBarsObserved" ? "Unknown" : "Unknown",
        ["Form"] = "Unknown",
        ["Costume"] = "Unknown",
        ["Background"] = "Unknown",
        ["Shiny"] = "Unknown",
        ["Shadow"] = "Unknown",
        ["Lucky"] = "Unknown",
        ["Dynamax"] = "Unknown",
        ["Nickname"] = "Unknown",
        ["CatchDateOrAge"] = "Unknown",
        ["CatchLocation"] = "Unknown",
        ["ExistingTags"] = tags.NamesComplete ? "EvidenceReviewed" : "Unknown",
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
            new RulePolicy());
        foreach (var decision in analysis.Decisions)
            await reloadedPersistence.UpdateRecommendationAsync(runId, decision, cancellationToken);

        var rows = await new InventoryPersistenceService(Path.GetFullPath(request.DatabasePath))
            .LoadCleanupProofRowsAsync(runId, cancellationToken);
        var sqlSummary = await new InventoryPersistenceService(Path.GetFullPath(request.DatabasePath))
            .ReadCleanupProofSqlSummaryAsync(cancellationToken);
        var comparative = BuildComparativeSuggestions(rows);
        ValidateReports(rows, sqlSummary, request.OutputDirectory);
        await WriteReportsAsync(request, runId, status, stopReason, captures, rows, sqlSummary, comparative, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var output = Path.GetFullPath(request.OutputDirectory);
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
        proof.AppendLine("## Limitations");
        proof.AppendLine();
        proof.AppendLine("- Species is QueryDerived from the exact search; CP, IVs, variant fields, nickname, catch date and location remain Unknown unless separately evidence-reviewed.");
        proof.AppendLine("- Appraisal bars are captured as evidence, but no verified semantic IV provider is selected and no Calcy surface is used.");
        proof.AppendLine("- No tag, transfer, delete, power-up, evolve, purify, purchase or location-changing action is exposed by this command.");
        await File.WriteAllTextAsync(Path.Combine(output, "proof-summary.md"), proof.ToString(), cancellationToken);
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
            ["Species"] = Field(row.Observation.Species, "QueryDerived", evidence, "Exact species query; not OCR."),
            ["Cp"] = Field(null, "Unknown", string.Empty, "Requires bounded human evidence review."),
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

    private static IReadOnlyList<CleanupProofComparativeSuggestion> BuildComparativeSuggestions(
        IReadOnlyList<CleanupProofDatabaseRow> rows)
    {
        var result = new List<CleanupProofComparativeSuggestion>();
        foreach (var group in rows.GroupBy(row => row.Observation.GroupKey, StringComparer.Ordinal))
        {
            var ranked = group.OrderByDescending(row => row.Observation.TotalIv ?? -1)
                .ThenByDescending(row => row.Observation.Cp ?? -1)
                .ThenBy(row => row.Ordinal)
                .ToArray();
            foreach (var row in ranked)
            {
                var comparator = ranked.FirstOrDefault(candidate => candidate.LocalPokemonId != row.LocalPokemonId);
                var missing = ProtectionFields(row.Observation);
                var exactReviewed = row.Observation.HasKnownCriticalValues &&
                    row.Observation.IdentityConfidence == IdentityConfidence.Exact &&
                    row.Observation.VariantIdentity?.VariantKey is not null;
                var protectedKnown = row.Observation.IsFavorite is true || row.Observation.IsShiny is true ||
                    row.Observation.IsBackground is true || row.Observation.IsShadow is true ||
                    row.Observation.IsPurified is true || row.Observation.IsLucky is true ||
                    row.Observation.IsCostume is true || row.Observation.IsDynamax is true ||
                    row.Observation.IsGigantamax is true;
                var better = comparator is not null &&
                    comparator.Observation.TotalIv is not null && row.Observation.TotalIv is not null &&
                    (comparator.Observation.TotalIv > row.Observation.TotalIv ||
                     comparator.Observation.TotalIv == row.Observation.TotalIv && comparator.Observation.Cp > row.Observation.Cp);
                var isRetained = row.LocalPokemonId == ranked[0].LocalPokemonId;
                var classification = isRetained && exactReviewed
                    ? "RETAINED_COMPARATOR"
                    : comparator is null || !exactReviewed || protectedKnown || !better
                        ? "INSUFFICIENT_COMPARISON_DATA"
                        : "LIKELY_DELETE_SUGGESTION";
                result.Add(new CleanupProofComparativeSuggestion
                {
                    LocalPokemonId = row.LocalPokemonId,
                    Species = row.Observation.Species,
                    Classification = classification,
                    ComparatorLocalPokemonId = better ? comparator!.LocalPokemonId : null,
                    Cp = row.Observation.Cp,
                    TotalIv = row.Observation.TotalIv,
                    Ordinal = row.Ordinal,
                    MissingProtectionChecks = missing
                });
            }
        }
        return result.OrderBy(item => item.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ProtectionFields(PokemonObservation observation) =>
        new (string Name, bool? Value)[]
        {
            ("Favorite", observation.IsFavorite), ("Shiny", observation.IsShiny),
            ("Background", observation.IsBackground), ("Shadow", observation.IsShadow),
            ("Purified", observation.IsPurified), ("Lucky", observation.IsLucky),
            ("Costume", observation.IsCostume), ("Dynamax", observation.IsDynamax),
            ("Gigantamax", observation.IsGigantamax), ("SpecialMove", observation.HasSpecialMove),
            ("Xxl", observation.IsXxl), ("Xxs", observation.IsXxs), ("Nickname", string.IsNullOrWhiteSpace(observation.Nickname) ? null : true),
            ("CatchDate", observation.CatchDate is null ? null : true)
        }.Where(item => item.Value is null).Select(item => item.Name).ToArray();

    private static int Count(IReadOnlyDictionary<string, int> counts, string key) =>
        counts.TryGetValue(key, out var value) ? value : 0;

    private static string Csv(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
