using DevWeave.Application.Abstractions;
using DevWeave.Application.Evidence;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Tests.Evidence;

/// <summary>
/// Exercises the Evidence use cases (SRS PRO-006) against in-memory fakes, focusing on flow
/// correctness: record-review-list, the origin-must-be-a-claim guard, and the claim-verification rule
/// that a claim stays unverified until at least one evidence record is accepted (PRO-005/PRO-006).
/// </summary>
public sealed class EvidenceUseCaseTests
{
    [Fact]
    public async Task PRO_006_record_review_and_list_walk_the_governed_lifecycle()
    {
        var (fakes, seed) = await SeedAsync();
        var reviewer = await SeedPrincipalAsync(fakes, seed.Tenant, "reviewer@alphacorp.com");

        var record = new RecordEvidence(fakes.Evidence, fakes.Activities, fakes.Artifacts, fakes.UnitOfWork);
        var evidence = await record.ExecuteAsync(
            seed.Tenant, seed.ClaimId, "screenshot", "sha256:aa", null, "AC-1");

        Assert.Equal(seed.ClaimId, evidence.OriginActivityId);
        Assert.Equal(seed.Project, evidence.ProjectId);
        Assert.Equal(EvidenceReviewState.Unreviewed, evidence.ReviewState);
        Assert.Null(evidence.ReviewedBy);

        var listedForClaim = await new ListEvidenceForClaim(fakes.Evidence).ExecuteAsync(seed.Tenant, seed.ClaimId);
        Assert.Single(listedForClaim);

        var listedForProject = await new ListEvidenceForProject(fakes.Evidence).ExecuteAsync(seed.Tenant, seed.Project);
        Assert.Single(listedForProject);
        Assert.Equal(evidence.Id, listedForProject[0].Id);

        var reviewed = await new ReviewEvidence(fakes.Evidence, fakes.Principals, fakes.UnitOfWork)
            .ExecuteAsync(seed.Tenant, evidence.Id, EvidenceReviewState.Accepted, reviewer.Id);

        Assert.Equal(EvidenceReviewState.Accepted, reviewed.ReviewState);
        Assert.Equal(reviewer.Id, reviewed.ReviewedBy);
        Assert.NotNull(reviewed.ReviewedAt);

        // A no-op or illegal transition is surfaced as a conflict rather than applied.
        await Assert.ThrowsAsync<ConflictException>(() =>
            new ReviewEvidence(fakes.Evidence, fakes.Principals, fakes.UnitOfWork)
                .ExecuteAsync(seed.Tenant, evidence.Id, EvidenceReviewState.Accepted, reviewer.Id));
    }

    [Fact]
    public async Task PRO_006_claim_remains_unverified_until_an_evidence_record_is_accepted()
    {
        var (fakes, seed) = await SeedAsync();
        var reviewer = await SeedPrincipalAsync(fakes, seed.Tenant, "reviewer@alphacorp.com");
        var verification = new GetClaimVerification(fakes.Activities, fakes.Evidence);

        // No evidence: unverified.
        var empty = await verification.ExecuteAsync(seed.Tenant, seed.ClaimId);
        Assert.False(empty.IsVerified);
        Assert.Empty(empty.Evidence);

        // Unreviewed evidence: still unverified.
        var evidence = await new RecordEvidence(fakes.Evidence, fakes.Activities, fakes.Artifacts, fakes.UnitOfWork)
            .ExecuteAsync(seed.Tenant, seed.ClaimId, "screenshot", "sha256:aa", null, "AC-1");
        Assert.False((await verification.ExecuteAsync(seed.Tenant, seed.ClaimId)).IsVerified);

        // Accepted evidence: verified (PRO-005/PRO-006).
        await new ReviewEvidence(fakes.Evidence, fakes.Principals, fakes.UnitOfWork)
            .ExecuteAsync(seed.Tenant, evidence.Id, EvidenceReviewState.Accepted, reviewer.Id);

        var verified = await verification.ExecuteAsync(seed.Tenant, seed.ClaimId);
        Assert.True(verified.IsVerified);
        Assert.Single(verified.Evidence);
        Assert.Equal(seed.ClaimId.Value, verified.ClaimId);
    }

