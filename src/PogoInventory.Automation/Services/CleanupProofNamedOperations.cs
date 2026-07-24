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
        CleanupProofIdentityCapture? confirmedIdentityCapture,
        CancellationToken cancellationToken);

    Task<AppraisalCarouselAdvanceResult> AdvanceToNextPokemonInAppraisalAsync(
        string previousAppraisalFingerprint,
        CleanupProofAppraisalCapture? confirmedPreSwipeCapture,
        CancellationToken cancellationToken);

    Task<string> CloseInventoryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Bounded, input-free re-observation used only when
    /// <see cref="CloseInventoryAsync"/> reported a final state other than
    /// GameplayMap: the close animation can still be settling. Polls captures
    /// within a single <c>StateTimeoutSeconds</c> deadline and never sends
    /// input.
    /// </summary>
    Task<CleanupFinalMapVerification> VerifyGameplayMapSettledAsync(CancellationToken cancellationToken);
}
