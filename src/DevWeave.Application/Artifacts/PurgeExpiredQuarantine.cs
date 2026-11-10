using DevWeave.Application.Abstractions;

namespace DevWeave.Application.Artifacts;

/// <summary>
/// The explicit cleanup half of the quarantine disposition (SRS ART-008). It enumerates the quarantine
/// store for objects whose bounded lifetime has elapsed and deletes their bytes, so quarantined content
/// is transient by construction rather than permanent source retention. Deletion is idempotent, so a
/// repeated sweep is safe. Runs from the retention worker on a fixed cadence.
/// </summary>
public sealed class PurgeExpiredQuarantine
{
    private readonly IQuarantineStore _quarantine;

    public PurgeExpiredQuarantine(IQuarantineStore quarantine)
    {
        _quarantine = quarantine;
    }

    public async Task<int> ExecuteAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var expired = await _quarantine.ListExpiredAsync(now, ct);

        foreach (var key in expired)
            await _quarantine.DeleteAsync(key, ct);

        return expired.Count;
    }
}
