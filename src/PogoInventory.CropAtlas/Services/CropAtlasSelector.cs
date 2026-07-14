using PogoInventory.CropAtlas.Models;
using PogoInventory.RegionDiscovery.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.CropAtlas.Services;

public static class CropAtlasSelector
{
    private static readonly RegionCandidateKind[] RequiredKinds =
    {
        RegionCandidateKind.ScreenStateDiscriminator,
        RegionCandidateKind.DynamicContent,
        RegionCandidateKind.TextDense
    };

    public static IReadOnlyList<RegionDiscoveryCandidate> Select(
        IReadOnlyList<RegionDiscoveryCandidate> candidates,
        CropAtlasOptions options)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var selected = new List<RegionDiscoveryCandidate>();

        foreach (var kind in RequiredKinds)
        {
            var first = Ordered(candidates, kind)
                .FirstOrDefault(candidate =>
                    IsCompatible(candidate, selected, options));
            if (first is not null)
            {
                selected.Add(first);
            }
        }

        var remaining = candidates
            .Where(candidate => RequiredKinds.Contains(candidate.Kind))
            .OrderByDescending(candidate => candidate.AverageScore)
            .ThenByDescending(candidate => candidate.MaximumScore)
            .ThenBy(candidate => candidate.Kind)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal);

        foreach (var candidate in remaining)
        {
            if (selected.Count >= options.MaximumCandidates)
            {
                break;
            }

            if (selected.Any(item =>
                item.Id.Equals(candidate.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            if (IsCompatible(candidate, selected, options))
            {
                selected.Add(candidate);
            }
        }

        return selected
            .OrderBy(candidate => candidate.Kind)
            .ThenByDescending(candidate => candidate.AverageScore)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IOrderedEnumerable<RegionDiscoveryCandidate> Ordered(
        IReadOnlyList<RegionDiscoveryCandidate> candidates,
        RegionCandidateKind kind) =>
        candidates
            .Where(candidate => candidate.Kind == kind)
            .OrderByDescending(candidate => candidate.AverageScore)
            .ThenByDescending(candidate => candidate.MaximumScore)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal);

    private static bool IsCompatible(
        RegionDiscoveryCandidate candidate,
        IReadOnlyList<RegionDiscoveryCandidate> selected,
        CropAtlasOptions options) =>
        selected
            .Where(item => item.Kind == candidate.Kind)
            .All(item =>
                OverlapRatio(item.Region, candidate.Region) <=
                options.MaximumSameKindOverlap);

    private static double OverlapRatio(
        NormalizedRegion first,
        NormalizedRegion second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(
            first.X + first.Width,
            second.X + second.Width);
        var bottom = Math.Min(
            first.Y + first.Height,
            second.Y + second.Height);

        var intersection = Math.Max(0, right - left) *
            Math.Max(0, bottom - top);
        var smallerArea = Math.Min(
            first.Width * first.Height,
            second.Width * second.Height);
        return smallerArea <= 0 ? 0 : intersection / smallerArea;
    }
}
