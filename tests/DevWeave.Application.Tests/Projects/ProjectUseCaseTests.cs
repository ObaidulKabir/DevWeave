using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Application.Projects;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Projects;

/// <summary>
/// Exercises the project and lifecycle use cases against in-memory fakes, focusing on flow
/// correctness and the cross-tenant deny-by-default invariant (PRJ-001..PRJ-006, PRJ-009, LFC-001).
/// </summary>
public sealed class ProjectUseCaseTests
{
    [Fact]
    public async Task PRJ_002_create_project_pins_a_profile_and_records_owning_tenant_and_actor()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var profile = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "agile", "Agile", "1.0");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");

        var project = await new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "Shop API", "shop-api", "Ecommerce", profile.Id, actor.Id);

        Assert.Equal(tenant.Id, project.TenantId);
        Assert.Equal(profile.Id, project.LifecycleProfileId);
        Assert.Equal(actor.Id, project.CreatedBy);
        Assert.Equal(ProjectStatus.Draft, project.Status);
        Assert.Equal("shop-api", project.Key);
    }

    [Fact]
    public async Task PRJ_002_create_rejects_a_profile_from_another_tenant()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var profileB = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenantB.Id, "agile", "Agile", "1.0");
        var actor = await SeedPrincipalAsync(fakes, tenantA.Id, "a@alphacorp.com");

        var useCase = new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(tenantA.Id, "Shop API", "shop-api", null, profileB.Id, actor.Id));
    }

    [Fact]
    public async Task PRJ_001_create_rejects_an_actor_from_another_tenant()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var profileA = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenantA.Id, "agile", "Agile", "1.0");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");

        var useCase = new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(tenantA.Id, "Shop API", "shop-api", null, profileA.Id, actorB.Id));
    }

    [Fact]
    public async Task PRJ_002_duplicate_key_is_rejected_within_a_tenant_but_allowed_across_tenants()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var profileA = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenantA.Id, "agile", "Agile", "1.0");
        var profileB = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenantB.Id, "agile", "Agile", "1.0");
        var actorA = await SeedPrincipalAsync(fakes, tenantA.Id, "a@alphacorp.com");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");

        var createA = new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        await createA.ExecuteAsync(tenantA.Id, "Shop API", "shared-key", null, profileA.Id, actorA.Id);

        // Same tenant -> conflict.
        await Assert.ThrowsAsync<ConflictException>(() =>
            createA.ExecuteAsync(tenantA.Id, "Other API", "shared-key", null, profileA.Id, actorA.Id));

        // Different tenant -> allowed (key uniqueness is scoped to the tenant).
        var createB = new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var projectB = await createB.ExecuteAsync(tenantB.Id, "Other API", "shared-key", null, profileB.Id, actorB.Id);
        Assert.Equal("shared-key", projectB.Key);
    }

    [Fact]
    public async Task PRJ_004_PRJ_009_archive_restore_and_delete_walk_the_governed_lifecycle()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var profile = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "agile", "Agile", "1.0");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var project = await new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "Shop API", "shop-api", null, profile.Id, actor.Id);

        await new ArchiveProject(fakes.Projects, fakes.UnitOfWork).ExecuteAsync(tenant.Id, project.Id);
        Assert.Equal(ProjectStatus.Archived, project.Status);

        await new RestoreProject(fakes.Projects, fakes.UnitOfWork).ExecuteAsync(tenant.Id, project.Id);
        Assert.Equal(ProjectStatus.Active, project.Status);

        await new ArchiveProject(fakes.Projects, fakes.UnitOfWork).ExecuteAsync(tenant.Id, project.Id);
        await new DeleteProject(fakes.Projects, fakes.UnitOfWork).ExecuteAsync(tenant.Id, project.Id);
        Assert.True(project.IsDeleted);

        // A deleted project is no longer visible through tenant-scoped reads.
        var listed = await new ListProjectsForTenant(fakes.Projects).ExecuteAsync(tenant.Id);
        Assert.Empty(listed);
        Assert.Null(await new GetProject(fakes.Projects).ExecuteAsync(tenant.Id, project.Id));
    }

    [Fact]
    public async Task PRJ_006_add_reference_is_scoped_to_the_owning_tenant()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var profile = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "agile", "Agile", "1.0");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var project = await new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "Shop API", "shop-api", null, profile.Id, actor.Id);

        var reference = await new AddProjectReference(fakes.Projects, fakes.References, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, ProjectReferenceType.Repository, "devweave/api", null);

        Assert.Equal(project.Id, reference.ProjectId);
        Assert.Equal(tenant.Id, reference.TenantId);

        var scoped = await fakes.References.ListByProjectAsync(tenant.Id, project.Id);
        Assert.Single(scoped);

        var crossTenant = await fakes.References.ListByProjectAsync(Tenant.Create("Other").Id, project.Id);
        Assert.Empty(crossTenant);
    }

    [Fact]
    public async Task PRJ_003_grant_and_revoke_membership_is_scoped_to_the_owning_tenant()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var otherTenant = Tenant.Create("Beta Corp");
        var profile = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "agile", "Agile", "1.0");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var project = await new CreateProject(fakes.Projects, fakes.Profiles, fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "Shop API", "shop-api", null, profile.Id, actor.Id);

        var foreignPrincipal = await SeedPrincipalAsync(fakes, otherTenant.Id, "b@betacorp.com");

        var grant = new GrantProjectMembership(fakes.Projects, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        // Cross-tenant principal is rejected.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            grant.ExecuteAsync(tenant.Id, project.Id, foreignPrincipal.Id, ProjectRole.Member));

        var membership = await grant.ExecuteAsync(tenant.Id, project.Id, actor.Id, ProjectRole.Owner);
        Assert.Equal(ProjectMembershipStatus.Active, membership.Status);

        await new RevokeProjectMembership(fakes.Memberships, fakes.UnitOfWork).ExecuteAsync(tenant.Id, membership.Id);
        Assert.Equal(ProjectMembershipStatus.Revoked, membership.Status);
    }

    [Fact]
    public async Task LFC_001_profiles_are_listed_within_the_owning_tenant_only()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");

        var profileA = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenantA.Id, "agile", "Agile", "1.0");
        var profileB = await new CreateLifecycleProfile(fakes.Profiles, fakes.UnitOfWork)
            .ExecuteAsync(tenantB.Id, "waterfall", "Waterfall", "1.0");

        var result = await new ListLifecycleProfilesForTenant(fakes.Profiles).ExecuteAsync(tenantA.Id);

        Assert.Single(result);
        Assert.Equal(profileA.Id, result[0].Id);
        Assert.DoesNotContain(profileB, result);
    }

    private static Fakes CreateFakes() => new();

    private static async Task<Principal> SeedPrincipalAsync(Fakes fakes, TenantId tenantId, string email)
    {
        var principal = Principal.Create(tenantId, email, PrincipalType.Human);
        await fakes.Principals.AddAsync(principal);
        return principal;
    }

    private sealed class Fakes
    {
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryProjectReferenceRepository References { get; } = new();
        public InMemoryProjectMembershipRepository Memberships { get; } = new();
        public InMemoryLifecycleProfileRepository Profiles { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public List<Project> Projects { get; } = new();

        public Task AddAsync(Project project, CancellationToken ct = default)
        {
            Projects.Add(project);
            return Task.CompletedTask;
        }

        public Task<Project?> GetByIdAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<Project>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => p.TenantId == tenantId && !p.IsDeleted).ToList());

        public Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken ct = default) =>
            Task.FromResult(Projects.Any(p => p.TenantId == tenantId && p.Key == key));
    }

    private sealed class InMemoryProjectReferenceRepository : IProjectReferenceRepository
    {
        public List<ProjectReference> References { get; } = new();

        public Task AddAsync(ProjectReference reference, CancellationToken ct = default)
        {
            References.Add(reference);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ProjectReference>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectReference>>(
                References.Where(r => r.TenantId == tenantId && r.ProjectId == projectId).ToList());
    }

    private sealed class InMemoryProjectMembershipRepository : IProjectMembershipRepository
    {
        public List<ProjectMembership> Memberships { get; } = new();

        public Task AddAsync(ProjectMembership membership, CancellationToken ct = default)
        {
            Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task<ProjectMembership?> GetByIdAsync(TenantId tenantId, ProjectMembershipId id, CancellationToken ct = default) =>
            Task.FromResult(Memberships.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                Memberships.Where(m => m.TenantId == tenantId && m.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                Memberships.Where(m => m.TenantId == tenantId && m.PrincipalId == principalId).ToList());
    }

    private sealed class InMemoryLifecycleProfileRepository : ILifecycleProfileRepository
    {
        public List<LifecycleProfile> Profiles { get; } = new();

        public Task AddAsync(LifecycleProfile profile, CancellationToken ct = default)
        {
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task<LifecycleProfile?> GetByIdAsync(TenantId tenantId, LifecycleProfileId id, CancellationToken ct = default) =>
            Task.FromResult(Profiles.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));

        public Task<IReadOnlyList<LifecycleProfile>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LifecycleProfile>>(Profiles.Where(p => p.TenantId == tenantId).ToList());
    }

    private sealed class InMemoryPrincipalRepository : IPrincipalRepository
    {
        public List<Principal> Principals { get; } = new();

        public Task AddAsync(Principal principal, CancellationToken ct = default)
        {
            Principals.Add(principal);
            return Task.CompletedTask;
        }

        public Task<Principal?> GetByIdAsync(TenantId tenantId, PrincipalId id, CancellationToken ct = default) =>
            Task.FromResult(Principals.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}