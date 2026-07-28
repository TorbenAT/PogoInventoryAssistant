using PogoInventory.Vision.Models;

namespace PogoInventory.Streaming.Semantics;

public enum SemanticRegionKind { StateRegion, HeaderRegion, SpeciesNameRegion, CpRegion, AppraisalPanelRegion, AttackBarRegion, DefenseBarRegion, HpBarRegion, BadgeRegion, TagRegion }

public sealed record SemanticRegion(SemanticRegionKind Kind, NormalizedRegion Region)
{
    public PixelRectangle Resolve(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException();
        Region.Validate(Kind.ToString());
        return Region.ToPixels(width, height);
    }
}

public static class SemanticLayoutValidator
{
    public static bool IsSupported(int width, int height, string orientation, IReadOnlyCollection<SemanticRegion> regions)
    {
        if (width <= 0 || height <= 0 || !string.Equals(orientation, width >= height ? "Landscape" : "Portrait", StringComparison.Ordinal)) return false;
        try { foreach (var region in regions) _ = region.Resolve(width, height); return true; }
        catch (ArgumentException) { return false; }
    }
}
