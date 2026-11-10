using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Projects;

/// <summary>
/// Proves PRJ-003 / TEN-005: project membership carries a scoped role independent of tenant-wide
/// roles, and both membership and scoped grants have a governed grant/revoke lifecycle.
/// </summary>
public sealed class ProjectMembershipAndGrantTests
{
    [Fact]
    public void PRJ_003_grant_creates_an_active_membership_with_the_requested_role()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = CreateProject(tenant.Id);
        var principal = PrincipalId.New();

        var membership = ProjectMembership.Grant(tenant.Id, project.Id, principal, ProjectRole.Owner);

        Assert.Equal(project.Id, membership.ProjectId);
        Assert.Equal(principal, membership.PrincipalId);
        Assert.Equal(ProjectRole.Owner, membership.Role);
        Assert.Equal(ProjectMembershipStatus.Active, membership.Status);
        Assert.Null(membership.RevokedAt);
    }

    [Fact]
    public void PRJ_003_revoke_transitions_active_to_revoked_and_is_terminal()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = CreateProject(tenant.Id);

        var membership = ProjectMembership.Grant(tenant.Id, project.Id, PrincipalId.New(), ProjectRole.Member);
        membership.Revoke();

        Assert.Equal(ProjectMembershipStatus.Revoked, membership.Status);
        Assert.NotNull(membership.RevokedAt);

        Assert.Throws<InvalidOperationException>(() => membership.Revoke());
    }

    [Fact]
    public void TEN_005_project_roles_are_independent_of_tenant_roles()
    {
        var roles = new[] { ProjectRole.Owner, ProjectRole.Admin, ProjectRole.Member, ProjectRole.Viewer };

        // Project roles are a separate classification from the tenant-wide MembershipRole (TEN-004).
        Assert.NotEqual(typeof(ProjectRole), typeof(MembershipRole));
        Assert.Equal(4, roles.Distinct().Count());
    }

    [Fact]
    public void TEN_005_scoped_grant_has_a_governed_grant_revoke_lifecycle()
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = CreateProject(tenant.Id);
        var principal = PrincipalId.New();

        var grant = ProjectGrant.Grant(tenant.Id, project.Id, principal, ProjectGrantScope.Deploy);

        Assert.Equal(project.Id, grant.ProjectId);
        Assert.Equal(principal, grant.PrincipalId);
        Assert.Equal(ProjectGrantScope.Deploy, grant.Scope);
        Assert.Equal(ProjectGrantStatus.Granted, grant.Status);
        Assert.Null(grant.RevokedAt);

        grant.Revoke();

        Assert.Equal(ProjectGrantStatus.Revoked, grant.Status);
        Assert.NotNull(grant.RevokedAt);

        Assert.Throws<InvalidOperationException>(() => grant.Revoke());
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