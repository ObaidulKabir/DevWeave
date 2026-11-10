namespace DevWeave.Application.Abstractions;

/// <summary>
/// Port for one-way secret hashing (SRS AGT-002). Only the hash of an agent secret is persisted; the
/// plaintext is returned to the caller exactly once at issuance. The concrete implementation lives in
/// the Infrastructure layer so the Domain and Application stay free of crypto libraries.
/// </summary>
public interface ISecretHasher
{
    /// <summary>Derives a one-way, salted hash of the secret for storage (AGT-002).</summary>
    string Hash(string secret);

    /// <summary>Returns true when the supplied secret matches the stored hash, in constant time.</summary>
    bool Verify(string secret, string hash);
}