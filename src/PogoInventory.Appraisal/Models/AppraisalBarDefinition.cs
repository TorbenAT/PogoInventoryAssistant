using PogoInventory.Vision.Models;

namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalBarDefinition
{
    public AppraisalBarKind Kind { get; init; }
    public required NormalizedRegion Region { get; init; }

    public void Validate()
    {
        Region.Validate(Kind.ToString());
    }
}
