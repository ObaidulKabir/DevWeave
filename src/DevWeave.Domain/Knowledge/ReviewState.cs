namespace DevWeave.Domain.Knowledge;

/// <summary>
/// Human review progress for a knowledge item (SRS KNW-003, KNW-012). This is one of four independent
/// state dimensions: approving an item advances review to <see cref="Reviewed"/> while leaving the
/// verification and currency dimensions untouched, so an item can be approved and still unverified.
/// A new revision resets review to <see cref="Pending"/> until the changed content is reviewed again.
/// </summary>
public enum ReviewState
{
    Pending = 1,
    InReview = 2,
    Reviewed = 3,
}