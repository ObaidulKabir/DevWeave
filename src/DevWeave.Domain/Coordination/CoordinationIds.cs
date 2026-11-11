namespace DevWeave.Domain.Coordination;

public record ChannelId(Guid Value)
{
    public static ChannelId New() => new(Guid.NewGuid());
}

public record MessageId(Guid Value)
{
    public static MessageId New() => new(Guid.NewGuid());
}

public record CampaignId(Guid Value)
{
    public static CampaignId New() => new(Guid.NewGuid());
}

public record WorkPackageId(Guid Value)
{
    public static WorkPackageId New() => new(Guid.NewGuid());
}

public record LeaseId(Guid Value)
{
    public static LeaseId New() => new(Guid.NewGuid());
}

public record RuntimeId(Guid Value)
{
    public static RuntimeId New() => new(Guid.NewGuid());
}

public record BaselineId(Guid Value)
{
    public static BaselineId New() => new(Guid.NewGuid());
}

public record CodebaseTargetId(Guid Value)
{
    public static CodebaseTargetId New() => new(Guid.NewGuid());
}

public record ChangeSetId(Guid Value)
{
    public static ChangeSetId New() => new(Guid.NewGuid());
}

public record ParticipationOfferId(Guid Value)
{
    public static ParticipationOfferId New() => new(Guid.NewGuid());
}

public record IntegrationQueueItemId(Guid Value)
{
    public static IntegrationQueueItemId New() => new(Guid.NewGuid());
}
