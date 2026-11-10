namespace DevWeave.Domain.Knowledge;

/// <summary>
/// Currency of a knowledge item's current content (SRS KNW-003). The current-revision pointer on
/// the item is the authoritative signal of which revision is live; this typed field records whether
/// the item's content has been superseded by later work.
/// </summary>
public enum CurrencyState
{
    Current = 1,
    Superseded = 2,
}