using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Integrations;

/// <summary>
/// A tenant-scoped connection to an external provider (SRS INT-001, INT-002, INT-003, INT-004, INT-007, INT-008).
/// Maintains the registry of connections and their current credential references.
/// </summary>
public sealed class Integration
{
    public IntegrationId Id { get; private set; }
    public TenantId TenantId { get; private set; } = default!;
    public string Provider { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;
    public string CredentialRef { get; private set; } = string.Empty;
    public IntegrationStatus Status { get; private set; }
    public IReadOnlyList<string> AllowedMetadataFields { get; private set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Integration() { }

    public static Integration Connect(
        TenantId tenantId,
        string provider,
        string scope,
        string credentialRef,
        IReadOnlyList<string>? allowedMetadataFields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialRef);

        return new Integration
        {
            Id = IntegrationId.New(),
            TenantId = tenantId,
            Provider = provider,
            Scope = scope,
            CredentialRef = credentialRef,
            Status = IntegrationStatus.Connected,
            AllowedMetadataFields = allowedMetadataFields ?? Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void RotateCredential(string newCredentialRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newCredentialRef);
        CredentialRef = newCredentialRef;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Disconnect()
    {
        Status = IntegrationStatus.Disconnected;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Suspend()
    {
        Status = IntegrationStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
