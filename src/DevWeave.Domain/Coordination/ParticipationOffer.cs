using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum ParticipationOfferStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired
}

/// <summary>
/// A time-bounded offer made to an agent to participate in a campaign or work package (SRS MCO-BR-04).
/// </summary>
public sealed class ParticipationOffer
{
    public ParticipationOfferId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public CampaignId CampaignId { get; }
    public RuntimeId RuntimeId { get; }
    public IReadOnlyList<string> RequiredCapabilities { get; }
    public DateTimeOffset AvailableUntil { get; }
    public ParticipationOfferStatus Status { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private ParticipationOffer(
        ParticipationOfferId id,
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        RuntimeId runtimeId,
        IReadOnlyList<string> requiredCapabilities,
        DateTimeOffset availableUntil,
        ParticipationOfferStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        CampaignId = campaignId;
        RuntimeId = runtimeId;
        RequiredCapabilities = requiredCapabilities;
        AvailableUntil = availableUntil;
        Status = status;
        CreatedAt = createdAt;
    }

    public static ParticipationOffer Create(
        TenantId tenantId,
        ProjectId projectId,
        CampaignId campaignId,
        RuntimeId runtimeId,
        IReadOnlyList<string> requiredCapabilities,
        DateTimeOffset availableUntil)
    {
        return new ParticipationOffer(
            ParticipationOfferId.New(),
            tenantId,
            projectId,
            campaignId,
            runtimeId,
            requiredCapabilities,
            availableUntil,
            ParticipationOfferStatus.Pending,
            DateTimeOffset.UtcNow);
    }

    public void Decide(bool accept, string? reason)
    {
        if (Status != ParticipationOfferStatus.Pending)
            throw new InvalidOperationException("Offer already decided.");

        if (DateTimeOffset.UtcNow > AvailableUntil)
        {
            Status = ParticipationOfferStatus.Expired;
        }
        else
        {
            Status = accept ? ParticipationOfferStatus.Accepted : ParticipationOfferStatus.Rejected;
        }

        Reason = reason;
        DecidedAt = DateTimeOffset.UtcNow;
    }
}
