using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Planning;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Tests.Planning;

public sealed class ContextInvalidationTests
{
    [Fact]
    public async Task Analyze_marks_context_packages_pinning_changed_revisions_stale()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Impact Corp");
        var actor = Principal.Create(tenant.Id, "owner@impact.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);
        fakes.Memberships.GrantOwner(tenant.Id, project.Id, actor.Id);

        var requirement = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1", "FR", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var previous = requirement.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        var next = requirement.AddRevision("1.0", "{\"v\":2}", actor.Id, "tighten", null, previous.Id, 1);
        await fakes.Items.AddAsync(requirement);
        await fakes.Revisions.AddAsync(previous);
        await fakes.Revisions.AddAsync(next);

        var context = ContextPackage.Assemble(
            tenant.Id,
            project.Id,
            "wp-1",
            actor.Id,
            "ctx-1",
            [new ContextPackageEntry(requirement.Id, previous.Id, 0, RepresentationKind.Text)]);
        fakes.Contexts.Items.Add(context);

        var changeSet = RequirementChangeSet.Create(
            tenant.Id,
            project.Id,
            actor.Id,
            [
                new RequirementChangeDescriptor(
                    RequirementChangeKind.ThresholdTightened,
                    [previous.Id],
                    [next.Id],
                    actor.Id,
                    "tighten p95",
                    DateTimeOffset.UtcNow,
                    "chg-1"),
            ]);
        fakes.ChangeSets.Items.Add(changeSet);

        var report = await new AnalyzeRequirementImpact(
                fakes.ChangeSets,
                fakes.Reports,
                new ImpactAnalysisService(fakes.Revisions, fakes.Items, fakes.Relationships),
                fakes.Contexts,
                fakes.Outbox,
                fakes.Memberships,
                fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, changeSet.Id, actor.Id);

        Assert.True(context.IsStale);
        Assert.Contains(report.Id.Value.ToString(), context.StaleReason);
        Assert.Contains(fakes.Outbox.Events, e => e.EventType == "requirement.impact.analyzed");
    }

