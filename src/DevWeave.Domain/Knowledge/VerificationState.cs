namespace DevWeave.Domain.Knowledge;

/// <summary>
/// Independent verification status for a knowledge item (SRS KNW-003). Advanced to Verified when
/// the information lifecycle reaches <see cref="InfoState.Verified"/>.
/// </summary>
public enum VerificationState
{
    Unverified = 1,
    Verified = 2,
}