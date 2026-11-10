using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Projects;

/// <summary>
/// Proves PRJ-005/PRJ-006: a project references external systems through typed references carrying
/// an external identifier or URL rather than owned/duplicated data.
/// </summary>
public sealed class ProjectReferenceTests
{
    [Fact]
    public void PRJ_006_repository_reference_carries_type_identifier_and_url()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = CreateProject(tenant.Id);

        var reference = ProjectReference.Create(
            tenant.Id,
            project.Id,
            ProjectReferenceType.Repository,
            "devweave/api",
            "https://github.com/devweave/api");

        Assert.Equal(project.Id, reference.ProjectId);
        Assert.Equal(ProjectReferenceType.Repository, reference.Type);
        Assert.Equal("devweave/api", reference.ExternalId);
        Assert.Equal("https://github.com/devweave/api", reference.Url);
        Assert.NotEqual(Guid.Empty, reference.Id.Value);
    }

    [Fact]
    public void PRJ_006_typed_references_cover_repository_database_environment_and_service()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = CreateProject(tenant.Id);

        var repository = ProjectReference.Create(tenant.Id, project.Id, ProjectReferenceType.Repository, "devweave/api", null);
        var database = ProjectReference.Create(tenant.Id, project.Id, ProjectReferenceType.Database, "orders-db", null);
        var environment = ProjectReference.Create(tenant.Id, project.Id, ProjectReferenceType.DeploymentEnvironment, "prod", null);
        var service = ProjectReference.Create(tenant.Id, project.Id, ProjectReferenceType.Service, "payments", null);

        Assert.Equal(ProjectReferenceType.Repository, repository.Type);
        Assert.Equal(ProjectReferenceType.Database, database.Type);
        Assert.Equal(ProjectReferenceType.DeploymentEnvironment, environment.Type);
        Assert.Equal(ProjectReferenceType.Service, service.Type);
    }

    [Fact]
    public void PRJ_006_external_identifier_is_required()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = CreateProject(tenant.Id);

        Assert.Throws<ArgumentException>(() =>
            ProjectReference.Create(tenant.Id, project.Id, ProjectReferenceType.Database, " ", null));
    }

    private static Project CreateProject(TenantId tenantId) =>
        Project.Create(
            tenantId,
            "Shop API",
            "shop-api",
            null,
            LifecycleProfileId.New(),
            PrincipalId.New());
}