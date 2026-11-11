namespace DevWeave.Domain.Integrations;

/// <summary>
/// Records an inbound event from an integration for deduplication and processing history (SRS INT-004, DATA-07).
/// </summary>
public sealed class InboundEvent
{
    public InboundEventId Id { get; private set; }
    public IntegrationId IntegrationId { get; private set; }
    public string ProviderEventId { get; private set; } = string.Empty;
    public DateTimeOffset SentAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public string MetadataJson { get; private set; } = string.Empty;
    public DateTimeOffset? ProcessedAt { get; private set; }

    private InboundEvent() { }

    public static InboundEvent Record(
        IntegrationId integrationId,
        string providerEventId,
        DateTimeOffset sentAt,
        string metadataJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataJson);

        return new InboundEvent
        {
            Id = InboundEventId.New(),
            IntegrationId = integrationId,
            ProviderEventId = providerEventId,
            SentAt = sentAt,
            ReceivedAt = DateTimeOffset.UtcNow,
            MetadataJson = metadataJson
        };
    }

    public void MarkProcessed()
    {
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
