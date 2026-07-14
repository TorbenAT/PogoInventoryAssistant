namespace PogoInventory.Appraisal.Models;

public sealed record AppraisalImageResult
{
    public required string FileName { get; init; }
    public int SequenceNumber { get; init; }
    public required string Sha256 { get; init; }
    public bool Decoded { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public string? ClusterId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDetail { get; init; }
    public AppraisalAnalysisResult? Analysis { get; init; }
    public string? OverlayFile { get; init; }
}