    [Fact]
    public async Task PRO_006_evidence_requires_a_claim_origin_for_the_acting_tenant()
    {
        var (fakes, seed) = await SeedAsync();
        var record = new RecordEvidence(fakes.Evidence, fakes.Activities, fakes.Artifacts, fakes.UnitOfWork);

        // A missing claim inside the tenant is indistinguishable from a cross-tenant one.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            record.ExecuteAsync(seed.Tenant, ActivityId.New(), "screenshot", null, null, "AC-1"));

        // A non-claim activity cannot receive evidence.
        var nonClaim = Activity.Create(
            seed.Tenant, seed.Project, ActivityType.Generated, seed.Actor, "generate", ActivityStatus.Completed,
            DateTimeOffset.UtcNow, null, null, null, null, Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>());
        await fakes.Activities.AddAsync(nonClaim);

        await Assert.ThrowsAsync<ConflictException>(() =>
            record.ExecuteAsync(seed.Tenant, nonClaim.Id, "screenshot", null, null, "AC-1"));
    }

    private static async Task<(Fakes Fakes, Seed Seed)> SeedAsync()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var actor = Principal.Create(tenant.Id, "a@alphacorp.com", PrincipalType.Human);
        await fakes.Principals.AddAsync(actor);

        var project = Project.Create(tenant.Id, "Shop API", "shop-api", null, LifecycleProfileId.New(), actor.Id);
        await fakes.Projects.AddAsync(project);

        var claim = Activity.Create(
            tenant.Id, project.Id, ActivityType.Claim, actor.Id, "blueprint meets AC-1", ActivityStatus.Completed,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null,
            Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>());
        await fakes.Activities.AddAsync(claim);

        return (fakes, new Seed(tenant.Id, project.Id, actor.Id, claim.Id));
    }

    private static async Task<Principal> SeedPrincipalAsync(Fakes fakes, TenantId tenantId, string email)
    {
        var principal = Principal.Create(tenantId, email, PrincipalType.Human);
        await fakes.Principals.AddAsync(principal);
        return principal;
    }

    private static Fakes CreateFakes() => new();

    private sealed record Seed(TenantId Tenant, ProjectId Project, PrincipalId Actor, ActivityId ClaimId);

    private sealed class Fakes
    {
        public InMemoryEvidenceRepository Evidence { get; } = new();
        public InMemoryActivityRepository Activities { get; } = new();
        public InMemoryArtifactRepository Artifacts { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryEvidenceRepository : IEvidenceRepository
    {
        public List<EvidenceRecord> Items { get; } = new();

        public Task AddAsync(EvidenceRecord evidence, CancellationToken ct = default)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }

        public Task<EvidenceRecord?> GetByIdAsync(TenantId tenantId, EvidenceId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(e => e.TenantId == tenantId && e.Id == id));

        public Task<IReadOnlyList<EvidenceRecord>> ListForClaimAsync(TenantId tenantId, ActivityId originActivityId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EvidenceRecord>>(
                Items.Where(e => e.TenantId == tenantId && e.OriginActivityId == originActivityId)
                    .OrderBy(e => e.RecordedAt)
                    .ToList());

        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EvidenceRecord>>(
                Items.Where(e => e.TenantId == tenantId && e.ProjectId == projectId)
                    .OrderBy(e => e.RecordedAt)
                    .ToList());
    }

    private sealed class InMemoryActivityRepository : IActivityRepository
    {
        public List<Activity> Items { get; } = new();

        public Task AddAsync(Activity activity, CancellationToken ct = default)
        {
            Items.Add(activity);
            return Task.CompletedTask;
        }

        public Task<Activity?> GetByIdAsync(TenantId tenantId, ActivityId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<IReadOnlyList<Activity>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Activity>> ListForItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && (a.Inputs.Any(i => i.ItemId == itemId) || a.Outputs.Any(o => o.ItemId == itemId))).ToList());

        public Task<IReadOnlyList<Activity>> ListByActorAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && a.Actor == actorId).ToList());

        public Task<Activity?> FindByCorrelationIdAsync(TenantId tenantId, ProjectId projectId, PrincipalId actorId, string correlationId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a =>
                a.TenantId == tenantId && a.ProjectId == projectId && a.Actor == actorId && a.CorrelationId == correlationId));
    }

    private sealed class InMemoryArtifactRepository : IArtifactRepository
    {
        public List<Artifact> Items { get; } = new();

        public Task AddAsync(Artifact artifact, CancellationToken ct = default)
        {
            Items.Add(artifact);
            return Task.CompletedTask;
        }

        public Task<Artifact?> GetByIdAsync(TenantId tenantId, ArtifactId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<IReadOnlyList<Artifact>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Artifact>>(
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Artifact>> ListVersionsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Artifact>>(
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId && a.Key == key).ToList());
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

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}