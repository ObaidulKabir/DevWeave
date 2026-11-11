using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// A record of a specific commit or change set applied to a codebase target 
/// within the context of a work package (SRS MCP-035).
/// </summary>
public sealed class ChangeSet
{
    public ChangeSetId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public WorkPackageId WorkPackageId { get; }
    public CodebaseTargetId CodebaseTargetId { get; }
    public string ProviderRef { get; }
    public string CommitSha { get; }
    public IReadOnlyList<string> ChangedPathMetadata { get; }
    public DateTimeOffset RecordedAt { get; }

    private ChangeSet(
        ChangeSetId id,
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        CodebaseTargetId codebaseTargetId,
        string providerRef,
        string commitSha,
        IReadOnlyList<string> changedPathMetadata,
        DateTimeOffset recordedAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        WorkPackageId = workPackageId;
        CodebaseTargetId = codebaseTargetId;
        ProviderRef = providerRef;
        CommitSha = commitSha;
        ChangedPathMetadata = changedPathMetadata;
        RecordedAt = recordedAt;
    }

    public static ChangeSet Record(
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        CodebaseTargetId codebaseTargetId,
        string providerRef,
        string commitSha,
        IReadOnlyList<string> changedPathMetadata)
    {
        return new ChangeSet(
            ChangeSetId.New(),
            tenantId,
            projectId,
            workPackageId,
            codebaseTargetId,
            providerRef,
            commitSha,
            changedPathMetadata,
            DateTimeOffset.UtcNow);
    }
}
