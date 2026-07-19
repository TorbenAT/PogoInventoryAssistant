namespace PogoInventory.Automation.Models;

public sealed record InventoryControlProfile
{
    public string SchemaVersion { get; init; } = "1.0";
    public required NormalizedPoint OpenInventory { get; init; }
    public required NormalizedPoint OpenSearch { get; init; }
    public required NormalizedPoint ClearSearch { get; init; }
    public required NormalizedPoint ApplySearch { get; init; }
    public required NormalizedPoint FirstSearchResult { get; init; }
    public required NormalizedPoint CloseAppraisal { get; init; }
    public required NormalizedPoint OpenTagMenu { get; init; }
    public required NormalizedPoint SelectAiIndexed { get; init; }
    public required NormalizedPoint SelectAiReview { get; init; }
    public required NormalizedPoint ConfirmTag { get; init; }
    public required NormalizedPoint DismissNewMegaLevel { get; init; }

    public void Validate()
    {
        if (SchemaVersion != "1.0")
        {
            throw new ArgumentException($"Unsupported inventory control profile schema '{SchemaVersion}'.");
        }

        ValidatePoint(OpenInventory, nameof(OpenInventory));
        ValidatePoint(OpenSearch, nameof(OpenSearch));
        ValidatePoint(ClearSearch, nameof(ClearSearch));
        ValidatePoint(ApplySearch, nameof(ApplySearch));
        ValidatePoint(FirstSearchResult, nameof(FirstSearchResult));
        ValidatePoint(CloseAppraisal, nameof(CloseAppraisal));
        ValidatePoint(OpenTagMenu, nameof(OpenTagMenu));
        ValidatePoint(SelectAiIndexed, nameof(SelectAiIndexed));
        ValidatePoint(SelectAiReview, nameof(SelectAiReview));
        ValidatePoint(ConfirmTag, nameof(ConfirmTag));
        ValidatePoint(DismissNewMegaLevel, nameof(DismissNewMegaLevel));
    }

    private static void ValidatePoint(NormalizedPoint point, string name)
    {
        ArgumentNullException.ThrowIfNull(point, name);
        point.Validate(name);
    }
}
