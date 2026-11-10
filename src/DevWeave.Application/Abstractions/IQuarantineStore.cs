namespace DevWeave.Application.Abstractions;

/// <summary>
/// A restricted, bounded-lifetime store for quarantined artifact bytes (SRS ART-008, SRS 7.7). Quarantine
/// is deliberately separate from the canonical <see cref="IObjectStorage"/>: flagged bytes must never be
/// served, indexed or exported, and must not become permanent source retention. Every object is written
/// with an explicit expiry and swept by <c>PurgeExpiredQuarantine</c> — there is no ordinary read path,
/// so quarantined content is write-only from the platform's perspective.
/// </summary>
public interface IQuarantineStore
{
    /// <summary>
    /// Persists quarantined bytes into the isolated quarantine namespace and returns the opaque key the
    /// byte payload lives under. The object is retained only until <paramref name="expiresAt"/>.
    /// </summary>
    Task<string> PutAsync(
        string artifactKey,
        byte[] content,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken ct = default);

    /// <summary>The quarantine keys whose bounded lifetime has elapsed as of <paramref name="now"/>.</summary>
    Task<IReadOnlyList<string>> ListExpiredAsync(DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Deletes a quarantined object. Idempotent: a missing key is a no-op.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
