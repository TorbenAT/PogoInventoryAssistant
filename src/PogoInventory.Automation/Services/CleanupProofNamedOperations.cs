using PogoInventory.Automation.Models;
using PogoInventory.Vision.Models;

namespace PogoInventory.Automation.Services;

public interface ICleanupProofNamedOperations : IVerifiedInventoryNamedOperations
{
    Task<CleanupProofIdentityCapture> CaptureCleanupIdentityAsync(
        int maximumFrames,
        int minimumCompleteFrames,
        int minimumPartialFrames,
        CancellationToken cancellationToken);

    Task<CleanupProofAppraisalCapture> CaptureCleanupAppraisalAsync(
        CancellationToken cancellationToken);

    Task<CleanupProofIdentityCapture> CaptureCleanupAppraisalIdentityAsync(
        CancellationToken cancellationToken);

    Task<CleanupProofAppraisalCapture> CaptureCurrentCleanupAppraisalAsync(
        CancellationToken cancellationToken);

    Task<AppraisalCarouselAdvanceResult> AdvanceToNextPokemonInAppraisalAsync(
        string previousAppraisalFingerprint,
        CleanupProofAppraisalCapture? confirmedPreSwipeCapture,
        CancellationToken cancellationToken);

    Task<string> CloseInventoryAsync(CancellationToken cancellationToken);
}
