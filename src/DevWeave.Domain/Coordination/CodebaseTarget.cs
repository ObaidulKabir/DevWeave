using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

/// <summary>
/// Defines a specific repository and branch where changes are intended to be applied (SRS MCP-035).
/// </summary>
public sealed class CodebaseTarget
{
    public CodebaseTargetId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string Provider { get; }
    public string RepositoryId { get; }
    public string TargetBranch { get; }
    public string BaseCommitSha { get; }
    public DateTimeOffset CreatedAt { get; }

    private CodebaseTarget(
        CodebaseTargetId id,
        TenantId tenantId,
        ProjectId projectId,
        string provider,
        string repositoryId,
        string targetBranch,
        string baseCommitSha,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Provider = provider;
        RepositoryId = repositoryId;
        TargetBranch = targetBranch;
        BaseCommitSha = baseCommitSha;
        CreatedAt = createdAt;
    }

    public static CodebaseTarget Register(
        TenantId tenantId,
        ProjectId projectId,
        string provider,
        string repositoryId,
        string targetBranch,
        string baseCommitSha)
    {
        return new CodebaseTarget(
            CodebaseTargetId.New(),
            tenantId,
            projectId,
            provider,
            repositoryId,
            targetBranch,
            baseCommitSha,
            DateTimeOffset.UtcNow);
    }
}
