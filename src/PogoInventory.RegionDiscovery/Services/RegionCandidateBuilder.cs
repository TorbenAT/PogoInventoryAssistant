using PogoInventory.RegionDiscovery.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.RegionDiscovery.Services;

internal static class RegionCandidateBuilder
{
    public static IReadOnlyList<RegionDiscoveryCandidate> Build(
        IReadOnlyList<RegionDiscoveryCell> cells,
        RegionDiscoveryOptions options)
    {
        var result = new List<RegionDiscoveryCandidate>();
        foreach (var kind in Enum.GetValues<RegionCandidateKind>())
        {
            result.AddRange(BuildForKind(cells, options, kind));
        }

        return result
            .OrderBy(candidate => candidate.Kind)
            .ThenByDescending(candidate => candidate.AverageScore)
            .ThenBy(candidate => candidate.Region.Y)
            .ThenBy(candidate => candidate.Region.X)
            .ToArray();
    }

    private static IReadOnlyList<RegionDiscoveryCandidate> BuildForKind(
        IReadOnlyList<RegionDiscoveryCell> cells,
        RegionDiscoveryOptions options,
        RegionCandidateKind kind)
    {
        var orderedScores = cells
            .Select(cell => cell.ScoreFor(kind))
            .OrderBy(value => value)
            .ToArray();
        if (orderedScores.Length == 0)
        {
            return Array.Empty<RegionDiscoveryCandidate>();
        }

        var percentileIndex = (int)Math.Floor(
            options.CandidatePercentile * (orderedScores.Length - 1));
        var threshold = Math.Max(
            options.MinimumCandidateScore,
            orderedScores[Math.Clamp(percentileIndex, 0, orderedScores.Length - 1)]);

        var eligible = cells
            .Where(cell => cell.ScoreFor(kind) >= threshold)
            .ToDictionary(
                cell => (cell.Row, cell.Column),
                cell => cell);

        if (eligible.Count == 0)
        {
            var best = cells
                .OrderByDescending(cell => cell.ScoreFor(kind))
                .First();
            eligible[(best.Row, best.Column)] = best;
        }

        var visited = new HashSet<(int Row, int Column)>();
        var components = new List<IReadOnlyList<RegionDiscoveryCell>>();
        foreach (var seed in eligible.Values
                     .OrderByDescending(cell => cell.ScoreFor(kind))
                     .ThenBy(cell => cell.Row)
                     .ThenBy(cell => cell.Column))
        {
            var key = (seed.Row, seed.Column);
            if (!visited.Add(key))
            {
                continue;
            }

            var minimumExpansionScore = Math.Max(
                options.MinimumCandidateScore,
                seed.ScoreFor(kind) * options.CandidateExpansionRatio);
            var queue = new Queue<RegionDiscoveryCell>();
            var component = new List<RegionDiscoveryCell>();
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var neighborKey in Neighbors(current.Row, current.Column))
                {
                    if (visited.Contains(neighborKey) ||
                        !eligible.TryGetValue(neighborKey, out var neighbor) ||
                        neighbor.ScoreFor(kind) < minimumExpansionScore)
                    {
                        continue;
                    }

                    visited.Add(neighborKey);
                    queue.Enqueue(neighbor);
                }
            }

            components.Add(component);
        }

        return components
            .Select(component => CreateCandidate(component, kind))
            .OrderByDescending(candidate => candidate.AverageScore)
            .ThenByDescending(candidate => candidate.MaximumScore)
            .ThenBy(candidate => candidate.Region.Y)
            .ThenBy(candidate => candidate.Region.X)
            .Take(options.MaximumCandidatesPerKind)
            .Select((candidate, index) => candidate with
            {
                Id = $"{KindPrefix(kind)}-{index + 1:D2}"
            })
            .ToArray();
    }

    private static RegionDiscoveryCandidate CreateCandidate(
        IReadOnlyList<RegionDiscoveryCell> cells,
        RegionCandidateKind kind)
    {
        var left = cells.Min(cell => cell.Region.X);
        var top = cells.Min(cell => cell.Region.Y);
        var right = cells.Max(cell => cell.Region.X + cell.Region.Width);
        var bottom = cells.Max(cell => cell.Region.Y + cell.Region.Height);
        var scores = cells.Select(cell => cell.ScoreFor(kind)).ToArray();

        return new RegionDiscoveryCandidate
        {
            Id = "pending",
            Kind = kind,
            Region = new NormalizedRegion
            {
                X = left,
                Y = top,
                Width = right - left,
                Height = bottom - top
            },
            AverageScore = scores.Average(),
            MaximumScore = scores.Max(),
            CellCount = cells.Count,
            ProvisionalReason = Reason(kind)
        };
    }

    private static IEnumerable<(int Row, int Column)> Neighbors(
        int row,
        int column)
    {
        yield return (row - 1, column);
        yield return (row + 1, column);
        yield return (row, column - 1);
        yield return (row, column + 1);
    }

    private static string KindPrefix(RegionCandidateKind kind) =>
        kind switch
        {
            RegionCandidateKind.StableChrome => "stable",
            RegionCandidateKind.ScreenStateDiscriminator => "state",
            RegionCandidateKind.DynamicContent => "dynamic",
            RegionCandidateKind.TextDense => "text",
            _ => "region"
        };

    private static string Reason(RegionCandidateKind kind) =>
        kind switch
        {
            RegionCandidateKind.StableChrome =>
                "Low cross-image variation with persistent edge structure; provisional stable UI anchor.",
            RegionCandidateKind.ScreenStateDiscriminator =>
                "High separation between visual clusters and low variation inside clusters; provisional screen-state anchor.",
            RegionCandidateKind.DynamicContent =>
                "High change across images or consecutive captures; provisional Pokémon-specific content region.",
            RegionCandidateKind.TextDense =>
                "High edge density combined with changing content; provisional text or numeric region.",
            _ => "Provisional visual region."
        };
}
