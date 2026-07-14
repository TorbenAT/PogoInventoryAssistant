namespace PogoInventory.Verification.Models;

public enum CalcyVerificationOutcome
{
    ExactComplete,
    SafeIncomplete,
    IncorrectIncomplete,
    WrongComplete,
    Conflicting,
    Failed,
    Unavailable,
    InvalidEvidence
}