    private sealed class Fakes
    {
        public InMemoryRequirementChangeSetRepository ChangeSets { get; } = new();
        public InMemoryImpactAnalysisReportRepository Reports { get; } = new();
        public InMemoryContextPackageRepository Contexts { get; } = new();
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryKnowledgeRevisionRepository Revisions { get; } = new();
        public InMemoryRelationshipRepository Relationships { get; } = new();
        public RecordingOutbox Outbox { get; } = new();
        public OwnerMembershipRepository Memberships { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class RecordingOutbox : ITransactionalOutbox
    {
        public List<(string EventType, string Payload)> Events { get; } = new();

        public Task EnqueueAsync(
            AuthorizedScope scope,
            string eventType,
            string schemaVersion,
            string allowlistedPayloadJson,
            CancellationToken ct = default)
        {
            Events.Add((eventType, allowlistedPayloadJson));
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryRequirementChangeSetRepository : IRequirementChangeSetRepository
    {
        public List<RequirementChangeSet> Items { get; } = new();

        public Task AddAsync(RequirementChangeSet changeSet, CancellationToken ct = default)
        {
            Items.Add(changeSet);
            return Task.CompletedTask;
        }

        public Task<RequirementChangeSet?> GetByIdAsync(
            TenantId tenantId,
            RequirementChangeSetId id,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(set => set.TenantId == tenantId && set.Id == id));
    }

    private sealed class InMemoryImpactAnalysisReportRepository : IImpactAnalysisReportRepository
    {
        public List<ImpactAnalysisReport> Items { get; } = new();

        public Task AddAsync(ImpactAnalysisReport report, CancellationToken ct = default)
        {
            Items.Add(report);
            return Task.CompletedTask;
        }

        public Task<ImpactAnalysisReport?> GetByIdAsync(
            TenantId tenantId,
            ImpactAnalysisReportId id,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(report => report.TenantId == tenantId && report.Id == id));

        public Task<IReadOnlyList<ImpactAnalysisReport>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ImpactAnalysisReport>>(
                Items.Where(report => report.TenantId == tenantId && report.ProjectId == projectId).ToList());
    }

    private sealed class InMemoryContextPackageRepository : IContextPackageRepository
    {
        public List<ContextPackage> Items { get; } = new();

        public Task AddAsync(ContextPackage package, CancellationToken ct = default)
        {
            Items.Add(package);
            return Task.CompletedTask;
        }

        public Task<ContextPackage?> GetByIdAsync(TenantId tenantId, ContextPackageId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(package => package.TenantId == tenantId && package.Id == id));

        public Task<ContextPackage?> FindByIdempotencyKeyAsync(
            TenantId tenantId,
            ProjectId projectId,
            PrincipalId agentId,
            string idempotencyKey,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(package =>
                package.TenantId == tenantId
                && package.ProjectId == projectId
                && package.AgentId == agentId
                && package.IdempotencyKey == idempotencyKey
                && !package.IsStale));

        public Task<IReadOnlyList<ContextPackage>> ListCurrentByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ContextPackage>>(
                Items.Where(package => package.TenantId == tenantId && package.ProjectId == projectId && !package.IsStale)
                    .ToList());

        public Task<ContextPackage?> GetLatestCurrentForTaskAsync(
            TenantId tenantId,
            ProjectId projectId,
            string taskKey,
            CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(package => package.TenantId == tenantId
                    && package.ProjectId == projectId
                    && package.TaskKey == taskKey
                    && !package.IsStale)
                .OrderByDescending(package => package.CreatedAt)
                .FirstOrDefault());

        public Task<ContextPackage?> GetLatestForTaskAsync(
            TenantId tenantId,
            ProjectId projectId,
            string taskKey,
            CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(package => package.TenantId == tenantId
                    && package.ProjectId == projectId
                    && package.TaskKey == taskKey)
                .OrderByDescending(package => package.CreatedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<ContextPackage>> ListCurrentPinningRevisionsAsync(
            TenantId tenantId,
            ProjectId projectId,
            IReadOnlyCollection<KnowledgeRevisionId> revisionIds,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ContextPackage>>(
                Items.Where(package =>
                        package.TenantId == tenantId
                        && package.ProjectId == projectId
                        && !package.IsStale
                        && revisionIds.Any(package.PinsRevision))
                    .ToList());
    }

    private sealed class OwnerMembershipRepository : IProjectMembershipRepository
    {
        private readonly List<ProjectMembership> _rows = new();

        public void GrantOwner(TenantId tenantId, ProjectId projectId, PrincipalId principalId) =>
            _rows.Add(ProjectMembership.Grant(tenantId, projectId, principalId, ProjectRole.Owner));

        public Task AddAsync(ProjectMembership membership, CancellationToken ct = default)
        {
            _rows.Add(membership);
            return Task.CompletedTask;
        }

        public Task<ProjectMembership?> GetByIdAsync(
            TenantId tenantId,
            ProjectMembershipId id,
            CancellationToken ct = default) =>
            Task.FromResult(_rows.FirstOrDefault(row => row.TenantId == tenantId && row.Id == id));

        public Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(
            TenantId tenantId,
            PrincipalId principalId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                _rows.Where(row => row.TenantId == tenantId && row.PrincipalId == principalId).ToList());

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                _rows.Where(row => row.TenantId == tenantId && row.ProjectId == projectId).ToList());
    }

    private sealed class InMemoryKnowledgeItemRepository : IKnowledgeItemRepository
    {
        public List<KnowledgeItem> Items { get; } = new();
        public Task AddAsync(KnowledgeItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<KnowledgeItem?> GetByIdAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.TenantId == tenantId && i.Id == id));

        public Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(Items.Where(i => i.TenantId == tenantId && i.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(Items.Where(i => i.TenantId == tenantId).ToList());

        public Task<bool> KeyExistsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(i => i.TenantId == tenantId && i.ProjectId == projectId && i.Key == key));
    }

    private sealed class InMemoryKnowledgeRevisionRepository : IKnowledgeRevisionRepository
    {
        public List<KnowledgeRevision> Revisions { get; } = new();
        public Task AddAsync(KnowledgeRevision revision, CancellationToken ct = default)
        {
            Revisions.Add(revision);
            return Task.CompletedTask;
        }

        public Task<KnowledgeRevision?> GetByIdAsync(TenantId tenantId, KnowledgeRevisionId id, CancellationToken ct = default) =>
            Task.FromResult(Revisions.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

        public Task<IReadOnlyList<KnowledgeRevision>> ListByItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRevision>>(
                Revisions.Where(r => r.TenantId == tenantId && r.KnowledgeItemId == itemId).ToList());

        public Task<IReadOnlyList<KnowledgeRevision>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRevision>>(Revisions.Where(r => r.TenantId == tenantId).ToList());
    }

    private sealed class InMemoryRelationshipRepository : IRelationshipRepository
    {
        public Task AddAsync(Relationship relationship, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Relationship?> GetByIdAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default) =>
            Task.FromResult<Relationship?>(null);
        public Task<IReadOnlyList<Relationship>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(Array.Empty<Relationship>());
        public Task<IReadOnlyList<Relationship>> ListOutgoingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(Array.Empty<Relationship>());
        public Task<IReadOnlyList<Relationship>> ListIncomingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(Array.Empty<Relationship>());
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
