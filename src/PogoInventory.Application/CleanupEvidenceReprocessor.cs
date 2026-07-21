using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Analysis;
using PogoInventory.Core.Policy;
using PogoInventory.HeaderText;
using PogoInventory.Persistence;

namespace PogoInventory.Application;

/// <summary>Inputs for the offline <c>analyze-cleanup-evidence</c> reprocess command.</summary>
public sealed record CleanupEvidenceReprocessRequest
{
    /// <summary>An existing cleanup-proof.sqlite database. Never modified.</summary>
    public required string SourceDatabasePath { get; init; }

    /// <summary>Directory containing the stored evidence screenshots (or a superset of them).</summary>
    public required string EvidenceRoot { get; init; }

    /// <summary>Directory the new database copy and report set are written into.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Species reference used for query classification and header OCR validation.</summary>
    public ISpeciesReference? SpeciesReference { get; init; }

    /// <summary>Optional header OCR analyzer. When null, species/CP are not re-derived from evidence.</summary>
    public PokemonHeaderAnalyzer? HeaderAnalyzer { get; init; }

    /// <summary>Rule policy used to rerun the recommendation analysis. Defaults to built-in defaults when null.</summary>
    public RulePolicy? Policy { get; init; }
}

public sealed record CleanupEvidenceCoverageSummary
{
    public required string NewDatabasePath { get; init; }
    public required int TotalRows { get; init; }
    public required int SpeciesExtracted { get; init; }
    public required int SpeciesUnknown { get; init; }
    public required int CpExtracted { get; init; }
    public required int CpUnknown { get; init; }
    public required int IvComplete { get; init; }
    public required int RowsWithQueryAsSpecies { get; init; }
    public required int ComparableGroups { get; init; }
}

