using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// A single, durable message within a coordination channel (SRS MCP-035).
/// </summary>
public sealed class CoordinationMessage
{
    public MessageId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public ChannelId ChannelId { get; }
    public PrincipalId SenderId { get; }
    public RuntimeId RuntimeId { get; }
    public MessageId? ParentMessageId { get; }
    public long Sequence { get; }
    public string MessageType { get; }
    public string SchemaVersion { get; }
    public string PayloadJson { get; }
    public string IdempotencyKey { get; }
    public DateTimeOffset CreatedAt { get; }

    private CoordinationMessage(
        MessageId id,
        TenantId tenantId,
        ProjectId projectId,
        ChannelId channelId,
        PrincipalId senderId,
        RuntimeId runtimeId,
        MessageId? parentMessageId,
        long sequence,
        string messageType,
        string schemaVersion,
        string payloadJson,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        ChannelId = channelId;
        SenderId = senderId;
        RuntimeId = runtimeId;
        ParentMessageId = parentMessageId;
        Sequence = sequence;
        MessageType = messageType;
        SchemaVersion = schemaVersion;
        PayloadJson = payloadJson;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
    }

    public static CoordinationMessage Create(
        TenantId tenantId,
        ProjectId projectId,
        ChannelId channelId,
        PrincipalId senderId,
        RuntimeId runtimeId,
        long sequence,
        string messageType,
        string schemaVersion,
        string payloadJson,
        string idempotencyKey,
        MessageId? parentMessageId = null)
    {
        return new CoordinationMessage(
            MessageId.New(),
            tenantId,
            projectId,
            channelId,
            senderId,
            runtimeId,
            parentMessageId,
            sequence,
            messageType,
            schemaVersion,
            payloadJson,
            idempotencyKey,
            DateTimeOffset.UtcNow);
    }
}
