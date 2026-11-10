using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Projects;

/// <summary>
/// A typed reference to an external system owned by the tenant but referenced (not duplicated) by a
/// project (SRS PRJ-005/PRJ-006). References are immutable external identifiers.
/// </summary>
public sealed class ProjectReference
{
    public ProjectReferenceId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public ProjectReferenceType Type { get; }
    public string ExternalId { get; }
    public string? Url { get; }
    public DateTimeOffset CreatedAt { get; }

    private ProjectReference(
        ProjectReferenceId id,
        TenantId tenantId,
        ProjectId projectId,
        ProjectReferenceType type,
        string externalId,
        string? url,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Type = type;
        ExternalId = externalId;
        Url = url;
        CreatedAt = createdAt;
    }

    public static ProjectReference Create(
        TenantId tenantId,
        ProjectId projectId,
        ProjectReferenceType type,
        string externalId,
        string? url)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External identifier is required.", nameof(externalId));

        return new ProjectReference(
            ProjectReferenceId.New(),
            tenantId,
            projectId,
            type,
            externalId,
            url,
            DateTimeOffset.UtcNow);
    }
}