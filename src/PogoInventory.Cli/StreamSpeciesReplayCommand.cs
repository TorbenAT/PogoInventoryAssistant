using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Reference;
using PogoInventory.HeaderText;
using PogoInventory.Semantics;
using PogoInventory.TesseractOcr;

/// <summary>
/// Replays only the saved stream evidence through the header/species parser.
/// It has no Device or Automation dependency and never changes the source run.
/// </summary>
internal static class StreamSpeciesReplayCommand
{
    private static readonly JsonSerializerOptions JsonLinesOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var options = Options.Parse(args);
        if (Directory.Exists(options.Output) &&
            Directory.EnumerateFileSystemEntries(options.Output).Any())
        {
            throw new InvalidOperationException(
                $"Output directory must be new or empty: {options.Output}");
        }

        Directory.CreateDirectory(options.Output);
        var sourceItems = Path.Combine(options.Source, "items.jsonl");
        if (!File.Exists(sourceItems))
        {
            throw new FileNotFoundException(
                "Stream evidence must contain items.jsonl.", sourceItems);
        }

        if (!TesseractTextRecognizer.IsSupported(options.Tessdata, "eng"))
        {
            throw new InvalidOperationException(
                $"Tesseract tessdata is unavailable: {options.Tessdata}");
        }

        var reference = new StaticSpeciesReference(
            SpeciesReferenceLoader.LoadFromFile(options.SpeciesReference)
                .Species.Select(x => x.Name));
        using var recognizer = new TesseractTextRecognizer(
            options.Tessdata, "eng", binarizeCpRegion: false);
        var headerAnalyzer = new PokemonHeaderAnalyzer(recognizer, reference);
        var semanticAnalyzer = new PokemonItemSemanticAnalyzer();
        var records = new List<ReplayItem>();

