using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Projects;

/// <summary>
/// Proves PRJ-002, PRJ-004 and PRJ-009: a project carries a stable identity, owning tenant, a
/// tenant-unique immutable key, and a governed Draft/Active/OnHold/Completed/Archived lifecycle
/// where archive/restore/delete are recorded events.
/// </summary>
public sealed class ProjectLifecycleTests
{
    [Fact]
    public void PRJ_002_create_establishes_identity_ownership_metadata_and_draft_status()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var profile = LifecycleProfile.Create(tenant.Id, "agile", "Agile", "1.0");
        var reactor = Principal.Create(tenant.Id, "a@alphacorp.com", PrincipalType.Human);

        var project = Project.Create(tenant.Id, "Shop API", "shop-api", "Ecommerce backend", profile.Id, reactor.Id);

        Assert.NotEqual(Guid.Empty, project.Id.Value);
        Assert.Equal(tenant.Id, project.TenantId);
        Assert.Equal("Shop API", project.Name);
        Assert.Equal("shop-api", project.Key);
        Assert.Equal("Ecommerce backend", project.Description);
        Assert.Equal(ProjectStatus.Draft, project.Status);
        Assert.Equal(profile.Id, project.LifecycleProfileId);
        Assert.Equal(reactor.Id, project.CreatedBy);
        Assert.False(project.IsDeleted);
        Assert.Null(project.DeletedAt);
    }

    [Fact]
    public void PRJ_002_key_is_required()
    {
        var tenant = Tenant.Create("Alpha Corp");

        Assert.Throws<ArgumentException>(() =>
            Project.Create(tenant.Id, "Shop API", " ", null, LifecycleProfileId.New(), PrincipalId.New()));
    }

    [Fact]
    public void PRJ_002_name_is_required()
    {
        var tenant = Tenant.Create("Alpha Corp");

        Assert.Throws<ArgumentException>(() =>
            Project.Create(tenant.Id, " ", "shop-api", null, LifecycleProfileId.New(), PrincipalId.New()));
    }

    [Fact]
    public void PRJ_002_key_is_immutable_after_creation()
    {
        var key = typeof(Project).GetProperty(nameof(Project.Key))!;

        Assert.False(key.CanWrite);
    }

    [Fact]
    public void PRJ_002_same_key_is_allowed_across_different_tenants()
    {
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");

        var a = CreateDraft(tenantA.Id, "shared-key");
        var b = CreateDraft(tenantB.Id, "shared-key");

        Assert.Equal("shared-key", a.Key);
        Assert.Equal("shared-key", b.Key);
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void PRJ_004_draft_flows_through_active_on_hold_completed_and_archived()
    {
        var project = CreateDraft();

        project.Activate();
        Assert.Equal(ProjectStatus.Active, project.Status);

        project.OnHold();
        Assert.Equal(ProjectStatus.OnHold, project.Status);

        project.Activate();
        Assert.Equal(ProjectStatus.Active, project.Status);

        project.Complete();
        Assert.Equal(ProjectStatus.Completed, project.Status);

        project.Archive();
        Assert.Equal(ProjectStatus.Archived, project.Status);
        Assert.NotNull(project.ArchivedAt);

        project.Restore();
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.NotNull(project.RestoredAt);
    }

    [Fact]
    public void PRJ_004_illegal_transitions_are_rejected()
    {
        var draft = CreateDraft();
        Assert.Throws<InvalidOperationException>(() => draft.OnHold());
        Assert.Throws<InvalidOperationException>(() => draft.Complete());

        var active = CreateDraft();
        active.Activate();
        Assert.Throws<InvalidOperationException>(() => active.Activate());

        var archived = CreateDraft();
        archived.Archive();
        Assert.Throws<InvalidOperationException>(() => archived.Archive());
        Assert.Throws<InvalidOperationException>(() => archived.Complete());
    }

    [Fact]
    public void PRJ_009_delete_requires_archive_and_is_recorded_not_destructive()
    {
        var draft = CreateDraft();
        Assert.Throws<InvalidOperationException>(() => draft.Delete());

        var project = CreateDraft();
        project.Archive();
        project.Delete();

        Assert.True(project.IsDeleted);
        Assert.NotNull(project.DeletedAt);
        Assert.Equal(ProjectStatus.Archived, project.Status);

        Assert.Throws<InvalidOperationException>(() => project.Delete());
        Assert.Throws<InvalidOperationException>(() => project.Restore());
    }

    [Fact]
    public void PRJ_002_project_and_its_children_carry_exactly_one_tenant()
    {
        var tenantA = Tenant.Create("Alpha Corp");
        var project = CreateDraft(tenantA.Id);

        var reference = ProjectReference.Create(tenantA.Id, project.Id, ProjectReferenceType.Repository, "devweave/api", null);
        var membership = ProjectMembership.Grant(tenantA.Id, project.Id, PrincipalId.New(), ProjectRole.Member);
        var grant = ProjectGrant.Grant(tenantA.Id, project.Id, PrincipalId.New(), ProjectGrantScope.Deploy);

        Assert.Equal(tenantA.Id, project.TenantId);
        Assert.Equal(tenantA.Id, reference.TenantId);
        Assert.Equal(tenantA.Id, membership.TenantId);
        Assert.Equal(tenantA.Id, grant.TenantId);
    }

    private static Project CreateDraft(TenantId? tenantId = null, string key = "shop-api")
    {
        var tenant = tenantId ?? Tenant.Create("Alpha Corp").Id;
        return Project.Create(
            tenant,
            "Shop API",
            key,
            "Ecommerce backend",
            LifecycleProfileId.New(),
            PrincipalId.New());
    }
}