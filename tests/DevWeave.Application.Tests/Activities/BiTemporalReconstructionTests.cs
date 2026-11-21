using System.Reflection;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Activities;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Tests.Activities;

/// <summary>
/// Phase 4 gate fixtures: T0/T1/T2 recorded-time reconstruction across revision, relationship
/// revocation, and evidence events (REV-007, NFR-035, REL-001, PRO-006).
/// </summary>
public sealed class BiTemporalReconstructionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 3, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Phase4_T0_T1_T2_fixtures_reproduce_revision_revoke_and_evidence_as_of_recorded_time()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Chronos Corp");
        var actor = Principal.Create(tenant.Id, "owner@chronos.test", PrincipalType.Human);
        await fakes.Principals.AddAsync(actor);

        var project = Project.Create(tenant.Id, "Timeline", "timeline", null, LifecycleProfileId.New(), actor.Id);
        await fakes.Projects.AddAsync(project);

        var item = KnowledgeItem.Create(
            tenant.Id, project.Id, "fr-1", "Checkout", Classification.Internal,
            RepresentationKind.Text, "requirements.functional", "1.0", actor.Id);
        var design = KnowledgeItem.Create(
            tenant.Id, project.Id, "adr-1", "Checkout ADR", Classification.Internal,
            RepresentationKind.Text, "architecture.decision", "1.0", actor.Id);
        await fakes.Items.AddAsync(item);
        await fakes.Items.AddAsync(design);

        // T0: first requirement revision + active satisfies edge.
        var rev1 = item.AddRevision("1.0", "{\"v\":1}", actor.Id, "initial", null, null, 0);
        SetInstant(rev1, "<RecordedAt>k__BackingField", T0);
        await fakes.Revisions.AddAsync(rev1);

        var edge = Relationship.Create(
            tenant.Id, project.Id, design.Id, item.Id, "satisfies", actor.Id, rationale: "ADR covers FR");
        SetInstant(edge, "<CreatedAt>k__BackingField", T0);
        await fakes.Relationships.AddAsync(edge);

        var claim = Activity.Create(
            tenant.Id, project.Id, ActivityType.Claim, actor.Id, "meets AC",
            ActivityStatus.Completed, T0, T0, null, null, null,
            Array.Empty<ActivityInput>(),
            new[] { new ActivityOutput(item.Id, rev1.Id, 0) });
        await fakes.Activities.AddAsync(claim);

        // T1: requirement revised; evidence recorded against the claim (still pins rev1).
        var rev2 = item.AddRevision("1.0", "{\"v\":2}", actor.Id, "clarify", null, rev1.Id, 1);
        SetInstant(rev2, "<RecordedAt>k__BackingField", T1);
        await fakes.Revisions.AddAsync(rev2);

        var evidence = EvidenceRecord.Create(
            tenant.Id, project.Id, claim.Id, "screenshot", "sha256:aa", null, item.Id, rev1.Id, "AC-1");
        SetInstant(evidence, "<RecordedAt>k__BackingField", T1);
        evidence.Review(EvidenceReviewState.Accepted, actor.Id);
        await fakes.Evidence.AddAsync(evidence);

        // T2: relationship revoked after the evidence event.
        edge.Revoke();
        SetInstant(edge, "<RevokedAt>k__BackingField", T2);

        var reconstruct = new ReconstructProjectState(
            fakes.Activities, fakes.Items, fakes.Revisions, fakes.Relationships, fakes.Evidence);

        var atT0 = await reconstruct.ExecuteAsync(tenant.Id, project.Id, T0);
        Assert.Equal(1, Assert.Single(atT0.Items, s => s.ItemId == item.Id).RevisionNumber);
        Assert.Equal(rev1.Id, Assert.Single(atT0.Items, s => s.ItemId == item.Id).RevisionId);
        Assert.Equal(edge.Id, Assert.Single(atT0.Relationships).Id);
        Assert.Empty(atT0.Evidence);
        Assert.Equal(claim.Id, Assert.Single(atT0.Activities).Id);

        var atT1 = await reconstruct.ExecuteAsync(tenant.Id, project.Id, T1);
        Assert.Equal(2, Assert.Single(atT1.Items, s => s.ItemId == item.Id).RevisionNumber);
        Assert.Equal(rev2.Id, Assert.Single(atT1.Items, s => s.ItemId == item.Id).RevisionId);
        Assert.Equal(edge.Id, Assert.Single(atT1.Relationships).Id);
        Assert.Equal(evidence.Id, Assert.Single(atT1.Evidence).Id);

        var atT2 = await reconstruct.ExecuteAsync(tenant.Id, project.Id, T2);
        Assert.Equal(2, Assert.Single(atT2.Items, s => s.ItemId == item.Id).RevisionNumber);
        Assert.Empty(atT2.Relationships);
        Assert.Equal(evidence.Id, Assert.Single(atT2.Evidence).Id);

        // Between T1 and T2 the edge remains visible even though it is revoked "now".
        var between = await reconstruct.ExecuteAsync(
            tenant.Id, project.Id, T1.AddHours(1));
        Assert.Equal(edge.Id, Assert.Single(between.Relationships).Id);
    }

    private static void SetInstant(object target, string backingField, DateTimeOffset value)
    {
        var field = target.GetType().GetField(backingField, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Missing backing field '{backingField}' on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private sealed class Fakes
    {
        public InMemoryActivityRepository Activities { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryKnowledgeRevisionRepository Revisions { get; } = new();
        public InMemoryRelationshipRepository Relationships { get; } = new();
        public InMemoryEvidenceRepository Evidence { get; } = new();
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
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId).OrderBy(a => a.StartedAt).ToList());

        public Task<IReadOnlyList<Activity>> ListForItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && (a.Inputs.Any(i => i.ItemId == itemId) || a.Outputs.Any(o => o.ItemId == itemId))).ToList());

        public Task<IReadOnlyList<Activity>> ListByActorAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(Items.Where(a => a.TenantId == tenantId && a.Actor == actorId).ToList());

        public Task<Activity?> FindByCorrelationIdAsync(TenantId tenantId, ProjectId projectId, PrincipalId actorId, string correlationId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a =>
                a.TenantId == tenantId && a.ProjectId == projectId && a.Actor == actorId && a.CorrelationId == correlationId));
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
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(
                Items.Where(i => i.TenantId == tenantId && i.ProjectId == projectId).ToList());

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
                Revisions.Where(r => r.TenantId == tenantId && r.KnowledgeItemId == itemId)
                    .OrderBy(r => r.RevisionNumber)
                    .ToList());

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
            Task.FromResult<IReadOnlyList<Relationship>>(
                Items.Where(r => r.TenantId == tenantId && r.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Relationship>> ListOutgoingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(
                Items.Where(r => r.TenantId == tenantId && r.SourceItemId == itemId).ToList());

        public Task<IReadOnlyList<Relationship>> ListIncomingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(
                Items.Where(r => r.TenantId == tenantId && r.TargetItemId == itemId).ToList());
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
                Items.Where(e => e.TenantId == tenantId && e.OriginActivityId == originActivityId).ToList());

        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EvidenceRecord>>(
                Items.Where(e => e.TenantId == tenantId && e.ProjectId == projectId).ToList());
    }
}