        foreach (var line in await File.ReadAllLinesAsync(sourceItems, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var source = JsonSerializer.Deserialize<StreamProofRecord>(
                line, JsonLinesOptions) ?? throw new InvalidOperationException(
                    "items.jsonl contains an invalid stream record.");
            if (source.FrameIds.Count != source.EvidenceFiles.Count ||
                source.FrameIds.Count != source.EvidenceHashes.Count ||
                source.FrameIds.Count < 2)
            {
                throw new InvalidOperationException(
                    $"Ordinal {source.Ordinal} has incomplete evidence metadata.");
            }

            var evidence = new List<PokemonEvidenceFrame>();
            var species = new List<SemanticObservation<string>>();
            var evidenceFramesRead = 0;
            for (var index = 0; index < source.EvidenceFiles.Count; index++)
            {
                var evidencePath = ResolveEvidencePath(
                    options.Source, source.EvidenceFiles[index]);
                var png = await File.ReadAllBytesAsync(evidencePath, cancellationToken);
                var hash = Convert.ToHexString(SHA256.HashData(png))
                    .ToLowerInvariant();
                if (!string.Equals(
                        hash,
                        source.EvidenceHashes[index],
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Ordinal {source.Ordinal} evidence hash mismatch at frame {source.FrameIds[index]}.");
                }

                evidenceFramesRead++;
                evidence.Add(new PokemonEvidenceFrame(
                    source.FrameIds[index],
                    source.FrameTimestampsUtc[index],
                    hash,
                    "AppraisalBars",
                    "offline-stream-replay"));
                var header = await headerAnalyzer.AnalyzeAsync(
                    png, HeaderScreenType.AppraisalBars, cancellationToken);
                if (header.Species is not null)
                {
                    species.Add(new SemanticObservation<string>(
                        header.Species,
                        header.SpeciesConfidence,
                        source.FrameIds[index],
                        hash));
                }
            }

            var replay = semanticAnalyzer.Analyze(
                new PokemonItemEvidenceSet(
                    $"offline-replay:{source.Ordinal:D6}", evidence, evidence),
                species,
                Array.Empty<SemanticObservation<int?>>(),
                Array.Empty<SemanticObservation<(int Attack, int Defense, int Hp)>>());
            records.Add(new ReplayItem(
                source.Ordinal,
                source.Result.Species.Status,
                source.Result.Species.Value,
                replay.Species.Status,
                replay.Species.Value,
                replay.Species.Reasons,
                evidenceFramesRead));
        }

        var baselineKnown = records.Count(x => x.PreviousStatus == SemanticFieldStatus.Known);
        var baselineConflicting = records.Count(x => x.PreviousStatus == SemanticFieldStatus.Conflicting);
        var replayKnown = records.Count(x => x.ReplayStatus == SemanticFieldStatus.Known);
        var replayConflicting = records.Count(x => x.ReplayStatus == SemanticFieldStatus.Conflicting);
        var falseKnown = records.Where(x =>
            x.PreviousStatus == SemanticFieldStatus.Known &&
            x.ReplayStatus == SemanticFieldStatus.Known &&
            !string.Equals(x.PreviousValue, x.ReplayValue, StringComparison.Ordinal))
            .ToArray();
        var uplifted = records.Where(x =>
            x.PreviousStatus != SemanticFieldStatus.Known &&
            x.ReplayStatus == SemanticFieldStatus.Known).ToArray();
        var accepted = records.Count > 0 &&
            records.Sum(x => x.EvidenceFramesRead) >= records.Count * 3 &&
            falseKnown.Length == 0 &&
            replayConflicting <= baselineConflicting &&
            replayKnown >= options.MinimumKnown;

        var report = new
        {
            Source = options.Source,
            Items = records.Count,
            EvidenceFramesRead = records.Sum(x => x.EvidenceFramesRead),
            BaselineKnown = baselineKnown,
            BaselineConflicting = baselineConflicting,
            ReplayKnown = replayKnown,
            ReplayUnknown = records.Count - replayKnown - replayConflicting,
            ReplayConflicting = replayConflicting,
            Uplifted = uplifted.Select(x => new
            {
                x.Ordinal,
                Species = x.ReplayValue,
                x.ReplayReasons
            }),
            FalseKnown = falseKnown.Select(x => new
            {
                x.Ordinal,
                Previous = x.PreviousValue,
                Replay = x.ReplayValue
            }),
            ConflictsNotWorsened = replayConflicting <= baselineConflicting,
            MinimumKnown = options.MinimumKnown,
            Accepted = accepted
        };
        await File.WriteAllTextAsync(
            Path.Combine(options.Output, "species-replay.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            }),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(options.Output, "species-replay.csv"),
            RenderCsv(records),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(options.Output, "species-replay.md"),
            RenderMarkdown(report, uplifted, falseKnown),
            cancellationToken);

        Console.WriteLine(
            $"Offline species replay: {replayKnown}/{records.Count} Known; " +
            $"uplifted={uplifted.Length}; false-Known={falseKnown.Length}; " +
            $"conflicts={replayConflicting}; accepted={accepted}.");
        return accepted ? 0 : 2;
    }

    private static string ResolveEvidencePath(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(
            fullRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Evidence path escapes the source directory.");
        }
        return candidate;
    }

    private static string RenderCsv(IEnumerable<ReplayItem> records)
    {
        static string Quote(string? value) =>
            '"' + (value ?? string.Empty).Replace("\"", "\"\"") + '"';
        var builder = new StringBuilder(
            "Ordinal,PreviousStatus,PreviousSpecies,ReplayStatus,ReplaySpecies,Reasons,EvidenceFramesRead\n");
        foreach (var record in records)
        {
            builder.Append(record.Ordinal).Append(',')
                .Append(record.PreviousStatus).Append(',')
                .Append(Quote(record.PreviousValue)).Append(',')
                .Append(record.ReplayStatus).Append(',')
                .Append(Quote(record.ReplayValue)).Append(',')
                .Append(Quote(string.Join('|', record.ReplayReasons))).Append(',')
                .Append(record.EvidenceFramesRead).Append('\n');
        }
        return builder.ToString();
    }

    private static string RenderMarkdown(
        dynamic report,
        IReadOnlyList<ReplayItem> uplifted,
        IReadOnlyList<ReplayItem> falseKnown)
    {
        var builder = new StringBuilder("# Offline species replay\n\n");
        builder.AppendLine($"- Items: {report.Items}");
        builder.AppendLine($"- Evidence frames read and hash-verified: {report.EvidenceFramesRead}");
        builder.AppendLine($"- Species Known: {report.BaselineKnown} -> {report.ReplayKnown}");
        builder.AppendLine($"- Species conflicts: {report.BaselineConflicting} -> {report.ReplayConflicting}");
        builder.AppendLine($"- False-Known (contradictory Known replay): {falseKnown.Count}");
        builder.AppendLine($"- Accepted: {report.Accepted}");
        builder.AppendLine();
        builder.AppendLine("## Reference-safe uplifts\n");
        foreach (var item in uplifted)
        {
            builder.AppendLine($"- Ordinal {item.Ordinal}: `{item.ReplayValue}` ({string.Join(", ", item.ReplayReasons)})");
        }
        return builder.ToString();
    }

    private sealed record ReplayItem(
        int Ordinal,
        SemanticFieldStatus PreviousStatus,
        string? PreviousValue,
        SemanticFieldStatus ReplayStatus,
        string? ReplayValue,
        IReadOnlyList<string> ReplayReasons,
        int EvidenceFramesRead);

    private sealed record Options(
        string Source,
        string Output,
        string Tessdata,
        string SpeciesReference,
        int MinimumKnown)
    {
        public static Options Parse(string[] args)
        {
            string Get(string name, string? fallback = null)
            {
                var index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length
                    ? args[index + 1]
                    : fallback ?? throw new ArgumentException($"{name} is required.");
            }

            var root = Directory.GetCurrentDirectory();
            var minimumKnown = int.Parse(Get("--minimum-known", "0"));
            if (minimumKnown < 0)
            {
                throw new ArgumentOutOfRangeException("--minimum-known");
            }
            return new Options(
                Path.GetFullPath(Get("--source")),
                Path.GetFullPath(Get("--out")),
                Path.GetFullPath(Get("--tessdata", Path.Combine(root, "tools", "tessdata-best"))),
                Path.GetFullPath(Get("--species-reference", Path.Combine(root, "data", "reference", "species-reference.json"))),
                minimumKnown);
        }
    }
}
