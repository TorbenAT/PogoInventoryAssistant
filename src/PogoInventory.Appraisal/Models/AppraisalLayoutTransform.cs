namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalLayoutTransform
{
    public double XOffset { get; init; }
    public double YOffset { get; init; }
    public double Scale { get; init; } = 1;

    public void Validate()
    {
        if (!double.IsFinite(XOffset) ||
            !double.IsFinite(YOffset) ||
            !double.IsFinite(Scale) ||
            Scale <= 0)
        {
            throw new InvalidOperationException(
                "Appraisal layout transform is invalid.");
        }
    }
}
