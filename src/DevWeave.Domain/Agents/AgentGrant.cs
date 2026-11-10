using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Agents;

/// <summary>
/// A capability granted to an AI agent principal, tenant-scoped and optionally narrowed to a single
/// project (SRS AGT-002, AGT-003, AGT-013). Grants follow a governed lifecycle: a human proposes the
/// grant, reviews and approves it, and may later revoke it. Approval and revocation are audited through
/// the recorded approver/revoker and timestamps; a <see langword="null"/> project means the capability
/// applies tenant-wide. A grant also carries a classification ceiling, bounding the most sensitive
/// knowledge tier the agent may read.
/// </summary>
public sealed class AgentGrant
{
    public AgentGrantId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId? ProjectId { get; }
    public PrincipalId AgentId { get; }
    public AgentCapability Capability { get; }
    public Classification ClassificationCeiling { get; }
    public AgentGrantStatus Status { get; private set; }
    public DateTimeOffset GrantedAt { get; }
    public DateTimeOffset? ExpiresAt { get; }
    public PrincipalId? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public PrincipalId? RevokedBy { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private AgentGrant(
        AgentGrantId id,
        TenantId tenantId,
        ProjectId? projectId,
        PrincipalId agentId,
        AgentCapability capability,
        Classification classificationCeiling,
        AgentGrantStatus status,
        DateTimeOffset grantedAt,
        DateTimeOffset? expiresAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        AgentId = agentId;
        Capability = capability;
        ClassificationCeiling = classificationCeiling;
        Status = status;
        GrantedAt = grantedAt;
        ExpiresAt = expiresAt;
    }

    public static AgentGrant Propose(
        TenantId tenantId,
        ProjectId? projectId,
        PrincipalId agentId,
        AgentCapability capability,
        DateTimeOffset? expiresAt,
        Classification classificationCeiling = Classification.Internal)
    {
        return new AgentGrant(
            AgentGrantId.New(),
            tenantId,
            projectId,
            agentId,
            capability,
            classificationCeiling,
            AgentGrantStatus.Proposed,
            DateTimeOffset.UtcNow,
            expiresAt);
    }

    public void Approve(PrincipalId approver)
    {
        if (Status == AgentGrantStatus.Approved)
            throw new InvalidOperationException("Agent grant is already approved.");

        if (Status == AgentGrantStatus.Revoked)
            throw new InvalidOperationException("A revoked agent grant cannot be approved.");

        Status = AgentGrantStatus.Approved;
        ApprovedBy = approver;
        ApprovedAt = DateTimeOffset.UtcNow;
    }

    public void Revoke(PrincipalId revoker)
    {
        if (Status == AgentGrantStatus.Revoked)
            throw new InvalidOperationException("Agent grant is already revoked.");

        Status = AgentGrantStatus.Revoked;
        RevokedBy = revoker;
        RevokedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>A grant is effective only once approved and not past its expiry (AGT-002).</summary>
    public bool IsActive(DateTimeOffset at) =>
        Status == AgentGrantStatus.Approved && (ExpiresAt is null || at < ExpiresAt);

    /// <summary>A tenant-wide grant covers every project; otherwise the grant covers only its own.</summary>
    public bool CoversProject(ProjectId projectId) => ProjectId is null || ProjectId == projectId;

    /// <summary>Whether this grant authorizes context-package assembly over covered projects (AGT-004).</summary>
    public bool AllowsContextAssembly =>
        Capability is AgentCapability.ReadKnowledge or AgentCapability.AssembleContext;
}