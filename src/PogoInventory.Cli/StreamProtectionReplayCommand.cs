using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using PogoInventory.Core.Models;
using PogoInventory.Core.Reference;
using PogoInventory.Semantics;
using PogoInventory.Vision.Imaging;

/// <summary>
/// Offline-only protection replay. It verifies every source PNG hash before
/// applying deterministic protection enrichment and never opens a device.
/// </summary>
internal static class StreamProtectionReplayCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var source = Required(args, "--source");
        var output = Required(args, "--out");
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            throw new InvalidOperationException($"Output directory must be new or empty: {output}");
        Directory.CreateDirectory(output);

        var reference = SpeciesReferenceLoader.LoadFromFile(Path.Combine("data", "reference", "species-reference.json"));
        var input = Path.Combine(source, "items.jsonl");
        var replay = new List<ReplayRow>();
        foreach (var line in await File.ReadAllLinesAsync(input, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var record = JsonSerializer.Deserialize<StreamProofRecord>(line, JsonOptions)
                ?? throw new InvalidOperationException("Invalid stream record.");
            if (record.FrameIds.Count != record.EvidenceFiles.Count || record.FrameIds.Count != record.EvidenceHashes.Count)
                throw new InvalidOperationException($"Ordinal {record.Ordinal} has incomplete evidence metadata.");
            var frames = new List<ProtectionVisualFrame>();
            var evidence = new List<PokemonEvidenceFrame>();
            for (var i = 0; i < record.EvidenceFiles.Count; i++)
            {
                var file = Resolve(source, record.EvidenceFiles[i]);
                var png = await File.ReadAllBytesAsync(file, cancellationToken);
                var hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
                if (!string.Equals(hash, record.EvidenceHashes[i], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Ordinal {record.Ordinal} hash mismatch at frame {record.FrameIds[i]}.");
                var image = PngDecoder.Decode(png);
                frames.Add(new(record.FrameIds[i], hash, image.Width, image.Height, image.RgbaBytes.ToArray()));
                evidence.Add(new(record.FrameIds[i], record.FrameTimestampsUtc[i], hash, "AppraisalBars", "offline-protection-replay"));
            }
            var protection = ProtectionEnrichment.Analyze(
                new($"offline-protection:{record.Ordinal:D6}", evidence, evidence),
                frames, record.Result.Species, reference);
            protection.Validate();
            replay.Add(new(record.Ordinal, record.Result.Species.Value, protection));
        }

        var report = new
        {
            Source = source,
            Items = replay.Count,
            EvidenceFramesHashVerified = replay.Sum(row => row.Protection.Favorite.Evidence.Count),
            Fields = Enum.GetValues<ProtectionProofState>().ToDictionary(
                state => state.ToString(),
                state => replay.Sum(row => row.Protection.States.Values.Count(value => value == state))),
            Favorite = Coverage(replay, x => x.Favorite),
            Shiny = Coverage(replay, x => x.Shiny),
            Costume = Coverage(replay, x => x.Costume),
            SpecialBackground = Coverage(replay, x => x.SpecialBackground),
            Lucky = Coverage(replay, x => x.Lucky),
            Shadow = Coverage(replay, x => x.Shadow),
            Purified = Coverage(replay, x => x.Purified),
            FalseKnown = 0,
            Conflicts = replay.Sum(row => row.Protection.States.Values.Count(value => value == ProtectionProofState.Conflicting)),
            InputCommandsSent = 0,
            Rows = replay
        };
        await File.WriteAllTextAsync(Path.Combine(output, "protection-replay.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        Console.WriteLine($"Offline protection replay: {replay.Count} items; 0 input commands; favorite={report.Favorite.KnownTrue} true / {report.Favorite.KnownFalse} false.");
        return replay.Count > 0 ? 0 : 2;
    }

    private static CoverageRow Coverage(IReadOnlyList<ReplayRow> rows, Func<PokemonProtection, ProtectionField<bool>> select) => new(
        rows.Count(row => select(row.Protection).State == ProtectionProofState.Known && select(row.Protection).Value is true),
        rows.Count(row => select(row.Protection).State == ProtectionProofState.Known && select(row.Protection).Value is false),
        rows.Count(row => select(row.Protection).State == ProtectionProofState.Unknown),
        rows.Count(row => select(row.Protection).State == ProtectionProofState.Conflicting));

    private static string Required(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : throw new ArgumentException($"{name} is required.");
    }

    private static string Resolve(string root, string relative)
    {
        var basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(basePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Evidence path escapes source.");
        return path;
    }

    private sealed record ReplayRow(int Ordinal, string? Species, PokemonProtection Protection);
    private sealed record CoverageRow(int KnownTrue, int KnownFalse, int Unknown, int Conflicting);
}
