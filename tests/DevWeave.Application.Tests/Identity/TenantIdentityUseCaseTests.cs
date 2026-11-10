using DevWeave.Application.Abstractions;
using DevWeave.Application.Identity;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Tests.Identity;

/// <summary>
/// Exercises the application use cases against in-memory fake repositories, focusing on flow
/// correctness and the tenant-scope invariant (SRS TEN-001..TEN-004).
/// </summary>
public sealed class TenantIdentityUseCaseTests
{
    [Fact]
    public async Task TEN_001_create_tenant_produces_an_independent_tenant()
    {
        var fakes = CreateFakes();
        var useCase = new CreateTenant(fakes.Tenants, fakes.UnitOfWork);

        var tenant = await useCase.ExecuteAsync("Alpha Corp");

        Assert.NotEqual(Guid.Empty, tenant.Id.Value);
        Assert.Equal("Alpha Corp", tenant.Name);
    }

    [Fact]
    public async Task TEN_003_create_principal_records_its_type_and_owning_tenant()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var useCase = new CreatePrincipal(fakes.Principals, fakes.UnitOfWork);

        var principal = await useCase.ExecuteAsync(tenant.Id, "agent@alphacorp.com", PrincipalType.AiAgent);

        Assert.Equal(tenant.Id, principal.TenantId);
        Assert.Equal(PrincipalType.AiAgent, principal.Type);
    }

    [Fact]
    public async Task TEN_004_invite_activate_and_revoke_walk_the_membership_lifecycle()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");

        var principal = await new CreatePrincipal(fakes.Principals, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "user@alphacorp.com", PrincipalType.Human);

        var membership = await new InviteMember(fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, principal.Id, MembershipRole.Member);
        Assert.Equal(MembershipStatus.Invited, membership.Status);

        await new ActivateMember(fakes.Memberships, fakes.UnitOfWork).ExecuteAsync(tenant.Id, membership.Id);
        Assert.Equal(MembershipStatus.Active, membership.Status);

        await new RevokeMember(fakes.Memberships, fakes.UnitOfWork).ExecuteAsync(tenant.Id, membership.Id);
        Assert.Equal(MembershipStatus.Revoked, membership.Status);
    }

    [Fact]
    public async Task TEN_002_invite_rejects_a_principal_that_belongs_to_another_tenant()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");

        var principalB = Principal.Create(tenantB.Id, "user@betacorp.com", PrincipalType.Human);
        await fakes.Principals.AddAsync(principalB);

        var invite = new InviteMember(fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            invite.ExecuteAsync(tenantA.Id, principalB.Id, MembershipRole.Member));
    }

    [Fact]
    public async Task TEN_002_list_members_is_scoped_to_the_owning_tenant()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");

        var principalA = await new CreatePrincipal(fakes.Principals, fakes.UnitOfWork)
            .ExecuteAsync(tenantA.Id, "a@alphacorp.com", PrincipalType.Human);
        var principalB = await new CreatePrincipal(fakes.Principals, fakes.UnitOfWork)
            .ExecuteAsync(tenantB.Id, "b@betacorp.com", PrincipalType.Human);

        var invite = new InviteMember(fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var membershipA = await invite.ExecuteAsync(tenantA.Id, principalA.Id, MembershipRole.Member);
        var membershipB = await invite.ExecuteAsync(tenantB.Id, principalB.Id, MembershipRole.Member);

        var result = await new ListMembersForTenant(fakes.Memberships).ExecuteAsync(tenantA.Id);

        Assert.Single(result);
        Assert.Equal(membershipA.Id, result[0].Id);
        Assert.DoesNotContain(membershipB, result);
    }

    private static Fakes CreateFakes() => new();

    private sealed class Fakes
    {
        public InMemoryTenantRepository Tenants { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryMembershipRepository Memberships { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryTenantRepository : ITenantRepository
    {
        public List<Tenant> Tenants { get; } = new();

        public Task AddAsync(Tenant tenant, CancellationToken ct = default)
        {
            Tenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task<Tenant?> GetByIdAsync(TenantId id, CancellationToken ct = default) =>
            Task.FromResult(Tenants.FirstOrDefault(t => t.Id == id));
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

    private sealed class InMemoryMembershipRepository : IMembershipRepository
    {
        public List<Membership> Memberships { get; } = new();

        public Task AddAsync(Membership membership, CancellationToken ct = default)
        {
            Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task<Membership?> GetByIdAsync(TenantId tenantId, MembershipId id, CancellationToken ct = default) =>
            Task.FromResult(Memberships.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));

        public Task<IReadOnlyList<Membership>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Membership>>(Memberships.Where(m => m.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<Membership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Membership>>(
                Memberships.Where(m => m.TenantId == tenantId && m.PrincipalId == principalId).ToList());
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}