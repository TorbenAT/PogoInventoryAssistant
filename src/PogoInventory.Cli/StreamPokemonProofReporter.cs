using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PogoInventory.Semantics;

internal enum StreamProofRunStatus
{
    Running,
    CompletedRequestedItems,
    FilterExhausted,
    SafeStopped,
    Failed
}

internal sealed record StreamProofRecord(
    string RunId,
    int Ordinal,
    DateTimeOffset CapturedAtUtc,
    string ItemFingerprint,
    PokemonItemSemanticResult Result,
    PokemonItemSemanticResult ProgressionResult,
    IReadOnlyList<string> RawOcr,
    double OcrMilliseconds,
    double IvMilliseconds,
    double ItemMilliseconds,
    IReadOnlyList<long> FrameIds,
    IReadOnlyList<DateTimeOffset> FrameTimestampsUtc,
    IReadOnlyList<string> EvidenceHashes,
    IReadOnlyList<string> EvidenceFiles,
    string FullFramePath,
    string HeaderCropPath,
    string AppraisalCropPath,
    string AttackCropPath,
    string DefenseCropPath,
    string HpCropPath,
    double SettlingMilliseconds,
    double? SwipeToStableMilliseconds,
    IReadOnlyDictionary<string, int> GateRejectionCounts);

internal sealed record StreamProofHandoff(
    int Ordinal,
    int FramesObserved,
    int FramesRejectedStale,
    int FramesRejectedWrongState,
    IReadOnlyDictionary<string, int> ReasonCounts,
    int StableQualifyingFrames,
    double ElapsedMilliseconds,
    string? PreviousFingerprint,
    string? CurrentFingerprint,
    string? NewFingerprint,
    long BarrierAfterFrameId,
    DateTimeOffset ActionStartedUtc,
    DateTimeOffset ActionCompletedUtc,
    string? StopReason);

internal sealed record StreamProofMetrics(
    long FramesPublished,
    long FramesDecoded,
    long FramesEvicted,
    int PeakQueueDepth,
    long TcpBytesReceived,
    long EncodedPacketsReceived,
    int? StreamWidth,
    int? StreamHeight,
    long LeasesAtShutdown,
    bool SourceRunning,
    string TransportLifecycle,
    int? FfmpegExitCode,
    string Shutdown,
    string? StreamError);

internal sealed record StreamProofContext(
    string RunId,
    string Commit,
    string Device,
    string InventoryQuery,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndedAtUtc,
    int RequestedItems,
    StreamProofRunStatus RunStatus,
    string? StopReason,
    int SetupInputs,
    int ProgressionSwipes,
    int OtherNamedInputs,
    int SemanticInputs,
    StreamProofMetrics Metrics);

internal sealed record StreamProofIntegrityCheck(string Name, bool Passed, string Detail);

internal sealed record StreamProofIntegrity(
    string RunId,
    string IntegrityStatus,
    int HtmlRows,
    int JsonlRows,
    int CsvRows,
    int SummaryCompletedItems,
    int UniqueItemFingerprints,
    int VerifiedProgressions,
    int DistinctSemanticEvidenceFrames,
    int ItemsWithFullEvidence,
    int BrokenImageLinks,
    IReadOnlyList<StreamProofIntegrityCheck> Checks);

