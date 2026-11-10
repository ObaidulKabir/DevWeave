using System.Reflection;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Activities;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Activities;

/// <summary>
/// Exercises the Activity use cases against in-memory fakes, focusing on flow correctness, the
/// cross-tenant deny-by-default invariant (PRO-001), revision-pinned inputs/outputs (PRO-003/PRO-004),
/// revision-based reconstruction (REV-007) and the PROV-O projection shape.
/// </summary>
public sealed class ActivityUseCaseTests
{
    [Fact]
    public async Task PRO_001_record_activity_commits_an_append_only_record()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");

        var input = new ActivityInput(KnowledgeItemId.New(), KnowledgeRevisionId.New(), 0);
        var output = new ActivityOutput(KnowledgeItemId.New(), KnowledgeRevisionId.New(), 0);

        var activity = await new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(
                tenant.Id, project.Id, ActivityType.Generated, actor.Id, "generate summary",
                ActivityStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "run-1", null, null,
                new[] { input }, new[] { output });

        Assert.Equal(tenant.Id, activity.TenantId);
        Assert.Equal(project.Id, activity.ProjectId);
        Assert.Equal(actor.Id, activity.Actor);
        Assert.Equal(ActivityType.Generated, activity.Type);
        Assert.Single(activity.Inputs);
        Assert.Single(activity.Outputs);
        Assert.Single(fakes.Activities.Items);
    }

    [Fact]
    public async Task record_activity_rejects_a_cross_tenant_actor_and_project()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var projectB = await SeedProjectAsync(fakes, tenantB.Id, "shop-api");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");

        var record = new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            record.ExecuteAsync(
                tenantA.Id, projectB.Id, ActivityType.Generated, PrincipalId.New(), "p",
                ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, null, null,
                Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>()));

        var projectA = await SeedProjectAsync(fakes, tenantA.Id, "shop-api-a");
        await Assert.ThrowsAsync<NotFoundException>(() =>
            record.ExecuteAsync(
                tenantA.Id, projectA.Id, ActivityType.Generated, actorB.Id, "p",
                ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, null, null,
                Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>()));
    }

    [Fact]
    public async Task REV_008_record_amendment_without_reason_or_reference_conflicts()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");

        foreach (var amendment in new[] { ActivityType.Correction, ActivityType.Retraction, ActivityType.Supersession })
        {
            await Assert.ThrowsAsync<ConflictException>(() => new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
                .ExecuteAsync(
                    tenant.Id, project.Id, amendment, actor.Id, "p",
                    ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, null, null,
                    Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>()));

            await Assert.ThrowsAsync<ConflictException>(() => new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
                .ExecuteAsync(
                    tenant.Id, project.Id, amendment, actor.Id, "p",
                    ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, "reason", null,
                    Array.Empty<ActivityInput>(), Array.Empty<ActivityOutput>()));
        }
    }

    [Fact]
    public async Task read_paths_never_disclose_cross_tenant_activities()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var projectB = await SeedProjectAsync(fakes, tenantB.Id, "shop-api");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");
        var itemB = KnowledgeItemId.New();

        var activityB = await new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(
                tenantB.Id, projectB.Id, ActivityType.Generated, actorB.Id, "p",
                ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, null, null,
                new[] { new ActivityInput(itemB, KnowledgeRevisionId.New(), 0) }, Array.Empty<ActivityOutput>());

        Assert.Null(await new GetActivity(fakes.Activities).ExecuteAsync(tenantA.Id, activityB.Id));
        Assert.Empty(await new ListActivitiesForProject(fakes.Activities).ExecuteAsync(tenantA.Id, projectB.Id));
        Assert.Empty(await new ListActivitiesForItem(fakes.Activities).ExecuteAsync(tenantA.Id, itemB));
        Assert.Empty(await new ListActivitiesByActor(fakes.Activities).ExecuteAsync(tenantA.Id, actorB.Id));

        Assert.Single(await new ListActivitiesByActor(fakes.Activities).ExecuteAsync(tenantB.Id, actorB.Id));
    }

    [Fact]
    public async Task outputs_are_linked_to_the_producing_activity_and_actor()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = KnowledgeItemId.New();
        var revision = KnowledgeRevisionId.New();

        var activity = await new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(
                tenant.Id, project.Id, ActivityType.Generated, actor.Id, "generate",
                ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, null, null,
                Array.Empty<ActivityInput>(), new[] { new ActivityOutput(item, revision, 0) });

        Assert.Equal(actor.Id, activity.Actor);
        Assert.Equal(revision, Assert.Single(activity.Outputs).RevisionId);
        Assert.Equal(item, Assert.Single(activity.Outputs).ItemId);
    }

    [Fact]
    public async Task REV_007_reconstruct_returns_latest_revision_when_no_timestamp_is_given()
    {
        var fakes = CreateFakes();
        var seed = await SeedItemWithRevisionsAsync(fakes);

        var result = await new ReconstructProjectState(fakes.Activities, fakes.Items, fakes.Revisions)
            .ExecuteAsync(seed.Tenant, seed.Project);

        var snapshot = Assert.Single(result.Items);
        Assert.Equal(3, snapshot.RevisionNumber);
        Assert.NotNull(snapshot.RevisionId);
    }

    [Fact]
    public async Task REV_007_reconstruct_rolls_back_to_the_greatest_revision_on_or_before_a_timestamp()
    {
        var fakes = CreateFakes();
        var seed = await SeedItemWithRevisionsAsync(fakes, spread: true);

        var asOf = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);

        var result = await new ReconstructProjectState(fakes.Activities, fakes.Items, fakes.Revisions)
            .ExecuteAsync(seed.Tenant, seed.Project, asOf);

        var snapshot = Assert.Single(result.Items);
        Assert.Equal(2, snapshot.RevisionNumber);

        // A timestamp before any revision yields an empty snapshot.
        var before = await new ReconstructProjectState(fakes.Activities, fakes.Items, fakes.Revisions)
            .ExecuteAsync(seed.Tenant, seed.Project, new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal(0, Assert.Single(before.Items).RevisionNumber);
        Assert.Null(Assert.Single(before.Items).RevisionId);
    }

    [Fact]
    public async Task PROV_O_export_projects_activity_agent_used_and_generated()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var inputRevision = KnowledgeRevisionId.New();
        var outputRevision = KnowledgeRevisionId.New();

        await new RecordActivity(fakes.Activities, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(
                tenant.Id, project.Id, ActivityType.Generated, actor.Id, "generate",
                ActivityStatus.Completed, DateTimeOffset.UtcNow, null, null, null, null,
                new[] { new ActivityInput(KnowledgeItemId.New(), inputRevision, 0) },
                new[] { new ActivityOutput(KnowledgeItemId.New(), outputRevision, 0) });

        var exported = await new ExportProvenance(fakes.Activities).ExecuteAsync(tenant.Id, project.Id);

        var record = Assert.Single(exported);
        Assert.Equal("Generated", record.ActivityType);
        Assert.Equal(actor.Id.Value, record.Agent);
        Assert.Equal(inputRevision.Value, Assert.Single(record.Used).RevisionId);
        Assert.Equal(outputRevision.Value, Assert.Single(record.Generated).RevisionId);
    }

    private static Fakes CreateFakes() => new();

    private static async Task<Project> SeedProjectAsync(Fakes fakes, TenantId tenantId, string key)
    {
        var project = Project.Create(tenantId, "Project", key, null, LifecycleProfileId.New(), PrincipalId.New());
        await fakes.Projects.AddAsync(project);
        return project;
    }

    private static async Task<Principal> SeedPrincipalAsync(Fakes fakes, TenantId tenantId, string email)
    {
        var principal = Principal.Create(tenantId, email, PrincipalType.Human);
        await fakes.Principals.AddAsync(principal);
        return principal;
    }

    private static async Task<(TenantId Tenant, ProjectId Project)> SeedItemWithRevisionsAsync(Fakes fakes, bool spread = false)
    {
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = KnowledgeItem.Create(
            tenant.Id, project.Id, "api", "API", Classification.Internal, RepresentationKind.Text, "type", "1.0", actor.Id);
        await fakes.Items.AddAsync(item);

        var recordedAt = spread
            ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : DateTimeOffset.UtcNow;

        for (var n = 1; n <= 3; n++)
        {
            await fakes.Revisions.AddAsync(
                CreateRevision(tenant.Id, item.Id, n, actor.Id, recordedAt.AddDays(n - 1)));
        }

        return (tenant.Id, project.Id);
    }

    private static KnowledgeRevision CreateRevision(
        TenantId tenant,
        KnowledgeItemId item,
        int number,
        PrincipalId actor,
        DateTimeOffset recordedAt)
    {
        var revision = KnowledgeRevision.Create(tenant, item, number, "1.0", "{\"n\":" + number + "}", actor, "reason", null, null);

        // Override the factory-assigned UtcNow so reconstruction tests can be deterministic.
        var field = typeof(KnowledgeRevision)
            .GetField("<RecordedAt>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(revision, recordedAt);

        return revision;
    }

    private sealed class Fakes
    {
        public InMemoryActivityRepository Activities { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryKnowledgeRevisionRepository Revisions { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
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
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && a.Actor == actorId).ToList());

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
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}