/// <summary>
/// Offline reprocess of an existing cleanup-proof.sqlite database: reruns
/// header OCR (species/CP/nickname) and IV consensus against the stored
/// evidence screenshots, writes a NEW sqlite copy (the original is never
/// modified), reruns the recommendation and comparative analysis and
/// regenerates the standard report set plus a species/CP coverage summary.
///
/// Lives in this (net8.0, Windows-independent) project rather than in
/// PogoInventory.Cli so that it can be exercised directly by
/// PogoInventory.SelfTest with a synthetic database and a scripted
/// <c>ITextRecognizer</c>, without requiring the Windows-only
/// PogoInventory.HeaderOcr / PogoInventory.Cli TFM.
/// </summary>
public static class CleanupEvidenceReprocessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<CleanupEvidenceCoverageSummary> ReprocessAsync(
        CleanupEvidenceReprocessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceDatabasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.EvidenceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);

        var sourceDatabase = Path.GetFullPath(request.SourceDatabasePath);
        var evidenceRoot = Path.GetFullPath(request.EvidenceRoot);
        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        if (!File.Exists(sourceDatabase))
            throw new FileNotFoundException($"Source database not found: {sourceDatabase}", sourceDatabase);
        Directory.CreateDirectory(outputDirectory);

        var speciesReference = request.SpeciesReference ?? new StaticSpeciesReference(Array.Empty<string>());
        var policy = request.Policy ?? new RulePolicy();

        var newDatabasePath = Path.Combine(outputDirectory, "cleanup-proof.sqlite");
        if (File.Exists(newDatabasePath)) File.Delete(newDatabasePath);
        File.Copy(sourceDatabase, newDatabasePath);

        var writeService = new InventoryPersistenceService(newDatabasePath);
        var runIds = await writeService.LoadAllCleanupRunIdsAsync(cancellationToken);

        var totalRows = 0;
        var speciesExtracted = 0;
        var speciesUnknown = 0;
        var cpExtracted = 0;
        var cpUnknown = 0;
        var ivComplete = 0;
        var queryAsSpeciesRows = 0;

        foreach (var runId in runIds)
        {
            var searchQuery = await writeService.ReadCleanupRunSearchQueryAsync(runId, cancellationToken);
            var classification = SearchQueryClassifier.Classify(searchQuery, speciesReference);
            var rows = await writeService.LoadCleanupProofRowsAsync(runId, cancellationToken);
            foreach (var row in rows)
            {
                totalRows++;
                var resolvedPaths = row.ScreenshotPaths
                    .Select(path => ResolveEvidencePath(path, evidenceRoot))
                    .Where(path => path is not null)
                    .Select(path => path!)
                    .ToArray();

                PokemonHeaderConsensusResult? headerConsensus = null;
                if (request.HeaderAnalyzer is not null && resolvedPaths.Length >= 2)
                {
                    var screen = row.Ordinal == 1 ? HeaderScreenType.PokemonDetails : HeaderScreenType.AppraisalBars;
                    headerConsensus = await AnalyzeHeaderFramesAsync(
                        request.HeaderAnalyzer, resolvedPaths, screen, cancellationToken);
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

                // Defensive regression guard: the raw broad-filter query must
                // never end up persisted as the species (the original defect).
                if (classification.Kind == SearchQueryKind.BroadFilter &&
                    string.Equals(species, searchQuery, StringComparison.OrdinalIgnoreCase))
                {
                    queryAsSpeciesRows++;
                    species = "Unknown";
                    speciesEvidence = "Unknown";
                }

                var cp = row.Observation.Cp;
                var cpEvidence = row.FieldEvidenceSources.TryGetValue("Cp", out var existingCpEvidence)
                    ? existingCpEvidence
                    : "Unknown";
                if (headerConsensus?.Cp is not null)
                {
                    cp = headerConsensus.Cp;
                    cpEvidence = "Automated";
                }

                var nickname = row.Observation.Nickname;
                if (headerConsensus is not null && headerConsensus.Species is null)
                {
                    var candidate = headerConsensus.Frames
                        .Select(frame => frame.Nickname)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .GroupBy(text => text, StringComparer.Ordinal)
                        .Where(group => group.Count() >= 2)
                        .OrderByDescending(group => group.Count())
                        .Select(group => group.Key)
                        .FirstOrDefault();
                    if (candidate is not null) nickname = candidate;
                }

                var updatedObservation = row.Observation with { Species = species, Cp = cp, Nickname = nickname };
                var fieldEvidence = new Dictionary<string, string>(row.FieldEvidenceSources, StringComparer.Ordinal)
                {
                    ["Species"] = speciesEvidence,
                    ["Cp"] = cpEvidence
                };

                await writeService.ReprocessCleanupSemanticsAsync(
                    runId, row.LocalPokemonId, updatedObservation, fieldEvidence, cancellationToken);

                if (!string.Equals(species, "Unknown", StringComparison.Ordinal)) speciesExtracted++;
                else speciesUnknown++;
                if (cp is not null) cpExtracted++;
                else cpUnknown++;
                if (updatedObservation.AttackIv is not null && updatedObservation.DefenseIv is not null &&
                    updatedObservation.HpIv is not null) ivComplete++;
            }
        }

        // Rerun recommendation + comparative analysis from a fresh connection,
        // exactly like the live runner does after a run completes.
        var reloadedService = new InventoryPersistenceService(newDatabasePath);
        var allRows = new List<CleanupProofDatabaseRow>();
        foreach (var runId in runIds)
            allRows.AddRange(await reloadedService.LoadCleanupProofRowsAsync(runId, cancellationToken));

        var analysis = new InventoryAnalyzer().Analyze(allRows.Select(row => row.Observation).ToArray(), policy);
        foreach (var decision in analysis.Decisions)
        {
            var separatorIndex = decision.ExternalKey.LastIndexOf(':');
            var originRunId = separatorIndex < 0 ? decision.ExternalKey : decision.ExternalKey[..separatorIndex];
            await reloadedService.UpdateRecommendationAsync(originRunId, decision, cancellationToken);
        }

        var finalRows = new List<CleanupProofDatabaseRow>();
        foreach (var runId in runIds)
            finalRows.AddRange(await new InventoryPersistenceService(newDatabasePath).LoadCleanupProofRowsAsync(runId, cancellationToken));
        var sqlSummary = await new InventoryPersistenceService(newDatabasePath).ReadCleanupProofSqlSummaryAsync(cancellationToken);
        var comparative = CleanupProofComparativeAnalyzer.BuildComparativeSuggestions(finalRows);

        await WriteReportsAsync(outputDirectory, runIds, finalRows, sqlSummary, comparative, cancellationToken);

        var comparableGroups = finalRows
            .Where(row => row.SemanticKeyCompleteness == "Comparable")
            .Select(row => row.Observation.GroupKey)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var summary = new CleanupEvidenceCoverageSummary
        {
            NewDatabasePath = newDatabasePath,
            TotalRows = totalRows,
            SpeciesExtracted = speciesExtracted,
            SpeciesUnknown = speciesUnknown,
            CpExtracted = cpExtracted,
            CpUnknown = cpUnknown,
            IvComplete = ivComplete,
            RowsWithQueryAsSpecies = queryAsSpeciesRows,
            ComparableGroups = comparableGroups
        };
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "species-cp-coverage.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        return summary;
    }

    /// <summary>
    /// Resolves a screenshot path recorded in an existing cleanup-proof
    /// database against a (possibly different-machine) evidence root: tries
    /// the stored path as-is, then joined under the evidence root, then
    /// joined by filename only, then a recursive filename search under the
    /// evidence root. Returns null (never guessed) when no candidate exists
    /// on disk.
    /// </summary>
    public static string? ResolveEvidencePath(string storedPath, string evidenceRoot)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return null;
        if (File.Exists(storedPath)) return storedPath;

        var trimmed = storedPath.TrimStart('\\', '/');
        var joined = Path.Combine(evidenceRoot, trimmed);
        if (File.Exists(joined)) return joined;

        var byFileName = Path.Combine(evidenceRoot, Path.GetFileName(storedPath));
        if (File.Exists(byFileName)) return byFileName;

        if (!Directory.Exists(evidenceRoot)) return null;
        return Directory.EnumerateFiles(evidenceRoot, Path.GetFileName(storedPath), SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private static async Task<PokemonHeaderConsensusResult?> AnalyzeHeaderFramesAsync(
        PokemonHeaderAnalyzer analyzer,
        IReadOnlyList<string> screenshotPaths,
        HeaderScreenType screen,
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
            frames.Add(await analyzer.AnalyzeAsync(bytes, screen, cancellationToken));
        }
        return frames.Count < 2 ? null : PokemonHeaderAnalyzer.Consensus(frames);
    }

    /// <summary>
    /// Mirrors <c>CleanupProofRunner.WriteReportsAsync</c>'s standard report
    /// set (recommendations.csv/.md, comparative-cleanup-suggestions.csv,
    /// group-summary.json, db-roundtrip.json) for the reprocessed database,
    /// which spans potentially multiple existing runs rather than a single
    /// fresh one.
    /// </summary>
    private static async Task WriteReportsAsync(
        string outputDirectory,
        IReadOnlyList<string> runIds,
        IReadOnlyList<CleanupProofDatabaseRow> rows,
        CleanupProofSqlSummary sql,
        IReadOnlyList<CleanupProofComparativeSuggestion> comparative,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "db-roundtrip.json"),
            JsonSerializer.Serialize(new
            {
                runIds,
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
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "recommendations.csv"), csv.ToString(), cancellationToken);

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
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "comparative-cleanup-suggestions.csv"), comparativeCsv.ToString(), cancellationToken);

        var markdown = new StringBuilder("# Recommendations (reprocessed)\n\n| LocalPokemonId | Ordinal | Species | CP | IV | Status | Recommendation | Reason | Comparator | Evidence |\n|---|---:|---|---:|---:|---|---|---|---|---|\n");
        foreach (var row in rows)
        {
            markdown.AppendLine($"| {row.LocalPokemonId} | {row.Ordinal} | {row.Observation.Species} | {row.Observation.Cp?.ToString() ?? "Unknown"} | {row.Observation.TotalIv?.ToString() ?? "Unknown"} | {row.ObservationStatus} | {row.CurrentRecommendation} | {row.RecommendationReason.Replace('|', '/')} | {row.ComparatorLocalPokemonId ?? ""} | {row.ScreenshotPaths.FirstOrDefault() ?? ""} |");
        }
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "recommendations.md"), markdown.ToString(), cancellationToken);

        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "group-summary.json"),
            JsonSerializer.Serialize(rows.GroupBy(row => row.Observation.GroupKey, StringComparer.Ordinal)
                .Select(group => new
                {
                    groupKey = group.Key,
                    count = group.Count(),
                    recommendations = group.GroupBy(row => row.CurrentRecommendation, StringComparer.Ordinal)
                        .ToDictionary(item => item.Key, item => item.Count(), StringComparer.Ordinal),
                    localPokemonIds = group.Select(row => row.LocalPokemonId).ToArray()
                }), JsonOptions), cancellationToken);
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