internal static class StreamPokemonProofReporter
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal static string BuildItemFingerprint(
        string visualFingerprint,
        PokemonItemSemanticResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visualFingerprint);
        ArgumentNullException.ThrowIfNull(result);

        static string Known<T>(SemanticFieldResult<T> field) =>
            field.Status == SemanticFieldStatus.Known && field.Value is not null
                ? Convert.ToString(
                    field.Value,
                    CultureInfo.InvariantCulture) ?? "Unknown"
                : "Unknown";

        var semanticIdentity = string.Join(
            ";",
            $"Species={Known(result.Species).Trim().ToUpperInvariant()}",
            $"CP={Known(result.Cp)}",
            $"AttackIV={Known(result.AttackIv)}",
            $"DefenseIV={Known(result.DefenseIv)}",
            $"HpIV={Known(result.HpIv)}");
        var semanticHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(semanticIdentity)))[..16]
            .ToLowerInvariant();
        return $"{visualFingerprint}|Semantic:{semanticHash}";
    }

    public static async Task WriteLiveAsync(
        string output,
        StreamProofContext context,
        IReadOnlyList<StreamProofRecord> records,
        IReadOnlyList<StreamProofHandoff> handoffs,
        CancellationToken cancellationToken)
    {
        await WriteDataArtifactsAsync(output, context, records, handoffs, "Pending", cancellationToken);
        await AtomicWriteAsync(
            Path.Combine(output, "live.html"),
            RenderHtml(context, records, "Pending", live: true),
            cancellationToken);
    }

    public static async Task<StreamProofIntegrity> WriteFinalAsync(
        string output,
        StreamProofContext context,
        IReadOnlyList<StreamProofRecord> records,
        IReadOnlyList<StreamProofHandoff> handoffs,
        CancellationToken cancellationToken)
    {
        await WriteDataArtifactsAsync(output, context, records, handoffs, "Pending", cancellationToken);
        await AtomicWriteAsync(
            Path.Combine(output, "index.html"),
            RenderHtml(context, records, "Pending", live: false),
            cancellationToken);

        var integrity = Validate(output, context, records, handoffs);
        await AtomicWriteAsync(
            Path.Combine(output, "integrity.json"),
            JsonSerializer.Serialize(integrity, JsonOptions),
            cancellationToken);
        await AtomicWriteAsync(
            Path.Combine(output, "integrity.md"),
            RenderIntegrityMarkdown(integrity),
            cancellationToken);
        await WriteDataArtifactsAsync(
            output, context, records, handoffs, integrity.IntegrityStatus, cancellationToken);
        await AtomicWriteAsync(
            Path.Combine(output, "index.html"),
            RenderHtml(context, records, integrity.IntegrityStatus, live: false),
            cancellationToken);
        return integrity;
    }

    private static async Task WriteDataArtifactsAsync(
        string output,
        StreamProofContext context,
        IReadOnlyList<StreamProofRecord> records,
        IReadOnlyList<StreamProofHandoff> handoffs,
        string integrityStatus,
        CancellationToken cancellationToken)
    {
        var known = new
        {
            Species = CountStatus(records, x => x.Result.Species.Status),
            CP = CountStatus(records, x => x.Result.Cp.Status),
            AttackIV = CountStatus(records, x => x.Result.AttackIv.Status),
            DefenseIV = CountStatus(records, x => x.Result.DefenseIv.Status),
            HpIV = CountStatus(records, x => x.Result.HpIv.Status)
        };
        var summary = new
        {
            context.RunId,
            context.Commit,
            context.Device,
            context.InventoryQuery,
            context.StartedAtUtc,
            context.EndedAtUtc,
            context.RequestedItems,
            CompletedItems = records.Count,
            context.RunStatus,
            context.StopReason,
            CompleteItems = records.Count(x => x.Result.IsComplete),
            PartialItems = records.Count(x => !x.Result.IsComplete),
            Coverage = new
            {
                SpeciesKnownPercent = Percent(records.Count(x => IsKnown(x.Result.Species.Status)), records.Count),
                CpKnownPercent = Percent(records.Count(x => IsKnown(x.Result.Cp.Status)), records.Count),
                FullIvKnownPercent = Percent(records.Count(HasKnownIv), records.Count),
                AllFiveKnownPercent = Percent(records.Count(x => x.Result.IsComplete), records.Count)
            },
            FieldStatusCounts = known,
            SetupInputCommandsSent = context.SetupInputs,
            ProgressionSwipesSent = context.ProgressionSwipes,
            OtherNamedInputCommandsSent = context.OtherNamedInputs,
            SemanticInputCommandsSent = context.SemanticInputs,
            IntegrityStatus = integrityStatus,
            Performance = new
            {
                Item = Percentiles(records.Select(x => x.ItemMilliseconds)),
                Settling = Percentiles(records.Select(x => x.SettlingMilliseconds)),
                SwipeToStable = Percentiles(records.Where(x => x.SwipeToStableMilliseconds.HasValue)
                    .Select(x => x.SwipeToStableMilliseconds!.Value)),
                OCR = Percentiles(records.Select(x => x.OcrMilliseconds)),
                IV = Percentiles(records.Select(x => x.IvMilliseconds))
            },
            GateRejections = AggregateGateRejections(handoffs),
            FramesRejectedStale = handoffs.Sum(x => x.FramesRejectedStale),
            FramesRejectedWrongState = handoffs.Sum(x => x.FramesRejectedWrongState),
            FramesDropped = context.Metrics.FramesEvicted,
            Stream = context.Metrics,
            Records = records,
            Handoffs = handoffs
        };
        await AtomicWriteAsync(
            Path.Combine(output, "summary.json"),
            JsonSerializer.Serialize(summary, JsonOptions),
            cancellationToken);
        await AtomicWriteAsync(Path.Combine(output, "items.csv"), RenderCsv(records), cancellationToken);
    }

    internal static StreamProofIntegrity Validate(
        string output,
        StreamProofContext context,
        IReadOnlyList<StreamProofRecord> records,
        IReadOnlyList<StreamProofHandoff> handoffs)
    {
        var htmlPath = Path.Combine(output, "index.html");
        var html = File.Exists(htmlPath) ? File.ReadAllText(htmlPath) : string.Empty;
        var htmlRows = CountOccurrences(html, "data-item-row=");
        var jsonlPath = Path.Combine(output, "items.jsonl");
        var jsonlRecords = ReadJsonl(jsonlPath);
        var jsonlRows = jsonlRecords.Count;
        var csvPath = Path.Combine(output, "items.csv");
        var csvRecords = ReadCsvIdentities(csvPath);
        var csvRows = csvRecords.Count;
        var htmlRecords = ReadHtmlIdentities(html);
        var summary = ReadSummaryIdentity(Path.Combine(output, "summary.json"));
        var fingerprints = records.Select(x => x.ItemFingerprint).ToArray();
        var uniqueFingerprints = fingerprints.Distinct(StringComparer.Ordinal).Count();
        var distinctFrames = records.SelectMany(x => x.FrameIds).Distinct().Count();
        var alignedHandoffs = records.Count == handoffs.Count &&
            records.Select(x => x.Ordinal).SequenceEqual(handoffs.Select(x => x.Ordinal));
        var verifiedProgressions = 0;
        var progressionAlignment = alignedHandoffs;
        var freshEvidence = alignedHandoffs;
        for (var index = 0; alignedHandoffs && index < records.Count; index++)
        {
            var record = records[index];
            var handoff = handoffs[index];
            var expectedPrevious = index == 0 ? null : records[index - 1].ItemFingerprint;
            var currentMatches =
                string.Equals(handoff.CurrentFingerprint, record.ItemFingerprint, StringComparison.Ordinal) &&
                (index == 0
                    ? handoff.NewFingerprint is null
                    : string.Equals(handoff.NewFingerprint, record.ItemFingerprint, StringComparison.Ordinal));
            var previousMatches = string.Equals(
                handoff.PreviousFingerprint, expectedPrevious, StringComparison.Ordinal);
            var semanticFallbackValid =
                !handoff.ReasonCounts.ContainsKey("SemanticProgressionProofRequired") ||
                handoff.ReasonCounts.Keys.Any(x =>
                    x.StartsWith("SemanticProgression:", StringComparison.Ordinal));
            var changed = index == 0 ||
                !string.Equals(expectedPrevious, record.ItemFingerprint, StringComparison.Ordinal);
            var progressionValid =
                currentMatches && previousMatches && semanticFallbackValid && changed;
            progressionAlignment &= progressionValid;
            if (index > 0 && progressionValid)
            {
                verifiedProgressions++;
            }

            freshEvidence &= record.FrameIds.Count == record.FrameTimestampsUtc.Count &&
                record.FrameIds.All(x => x > handoff.BarrierAfterFrameId) &&
                record.FrameTimestampsUtc.All(x => x > handoff.ActionCompletedUtc);
        }

        var allImagePaths = records.SelectMany(ImagePaths)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var brokenImages = allImagePaths.Count(path =>
            !TryResolveRelative(output, path, out var resolved) || !File.Exists(resolved));
        var htmlLinks = Regex.Matches(
                html, """(?:href|src)=["']([^"']+)["']""",
                RegexOptions.CultureInvariant)
            .Select(x => WebUtility.HtmlDecode(x.Groups[1].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var brokenHtmlLinks = htmlLinks.Count(path =>
            !TryResolveRelative(output, path, out var resolved) || !File.Exists(resolved));
        var evidenceHashesMatch = records.All(record =>
            record.EvidenceFiles.Count == record.EvidenceHashes.Count &&
            record.EvidenceFiles.Zip(record.EvidenceHashes, (path, hash) =>
                    TryResolveRelative(output, path, out var resolved) &&
                    File.Exists(resolved) &&
                    string.Equals(
                        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(resolved)))
                            .ToLowerInvariant(),
                        hash,
                        StringComparison.Ordinal))
                .All(x => x));
        var fullEvidence = records.Count(x =>
            x.FrameIds.Count >= 3 &&
            x.FrameIds.Distinct().Count() == x.FrameIds.Count &&
            x.FrameTimestampsUtc.Count == x.FrameIds.Count &&
            x.FrameTimestampsUtc.Distinct().Count() ==
                x.FrameTimestampsUtc.Count &&
            x.EvidenceHashes.Count == x.FrameIds.Count &&
            x.EvidenceHashes.Distinct(StringComparer.Ordinal).Count() ==
                x.EvidenceHashes.Count &&
            x.EvidenceFiles.Count == x.FrameIds.Count &&
            x.EvidenceFiles.Distinct(StringComparer.Ordinal).Count() ==
                x.EvidenceFiles.Count &&
            ImagePaths(x).Count() == x.EvidenceFiles.Count + 5);
        var recordTimestampsMonotone =
            records.Zip(records.Skip(1), (a, b) => a.CapturedAtUtc < b.CapturedAtUtc)
                .All(x => x) &&
            records.All(x => x.FrameTimestampsUtc
                .Zip(x.FrameTimestampsUtc.Skip(1), (a, b) => a < b).All(y => y));
        var artifactIdentitiesMatch =
            jsonlRecords.Count == records.Count &&
            csvRecords.Count == records.Count &&
            htmlRecords.Count == records.Count &&
            records.Select(Identity).SequenceEqual(jsonlRecords.Select(Identity)) &&
            records.Select(Identity).SequenceEqual(csvRecords) &&
            records.Select(Identity).SequenceEqual(htmlRecords);
        var summaryMatches = summary is not null &&
            summary.Value.RunId == context.RunId &&
            summary.Value.CompletedItems == records.Count &&
            summary.Value.RequestedItems == context.RequestedItems &&
            summary.Value.RunStatus == context.RunStatus.ToString();
        var speciesKnown = records.Count(x => IsKnown(x.Result.Species.Status));
        var cpKnown = records.Count(x => IsKnown(x.Result.Cp.Status));
        var ivKnown = records.Count(HasKnownIv);
        var complete = records.Count(x => x.Result.IsComplete);
        var primaryStatuses = new Func<StreamProofRecord, SemanticFieldStatus>[]
        {
            x => x.Result.Species.Status,
            x => x.Result.Cp.Status,
            x => x.Result.AttackIv.Status,
            x => x.Result.DefenseIv.Status,
            x => x.Result.HpIv.Status
        };
        var conflictMaximum = primaryStatuses
            .Select(selector => Percent(
                records.Count(x => selector(x) == SemanticFieldStatus.Conflicting),
                records.Count))
            .DefaultIfEmpty(0)
            .Max();
        var checks = new List<StreamProofIntegrityCheck>
        {
            Check("RequestedItemsIs120", context.RequestedItems == 120,
                $"{context.RequestedItems}"),
            Check("CompletedRequestedItems",
                context.RunStatus == StreamProofRunStatus.CompletedRequestedItems &&
                records.Count == context.RequestedItems && records.Count >= 100,
                $"{context.RunStatus}; {records.Count}/{context.RequestedItems}"),
            Check("MatchingArtifactCounts",
                htmlRows == records.Count && jsonlRows == records.Count && csvRows == records.Count,
                $"HTML={htmlRows}; JSONL={jsonlRows}; CSV={csvRows}; records={records.Count}"),
            Check("ArtifactContentsMatch", artifactIdentitiesMatch,
                $"HTML={htmlRecords.Count}; JSONL={jsonlRecords.Count}; CSV={csvRecords.Count}"),
            Check("SummaryMatchesRun", summaryMatches,
                summary is null ? "unreadable" :
                $"{summary.Value.RunId}; {summary.Value.CompletedItems}/{summary.Value.RequestedItems}; {summary.Value.RunStatus}"),
            Check("RunIdConsistent",
                records.All(x => x.RunId == context.RunId) &&
                jsonlRecords.All(x => x.RunId == context.RunId) &&
                csvRecords.All(x => x.RunId == context.RunId) &&
                htmlRecords.All(x => x.RunId == context.RunId),
                context.RunId),
            Check("ContinuousOrdinals",
                records.Select(x => x.Ordinal).SequenceEqual(Enumerable.Range(1, records.Count)),
                records.Count == 0 ? "no records" : $"1..{records[^1].Ordinal}"),
            Check("UniqueItemFingerprints",
                uniqueFingerprints == records.Count,
                $"{uniqueFingerprints}/{records.Count}"),
            Check("ThreeDistinctEvidenceFramesPerItem",
                fullEvidence == records.Count,
                $"{fullEvidence}/{records.Count}"),
            Check("DistinctSemanticEvidenceFrames",
                distinctFrames >= records.Count * 3,
                $"{distinctFrames}"),
            Check("MonotoneTimestamps", recordTimestampsMonotone,
                "strictly increasing"),
            Check("FreshPostActionEvidence", freshEvidence,
                $"{records.Count} aligned record windows"),
            Check("ProgressionAlignmentAndSemanticFallback", progressionAlignment,
                $"{handoffs.Count} aligned handoffs"),
            Check("VerifiedProgressions",
                records.Count == 0 ? verifiedProgressions == 0 :
                    verifiedProgressions == records.Count - 1,
                $"{verifiedProgressions}"),
            Check("ExactSwipeCount",
                context.ProgressionSwipes == Math.Max(0, records.Count - 1),
                $"{context.ProgressionSwipes}"),
            Check("AllImagesExist", brokenImages == 0, $"broken={brokenImages}"),
            Check("AllHtmlLinksResolve", brokenHtmlLinks == 0,
                $"links={htmlLinks.Length}; broken={brokenHtmlLinks}"),
            Check("EvidenceHashesMatchFiles", evidenceHashesMatch, "SHA-256"),
            Check("RunIdInHtml", html.Contains(context.RunId, StringComparison.Ordinal), context.RunId),
            Check("SpeciesCoverageAtLeast90",
                Percent(speciesKnown, records.Count) >= 90,
                $"{Percent(speciesKnown, records.Count):F2}%"),
            Check("CpCoverageAtLeast75",
                Percent(cpKnown, records.Count) >= 75,
                $"{Percent(cpKnown, records.Count):F2}%"),
            Check("IvCoverageAtLeast85",
                Percent(ivKnown, records.Count) >= 85,
                $"{Percent(ivKnown, records.Count):F2}%"),
            Check("CompleteCoverageAtLeast70",
                Percent(complete, records.Count) >= 70,
                $"{Percent(complete, records.Count):F2}%"),
            Check("PrimaryFieldConflictsAtMost2", conflictMaximum <= 2,
                $"{conflictMaximum:F2}% max"),
            Check("SemanticInputCommandsSent", context.SemanticInputs == 0,
                $"{context.SemanticInputs}"),
            Check("CleanShutdown",
                context.Metrics.Shutdown == "Clean" &&
                context.Metrics.LeasesAtShutdown == 0 &&
                !context.Metrics.SourceRunning &&
                context.Metrics.StreamError is null &&
                context.Metrics.TransportLifecycle is "Stopped" or "Disposed",
                $"{context.Metrics.Shutdown}; leases={context.Metrics.LeasesAtShutdown}; " +
                $"sourceRunning={context.Metrics.SourceRunning}; transport={context.Metrics.TransportLifecycle}; " +
                $"ffmpeg={context.Metrics.FfmpegExitCode?.ToString(CultureInfo.InvariantCulture) ?? "null"}")
        };
        var status = checks.All(x => x.Passed) ? "PASS" : "FAIL";
        return new(
            context.RunId, status, htmlRows, jsonlRows, csvRows, records.Count,
            uniqueFingerprints, verifiedProgressions, distinctFrames, fullEvidence,
            brokenImages, checks);
    }

    private static string RenderCsv(IReadOnlyList<StreamProofRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RunId,RunItem,TimestampUtc,ItemFingerprint,Species,SpeciesStatus,SpeciesConfidence,CP,CPStatus,CPConfidence,AttackIV,DefenseIV,HpIV,IVStatus,IVConfidence,ItemMs,SettlingMs,SwipeToStableMs,OCRMs,IVMs,FrameIds,FrameTimestampsUtc,EvidenceHashes,RawOCR");
        foreach (var item in records)
        {
            var ivStatus = HasKnownIv(item) ? SemanticFieldStatus.Known :
                new[] { item.Result.AttackIv.Status, item.Result.DefenseIv.Status, item.Result.HpIv.Status }
                    .Contains(SemanticFieldStatus.Conflicting)
                    ? SemanticFieldStatus.Conflicting : SemanticFieldStatus.Unknown;
            var ivConfidence = new[] { item.Result.AttackIv.Confidence, item.Result.DefenseIv.Confidence, item.Result.HpIv.Confidence }.Min();
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(item.RunId), Csv(item.Ordinal), Csv(item.CapturedAtUtc.ToString("O")),
                Csv(item.ItemFingerprint), Csv(Value(item.Result.Species.Value)),
                Csv(item.Result.Species.Status), Csv(item.Result.Species.Confidence),
                Csv(Value(item.Result.Cp.Value)), Csv(item.Result.Cp.Status),
                Csv(item.Result.Cp.Confidence), Csv(Value(item.Result.AttackIv.Value)),
                Csv(Value(item.Result.DefenseIv.Value)), Csv(Value(item.Result.HpIv.Value)),
                Csv(ivStatus), Csv(ivConfidence), Csv(item.ItemMilliseconds),
                Csv(item.SettlingMilliseconds), Csv(item.SwipeToStableMilliseconds),
                Csv(item.OcrMilliseconds), Csv(item.IvMilliseconds),
                Csv(string.Join("|", item.FrameIds)),
                Csv(string.Join("|", item.FrameTimestampsUtc.Select(x => x.ToString("O")))),
                Csv(string.Join("|", item.EvidenceHashes)),
                Csv(string.Join(" | ", item.RawOcr))
            }));
        }
        return builder.ToString();
    }

    private static string RenderHtml(
        StreamProofContext context,
        IReadOnlyList<StreamProofRecord> records,
        string integrityStatus,
        bool live)
    {
        var rows = string.Join(Environment.NewLine, records.Select(RenderRow));
        var refresh = live ? "<meta http-equiv='refresh' content='2'>" : string.Empty;
        var speciesKnown = records.Count(x => IsKnown(x.Result.Species.Status));
        var cpKnown = records.Count(x => IsKnown(x.Result.Cp.Status));
        var ivKnown = records.Count(HasKnownIv);
        var complete = records.Count(x => x.Result.IsComplete);
        var conflicts = records.Count(x => new[]
        {
            x.Result.Species.Status, x.Result.Cp.Status, x.Result.AttackIv.Status,
            x.Result.DefenseIv.Status, x.Result.HpIv.Status
        }.Contains(SemanticFieldStatus.Conflicting));
        var duration = context.EndedAtUtc is null
            ? DateTimeOffset.UtcNow - context.StartedAtUtc
            : context.EndedAtUtc.Value - context.StartedAtUtc;
        return $$"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8">{{refresh}}
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Pokémon stream proof {{H(context.RunId)}}</title>
<style>
body{font:14px system-ui,sans-serif;margin:20px;background:#f5f7fa;color:#17212b}
.summary,.controls{background:white;padding:14px;border-radius:10px;margin-bottom:14px;box-shadow:0 1px 4px #ccd}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(190px,1fr));gap:8px}
table{width:100%;border-collapse:collapse;background:white}th,td{padding:8px;border:1px solid #dbe2ea;vertical-align:top}th{position:sticky;top:0;background:#eaf0f6}
.thumb{width:180px;max-height:240px;object-fit:contain}.crop{max-width:260px;max-height:150px;object-fit:contain}
.Known{color:#087830}.Unknown{color:#805500}.Conflicting{color:#b00020}code{word-break:break-all}
button,select{margin-right:8px;padding:6px}details{max-width:760px}.hidden{display:none}
</style></head><body>
<h1>Pokémon stream proof</h1>
<section class="summary"><div class="grid">
<div><b>Run ID</b><br><code>{{H(context.RunId)}}</code></div>
<div><b>Commit</b><br><code>{{H(context.Commit)}}</code></div>
<div><b>Device</b><br>{{H(context.Device)}}</div>
<div><b>Inventory query</b><br><code>{{H(context.InventoryQuery)}}</code></div>
<div><b>Status</b><br>{{H(context.RunStatus)}} / {{H(context.StopReason ?? "none")}}</div>
<div><b>Requested / completed</b><br>{{context.RequestedItems}} / {{records.Count}}</div>
<div><b>Started / ended</b><br>{{H(context.StartedAtUtc.ToString("O"))}}<br>{{H(context.EndedAtUtc?.ToString("O") ?? "running")}}</div>
<div><b>Duration</b><br>{{H(duration)}}</div>
<div><b>Complete / partial</b><br>{{complete}} / {{records.Count - complete}}</div>
<div><b>Coverage</b><br>species {{Percent(speciesKnown, records.Count):F1}}%; CP {{Percent(cpKnown, records.Count):F1}}%; IV {{Percent(ivKnown, records.Count):F1}}%; complete {{Percent(complete, records.Count):F1}}%</div>
<div><b>Conflicting items</b><br>{{conflicts}}</div>
<div><b>Integrity</b><br>{{H(integrityStatus)}}</div>
<div><b>Inputs</b><br>setup {{context.SetupInputs}}; swipes {{context.ProgressionSwipes}}; other {{context.OtherNamedInputs}}; semantic {{context.SemanticInputs}}</div>
<div><b>Performance (P50/P95/P99 ms)</b><br>item {{H(TimingSummary(records.Select(x => x.ItemMilliseconds)))}}<br>settling {{H(TimingSummary(records.Select(x => x.SettlingMilliseconds)))}}<br>OCR {{H(TimingSummary(records.Select(x => x.OcrMilliseconds)))}}<br>IV {{H(TimingSummary(records.Select(x => x.IvMilliseconds)))}}</div>
<div><b>Stream</b><br>{{context.Metrics.StreamWidth}}×{{context.Metrics.StreamHeight}}; frames {{context.Metrics.FramesPublished}}; dropped {{context.Metrics.FramesEvicted}}; shutdown {{H(context.Metrics.Shutdown)}}; leases {{context.Metrics.LeasesAtShutdown}}</div>
</div></section>
<section class="controls"><label>Filter
<select id="filter"><option value="all">All</option><option value="complete">Complete</option>
<option value="partial">Partial</option><option value="species-unknown">Species Unknown</option>
<option value="cp-unknown">CP Unknown</option><option value="iv-unknown">IV Unknown</option>
<option value="conflicting">Conflicting</option><option value="slow">Slow items</option>
<option value="long-settling">Long settling</option></select></label>
<button type="button" id="sort">Sort by item time</button></section>
<table id="items"><thead><tr><th>Item</th><th>Evidence</th><th>Species</th><th>CP</th><th>IV</th><th>Timing</th><th>Proof detail</th></tr></thead>
<tbody>{{rows}}</tbody></table>
<script>
const body=document.querySelector('#items tbody'), rows=[...body.querySelectorAll('tr[data-item-row]')];
document.querySelector('#filter').onchange=e=>rows.forEach(r=>r.classList.toggle('hidden',e.target.value!=='all'&&!r.dataset.flags.split(' ').includes(e.target.value)));
document.querySelector('#sort').onclick=()=>[...rows].sort((a,b)=>Number(a.dataset.ms)-Number(b.dataset.ms)).forEach(r=>body.appendChild(r));
</script></body></html>
""";
    }

    private static string RenderRow(StreamProofRecord item)
    {
        var speciesUnknown = !IsKnown(item.Result.Species.Status);
        var cpUnknown = !IsKnown(item.Result.Cp.Status);
        var ivUnknown = !HasKnownIv(item);
        var conflicting = new[]
        {
            item.Result.Species.Status, item.Result.Cp.Status, item.Result.AttackIv.Status,
            item.Result.DefenseIv.Status, item.Result.HpIv.Status
        }.Contains(SemanticFieldStatus.Conflicting);
        var flags = new List<string> { item.Result.IsComplete ? "complete" : "partial" };
        if (speciesUnknown) flags.Add("species-unknown");
        if (cpUnknown) flags.Add("cp-unknown");
        if (ivUnknown) flags.Add("iv-unknown");
        if (conflicting) flags.Add("conflicting");
        if (item.ItemMilliseconds >= 5000 || item.SettlingMilliseconds >= 5000) flags.Add("slow");
        if (item.SettlingMilliseconds >= 5000) flags.Add("long-settling");
        var frames = string.Join("", item.EvidenceFiles.Select((path, index) =>
            $"<a href='{A(path)}'><img class='crop' src='{A(path)}' alt='evidence frame {index + 1}'></a>"));
        var crops =
            $"<a href='{A(item.HeaderCropPath)}'><img class='crop' src='{A(item.HeaderCropPath)}' alt='header crop'></a>" +
            $"<a href='{A(item.AppraisalCropPath)}'><img class='crop' src='{A(item.AppraisalCropPath)}' alt='appraisal crop'></a>" +
            $"<a href='{A(item.AttackCropPath)}'><img class='crop' src='{A(item.AttackCropPath)}' alt='attack bar'></a>" +
            $"<a href='{A(item.DefenseCropPath)}'><img class='crop' src='{A(item.DefenseCropPath)}' alt='defense bar'></a>" +
            $"<a href='{A(item.HpCropPath)}'><img class='crop' src='{A(item.HpCropPath)}' alt='hp bar'></a>";
        var ivConfidence = new[]
        {
            item.Result.AttackIv.Confidence,
            item.Result.DefenseIv.Confidence,
            item.Result.HpIv.Confidence
        }.Min();
        return $"""
<tr data-item-row="{item.Ordinal}" data-run-id="{H(item.RunId)}" data-fingerprint="{H(item.ItemFingerprint)}" data-flags="{H(string.Join(" ", flags))}" data-ms="{item.ItemMilliseconds.ToString(CultureInfo.InvariantCulture)}">
<td><b>{item.Ordinal}</b><br>{H(item.CapturedAtUtc.ToString("O"))}</td>
<td><a href="{A(item.FullFramePath)}"><img class="thumb" src="{A(item.FullFramePath)}" alt="item {item.Ordinal}"></a><div>{crops}</div></td>
<td class="{H(item.Result.Species.Status)}">{H(Value(item.Result.Species.Value))}<br>{H(item.Result.Species.Status)} {item.Result.Species.Confidence:P1}</td>
<td class="{H(item.Result.Cp.Status)}">{H(Value(item.Result.Cp.Value))}<br>{H(item.Result.Cp.Status)} {item.Result.Cp.Confidence:P1}</td>
<td class="{H(IvStatus(item))}">{H(Value(item.Result.AttackIv.Value))}/{H(Value(item.Result.DefenseIv.Value))}/{H(Value(item.Result.HpIv.Value))}<br>{H(IvStatus(item))} {ivConfidence:P1}</td>
<td>item {item.ItemMilliseconds:F0} ms<br>settling {item.SettlingMilliseconds:F0} ms<br>swipe-to-stable {H(item.SwipeToStableMilliseconds?.ToString("F0", CultureInfo.InvariantCulture) ?? "n/a")} ms<br>OCR {item.OcrMilliseconds:F0} ms<br>IV {item.IvMilliseconds:F0} ms</td>
<td><details><summary>Evidence and audit</summary>
<p><b>Fingerprint:</b> <code>{H(item.ItemFingerprint)}</code></p>
<p><b>Frames:</b> {H(string.Join(", ", item.FrameIds))}<br><b>Hashes:</b> <code>{H(string.Join(", ", item.EvidenceHashes))}</code></p>
<div>{frames}</div>
<p><b>Raw OCR:</b> {H(string.Join(" | ", item.RawOcr))}</p>
<p><b>Gate rejections:</b> {H(string.Join(", ", item.GateRejectionCounts.Select(x => $"{x.Key}={x.Value}")))}</p>
</details></td></tr>
""";
    }

    private static string RenderIntegrityMarkdown(StreamProofIntegrity report)
    {
        var builder = new StringBuilder()
            .AppendLine("# Stream proof integrity")
            .AppendLine()
            .AppendLine($"Status: **{report.IntegrityStatus}**")
            .AppendLine()
            .AppendLine("| Check | Result | Detail |")
            .AppendLine("|---|---:|---|");
        foreach (var check in report.Checks)
        {
            builder.AppendLine($"| {check.Name} | {(check.Passed ? "PASS" : "FAIL")} | {check.Detail.Replace("|", "\\|", StringComparison.Ordinal)} |");
        }
        return builder.ToString();
    }

    private static object CountStatus(
        IReadOnlyList<StreamProofRecord> records,
        Func<StreamProofRecord, SemanticFieldStatus> selector) => new
    {
        Known = records.Count(x => selector(x) == SemanticFieldStatus.Known),
        Unknown = records.Count(x => selector(x) == SemanticFieldStatus.Unknown),
        Conflicting = records.Count(x => selector(x) == SemanticFieldStatus.Conflicting)
    };

    private static IReadOnlyDictionary<string, int> AggregateGateRejections(
        IReadOnlyList<StreamProofHandoff> handoffs)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in handoffs.SelectMany(x => x.ReasonCounts))
        {
            result[pair.Key] = result.TryGetValue(pair.Key, out var count)
                ? count + pair.Value
                : pair.Value;
        }
        return result;
    }

    private static IReadOnlyList<StreamProofRecord> ReadJsonl(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<StreamProofRecord>();
        }

        try
        {
            return File.ReadLines(path)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => JsonSerializer.Deserialize<StreamProofRecord>(x, JsonOptions)
                    ?? throw new JsonException("A JSONL record was null."))
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<StreamProofRecord>();
        }
    }

    private static IReadOnlyList<ArtifactIdentity> ReadCsvIdentities(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<ArtifactIdentity>();
        }

        try
        {
            var lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                return Array.Empty<ArtifactIdentity>();
            }

            var header = ParseCsvLine(lines[0]);
            var runIdIndex = Array.IndexOf(header, "RunId");
            var ordinalIndex = Array.IndexOf(header, "RunItem");
            var fingerprintIndex = Array.IndexOf(header, "ItemFingerprint");
            if (runIdIndex < 0 || ordinalIndex < 0 || fingerprintIndex < 0)
            {
                return Array.Empty<ArtifactIdentity>();
            }

            return lines.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(ParseCsvLine)
                .Select(fields => new ArtifactIdentity(
                    fields[runIdIndex],
                    int.Parse(fields[ordinalIndex], CultureInfo.InvariantCulture),
                    fields[fingerprintIndex]))
                .ToArray();
        }
        catch (Exception error) when (
            error is FormatException or IndexOutOfRangeException)
        {
            return Array.Empty<ArtifactIdentity>();
        }
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var value = line[index];
            if (value == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (value == ',' && !quoted)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(value);
            }
        }
        if (quoted)
        {
            throw new FormatException("Unterminated CSV field.");
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }

    private static IReadOnlyList<ArtifactIdentity> ReadHtmlIdentities(string html) =>
        Regex.Matches(
                html,
                "data-item-row=\"(\\d+)\" data-run-id=\"([^\"]*)\" data-fingerprint=\"([^\"]*)\"",
                RegexOptions.CultureInvariant)
            .Select(match => new ArtifactIdentity(
                WebUtility.HtmlDecode(match.Groups[2].Value),
                int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                WebUtility.HtmlDecode(match.Groups[3].Value)))
            .ToArray();

    private static SummaryIdentity? ReadSummaryIdentity(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            return new(
                root.GetProperty("RunId").GetString() ?? string.Empty,
                root.GetProperty("CompletedItems").GetInt32(),
                root.GetProperty("RequestedItems").GetInt32(),
                root.GetProperty("RunStatus").GetString() ?? string.Empty);
        }
        catch (Exception error) when (
            error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static ArtifactIdentity Identity(StreamProofRecord record) =>
        new(record.RunId, record.Ordinal, record.ItemFingerprint);

    private static bool TryResolveRelative(
        string output,
        string relative,
        out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(relative) ||
            Path.IsPathRooted(relative) ||
            Uri.TryCreate(relative, UriKind.Absolute, out _))
        {
            return false;
        }

        var root = Path.GetFullPath(output)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        resolved = Path.GetFullPath(Path.Combine(root, FromRelative(relative)));
        return resolved.StartsWith(root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string TimingSummary(IEnumerable<double> source)
    {
        var values = source.OrderBy(x => x).ToArray();
        return values.Length == 0
            ? "n/a"
            : $"{Quantile(values, .50):F0}/{Quantile(values, .95):F0}/{Quantile(values, .99):F0}";
    }

    private sealed record ArtifactIdentity(
        string RunId,
        int Ordinal,
        string ItemFingerprint);

    private readonly record struct SummaryIdentity(
        string RunId,
        int CompletedItems,
        int RequestedItems,
        string RunStatus);

    private static object Percentiles(IEnumerable<double> source)
    {
        var values = source.OrderBy(x => x).ToArray();
        return new
        {
            Mean = values.Length == 0 ? (double?)null : values.Average(),
            P50 = Quantile(values, .50),
            P95 = Quantile(values, .95),
            P99 = Quantile(values, .99),
            Max = values.Length == 0 ? (double?)null : values[^1]
        };
    }

    private static double? Quantile(double[] values, double q)
    {
        if (values.Length == 0) return null;
        var index = (values.Length - 1) * q;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        return values[lower] + ((values[upper] - values[lower]) * (index - lower));
    }

    private static string IvStatus(StreamProofRecord item) => HasKnownIv(item)
        ? SemanticFieldStatus.Known.ToString()
        : new[] { item.Result.AttackIv.Status, item.Result.DefenseIv.Status, item.Result.HpIv.Status }
            .Contains(SemanticFieldStatus.Conflicting)
            ? SemanticFieldStatus.Conflicting.ToString()
            : SemanticFieldStatus.Unknown.ToString();

    private static bool HasKnownIv(StreamProofRecord item) =>
        IsKnown(item.Result.AttackIv.Status) &&
        IsKnown(item.Result.DefenseIv.Status) &&
        IsKnown(item.Result.HpIv.Status);

    private static bool IsKnown(SemanticFieldStatus status) => status == SemanticFieldStatus.Known;
    private static double Percent(int count, int total) => total == 0 ? 0 : count * 100d / total;
    private static StreamProofIntegrityCheck Check(string name, bool passed, string detail) => new(name, passed, detail);
    private static int CountOccurrences(string value, string token) => value.Split(token, StringSplitOptions.None).Length - 1;
    private static IEnumerable<string> ImagePaths(StreamProofRecord item) =>
        item.EvidenceFiles.Concat([item.HeaderCropPath, item.AppraisalCropPath, item.AttackCropPath, item.DefenseCropPath, item.HpCropPath]);
    private static string FromRelative(string path) => path.Replace('/', Path.DirectorySeparatorChar);
    private static string H(object? value) => WebUtility.HtmlEncode(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
    private static string A(string value) => H(value.Replace('\\', '/'));
    private static string Value(object? value) => value is null ? "Unknown" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "Unknown";

    private static string Csv(object? value)
    {
        var text = Value(value);
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static async Task AtomicWriteAsync(string path, string contents, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, contents, new UTF8Encoding(false), cancellationToken);
        File.Move(temporary, path, true);
    }
}
