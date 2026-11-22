using DevWeave.Application.Abstractions;
using DevWeave.Application.Planning;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Tests.Planning;

public sealed class ImpactAnalysisServiceTests
{
    [Fact]
    public async Task Analyze_marks_validates_neighbors_as_reverification_required()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Impact Corp");
        var actor = Principal.Create(tenant.Id, "owner@impact.test", PrincipalType.Human);
        await fakes.Principals.AddAsync(actor);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);
        await fakes.Projects.AddAsync(project);

        var requirement = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1", "FR", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var test = KnowledgeItem.Create(
            tenant.Id, project.Id, "tc-1", "Test", Classification.Internal,
            RepresentationKind.Text, "verification.test-case", "1.0", actor.Id);
        var previous = requirement.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        var next = requirement.AddRevision("1.0", "{\"v\":2}", actor.Id, "tighten", null, previous.Id, 1);
        var testRevision = test.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        await fakes.Items.AddAsync(requirement);
        await fakes.Items.AddAsync(test);
        await fakes.Revisions.AddAsync(previous);
        await fakes.Revisions.AddAsync(next);
        await fakes.Revisions.AddAsync(testRevision);

        await fakes.Relationships.AddAsync(Relationship.Create(
            tenant.Id, project.Id, test.Id, requirement.Id, RelationTypeCodes.Validates, actor.Id));

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
                    "p95 lowered",
                    DateTimeOffset.UtcNow,
                    "chg-1"),
            ]);

        var report = await new ImpactAnalysisService(fakes.Revisions, fakes.Items, fakes.Relationships)
            .AnalyzeAsync(changeSet, actor.Id);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(test.Id.Value, finding.ItemId);
        Assert.Equal(ImpactDisposition.ReverificationRequired, finding.Disposition);
        Assert.Contains("validates", finding.RelationPath, StringComparison.Ordinal);
        Assert.Equal(ImpactAnalysisReport.CurrentRulesVersion, report.RulesVersion);
    }

    [Fact]
    public async Task Analyze_marks_withdrawn_neighbors_as_orphaned()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Impact Corp");
        var actor = Principal.Create(tenant.Id, "owner@impact.test", PrincipalType.Human);
        await fakes.Principals.AddAsync(actor);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);
        await fakes.Projects.AddAsync(project);

        var requirement = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1", "FR", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var decision = KnowledgeItem.Create(
            tenant.Id, project.Id, "adr-1", "ADR", Classification.Internal,
            RepresentationKind.Text, "architecture.decision", "1.0", actor.Id);
        var previous = requirement.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        var decisionRevision = decision.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        await fakes.Items.AddAsync(requirement);
        await fakes.Items.AddAsync(decision);
        await fakes.Revisions.AddAsync(previous);
        await fakes.Revisions.AddAsync(decisionRevision);

        await fakes.Relationships.AddAsync(Relationship.Create(
            tenant.Id, project.Id, decision.Id, requirement.Id, RelationTypeCodes.Satisfies, actor.Id));

        var changeSet = RequirementChangeSet.Create(
            tenant.Id,
            project.Id,
            actor.Id,
            [
                new RequirementChangeDescriptor(
                    RequirementChangeKind.Withdrawn,
                    [previous.Id],
                    Array.Empty<KnowledgeRevisionId>(),
                    actor.Id,
                    "no longer needed",
                    DateTimeOffset.UtcNow,
                    "wd-1"),
            ]);

        var report = await new ImpactAnalysisService(fakes.Revisions, fakes.Items, fakes.Relationships)
            .AnalyzeAsync(changeSet, actor.Id);

        Assert.Equal(ImpactDisposition.Orphaned, Assert.Single(report.Findings).Disposition);
    }

    [Fact]
    public async Task Analyze_split_reviews_historical_implements_links_without_copying_them()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Impact Corp");
        var actor = Principal.Create(tenant.Id, "owner@impact.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);

        var requirement = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1", "FR", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var implementation = KnowledgeItem.Create(
            tenant.Id, project.Id, "impl-1", "Impl", Classification.Internal,
            RepresentationKind.Text, "implementation.component", "1.0", actor.Id);
        var previous = requirement.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        var left = requirement.AddRevision("1.0", "{\"v\":2a}", actor.Id, "split-a", null, previous.Id, 1);
        var right = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1b", "FR b", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var rightRevision = right.AddRevision("1.0", "{\"v\":2b}", actor.Id, "split-b", null, null, 0);
        var implRevision = implementation.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed", null, null, 0);
        await fakes.Items.AddAsync(requirement);
        await fakes.Items.AddAsync(implementation);
        await fakes.Items.AddAsync(right);
        await fakes.Revisions.AddAsync(previous);
        await fakes.Revisions.AddAsync(left);
        await fakes.Revisions.AddAsync(rightRevision);
        await fakes.Revisions.AddAsync(implRevision);

        var historical = Relationship.Create(
            tenant.Id, project.Id, implementation.Id, requirement.Id, RelationTypeCodes.Implements, actor.Id);
        await fakes.Relationships.AddAsync(historical);

        var changeSet = RequirementChangeSet.Create(
            tenant.Id,
            project.Id,
            actor.Id,
            [
                new RequirementChangeDescriptor(
                    RequirementChangeKind.Split,
                    [previous.Id],
                    [left.Id, rightRevision.Id],
                    actor.Id,
                    "split cart and checkout",
                    DateTimeOffset.UtcNow,
                    "split-1"),
            ]);

        var report = await new ImpactAnalysisService(fakes.Revisions, fakes.Items, fakes.Relationships)
            .AnalyzeAsync(changeSet, actor.Id);

        var finding = Assert.Single(report.Findings);
        Assert.Equal(implementation.Id.Value, finding.ItemId);
        Assert.Equal(ImpactDisposition.ReviewRequired, finding.Disposition);
        Assert.Equal(RelationshipStatus.Active, historical.Status);
        Assert.Equal(requirement.Id, historical.TargetItemId);
    }

    [Fact]
    public async Task Analyze_includes_open_work_packages_whose_scope_pins_changed_revisions()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Impact Corp");
        var actor = Principal.Create(tenant.Id, "owner@impact.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);

        var requirement = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1", "FR", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var previousA = requirement.AddRevision("1.0", "{\"v\":1}", actor.Id, "seed-a", null, null, 0);
        var previousB = requirement.AddRevision("1.0", "{\"v\":1b}", actor.Id, "seed-b", null, previousA.Id, 1);
        var merged = requirement.AddRevision("1.0", "{\"v\":2}", actor.Id, "merge", null, previousB.Id, 2);
        await fakes.Items.AddAsync(requirement);
        await fakes.Revisions.AddAsync(previousA);
        await fakes.Revisions.AddAsync(previousB);
        await fakes.Revisions.AddAsync(merged);

        var campaign = ExecutionCampaign.Create(tenant.Id, project.Id, Guid.NewGuid(), "ship", "Mediated");
        var affected = WorkPackage.Create(
            tenant.Id, project.Id, campaign.Id, campaign.BaselineId, "wp-a", "implement A",
            scopeRevisionIds: [previousA.Id.Value]);
        var outside = WorkPackage.Create(
            tenant.Id, project.Id, campaign.Id, campaign.BaselineId, "wp-b", "unrelated",
            scopeRevisionIds: [Guid.NewGuid()]);
        fakes.WorkPackages.Items.Add(affected);
        fakes.WorkPackages.Items.Add(outside);

        var changeSet = RequirementChangeSet.Create(
            tenant.Id,
            project.Id,
            actor.Id,
            [
                new RequirementChangeDescriptor(
                    RequirementChangeKind.Merged,
                    [previousA.Id, previousB.Id],
                    [merged.Id],
                    actor.Id,
                    "merge checkout flows",
                    DateTimeOffset.UtcNow,
                    "merge-1"),
            ]);

        var report = await new ImpactAnalysisService(
                fakes.Revisions, fakes.Items, fakes.Relationships, fakes.WorkPackages)
            .AnalyzeAsync(changeSet, actor.Id);

        var packageFinding = Assert.Single(report.Findings, finding => finding.ResourceKind == "work.package");
        Assert.Equal(affected.Id.Value, packageFinding.ItemId);
        Assert.Equal(ImpactDisposition.ReviewRequired, packageFinding.Disposition);
        Assert.DoesNotContain(report.Findings, finding => finding.ItemId == outside.Id.Value);
    }

    private sealed class Fakes
    {
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryKnowledgeRevisionRepository Revisions { get; } = new();
        public InMemoryRelationshipRepository Relationships { get; } = new();
        public InMemoryWorkPackageImpactSource WorkPackages { get; } = new();
    }

    private sealed class InMemoryWorkPackageImpactSource : IWorkPackageImpactSource
    {
        public List<WorkPackage> Items { get; } = new();

        public Task<List<WorkPackage>> ListWorkPackagesByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult(Items.Where(package => package.TenantId == tenantId && package.ProjectId == projectId).ToList());
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

        public Task<IReadOnlyList<Principal>> ListByTenantAndTypeAsync(TenantId tenantId, PrincipalType type, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Principal>>(Principals.Where(p => p.TenantId == tenantId && p.Type == type).ToList());
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
        public List<Relationship> Items { get; } = new();
        public Task AddAsync(Relationship relationship, CancellationToken ct = default)
        {
            Items.Add(relationship);
            return Task.CompletedTask;
        }

        public Task<Relationship?> GetByIdAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

        public Task<IReadOnlyList<Relationship>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(Items.Where(r => r.TenantId == tenantId && r.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Relationship>> ListOutgoingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(Items.Where(r => r.TenantId == tenantId && r.SourceItemId == itemId).ToList());

        public Task<IReadOnlyList<Relationship>> ListIncomingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(Items.Where(r => r.TenantId == tenantId && r.TargetItemId == itemId).ToList());
    }
}
