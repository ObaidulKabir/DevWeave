using DevWeave.Application.Abstractions;
using DevWeave.Application.Relations;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Application.Tests.Relations;

/// <summary>
/// Exercises the relationship use cases against in-memory fakes, focused on the registry-rule
/// enforcement (BR-007, REL-002/SCH-007, REL-007), cardinality, and the pure bounded BFS traversal
/// (REL-004, REL-005), including the revoked-edge hiding rule.
/// </summary>
public sealed class RelationshipUseCaseTests
{
    private static readonly string[] ImplementsSource = ["design"];
    private static readonly string[] ImplementsTarget = ["implementation"];

    [Fact]
    public async Task BR_007_an_unknown_relation_type_is_rejected()
    {
        var fakes = CreateFakes();
        await SeedRelationTypeAsync(fakes.RelationTypes, "references", RelationCardinality.ManyToMany);
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var (source, target) = await SeedTwoItemsAsync(fakes, tenant, project);

        var useCase = CreateUseCase(fakes);

        var ex = await Assert.ThrowsAsync<RelationTypeMismatchException>(() =>
            useCase.ExecuteAsync(tenant, project, source, target, "does_not_exist", PrincipalId.New()));

        Assert.Equal("unknown-relation-type", ex.Code);
    }

    [Fact]
    public async Task BR_007_a_non_active_relation_type_is_rejected()
    {
        var fakes = CreateFakes();
        var relationType = await SeedRelationTypeAsync(fakes.RelationTypes, "references", RelationCardinality.ManyToMany);
        relationType.Deprecate();
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var (source, target) = await SeedTwoItemsAsync(fakes, tenant, project);

        var useCase = CreateUseCase(fakes);

        var ex = await Assert.ThrowsAsync<RelationTypeMismatchException>(() =>
            useCase.ExecuteAsync(tenant, project, source, target, "references", PrincipalId.New()));

        Assert.Equal("relation-type-not-active", ex.Code);
    }

    [Fact]
    public async Task REL_007_a_cross_tenant_target_is_rejected_as_not_found()
    {
        var fakes = CreateFakes();
        await SeedRelationTypeAsync(fakes.RelationTypes, "references", RelationCardinality.ManyToMany);
        var tenantA = TenantId.New();
        var tenantB = TenantId.New();
        var project = ProjectId.New();
        var source = await SeedItemAsync(fakes, tenantA, project, "doc");
        var target = await SeedItemAsync(fakes, tenantB, project, "doc");

        var useCase = CreateUseCase(fakes);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(tenantA, project, source, target, "references", PrincipalId.New()));
    }

    [Fact]
    public async Task REL_007_a_cross_project_edge_is_rejected()
    {
        var fakes = CreateFakes();
        await SeedRelationTypeAsync(fakes.RelationTypes, "references", RelationCardinality.ManyToMany);
        var tenant = TenantId.New();
        var projectA = ProjectId.New();
        var projectB = ProjectId.New();
        var source = await SeedItemAsync(fakes, tenant, projectA, "doc");
        var target = await SeedItemAsync(fakes, tenant, projectB, "doc");

        var useCase = CreateUseCase(fakes);

        await Assert.ThrowsAsync<CrossProjectReferenceException>(() =>
            useCase.ExecuteAsync(tenant, projectA, source, target, "references", PrincipalId.New()));
    }

    [Fact]
    public async Task REL_002_SCH_007_an_endpoint_type_mismatch_is_rejected()
    {
        var fakes = CreateFakes();
        await SeedRelationTypeAsync(
            fakes.RelationTypes,
            "implements",
            RelationCardinality.ManyToMany,
            ImplementsSource,
            ImplementsTarget);
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var source = await SeedItemAsync(fakes, tenant, project, "test");
        var target = await SeedItemAsync(fakes, tenant, project, "implementation");

        var useCase = CreateUseCase(fakes);

        var ex = await Assert.ThrowsAsync<RelationTypeMismatchException>(() =>
            useCase.ExecuteAsync(tenant, project, source, target, "implements", PrincipalId.New()));

        Assert.Equal("endpoint-type-not-allowed", ex.Code);
    }

    [Fact]
    public async Task SCH_007_one_to_one_cardinality_rejects_a_second_source_edge()
    {
        var fakes = CreateFakes();
        await SeedRelationTypeAsync(fakes.RelationTypes, "links", RelationCardinality.OneToOne);
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var a = await SeedItemAsync(fakes, tenant, project, "doc");
        var b = await SeedItemAsync(fakes, tenant, project, "doc");
        var c = await SeedItemAsync(fakes, tenant, project, "doc");

        var useCase = CreateUseCase(fakes);
        await useCase.ExecuteAsync(tenant, project, a, b, "links", PrincipalId.New());

        var ex = await Assert.ThrowsAsync<RelationTypeMismatchException>(() =>
            useCase.ExecuteAsync(tenant, project, a, c, "links", PrincipalId.New()));

        Assert.Equal("cardinality-violation", ex.Code);
    }

    [Fact]
    public async Task SCH_007_one_to_many_cardinality_rejects_a_reused_target()
    {
        var fakes = CreateFakes();
        await SeedRelationTypeAsync(fakes.RelationTypes, "contains", RelationCardinality.OneToMany);
        var tenant = TenantId.New();
        var project = ProjectId.New();
        var parent = await SeedItemAsync(fakes, tenant, project, "doc");
        var other = await SeedItemAsync(fakes, tenant, project, "doc");
        var child = await SeedItemAsync(fakes, tenant, project, "doc");

        var useCase = CreateUseCase(fakes);
        await useCase.ExecuteAsync(tenant, project, parent, child, "contains", PrincipalId.New());

        // A different source targeting the same child is rejected because the target can only be used once.
        var ex = await Assert.ThrowsAsync<RelationTypeMismatchException>(() =>
            useCase.ExecuteAsync(tenant, project, other, child, "contains", PrincipalId.New()));

        Assert.Equal("cardinality-violation", ex.Code);
    }

    [Fact]
    public async Task REL_004_REL_005_traversal_returns_ordered_explainable_paths_bounded_by_depth()
    {
        var fakes = CreateFakes();
        var tenant = TenantId.New();
        var project = ProjectId.New();

        var r = await SeedItemAsync(fakes, tenant, project, "doc");
        var d = await SeedItemAsync(fakes, tenant, project, "doc");
        var i = await SeedItemAsync(fakes, tenant, project, "doc");
        var t = await SeedItemAsync(fakes, tenant, project, "doc");

        await AddEdgeAsync(fakes, tenant, r, d);
        await AddEdgeAsync(fakes, tenant, d, i);
        await AddEdgeAsync(fakes, tenant, d, t);
        await AddEdgeAsync(fakes, tenant, i, t);

        var traverse = new TraverseRelationships(fakes.Relationships, fakes.Items);

        var depthOne = await traverse.ExecuteAsync(tenant, r, TraversalDirection.Outgoing, 1, 100);
        Assert.Single(depthOne.Paths);
        Assert.Equal(d, depthOne.Paths[0].EndItemId);

        var depthTwo = await traverse.ExecuteAsync(tenant, r, TraversalDirection.Outgoing, 2, 100);
        Assert.Equal(3, depthTwo.Paths.Count);
        Assert.False(depthTwo.Truncated);

        // The depth-3 path is fully explainable as an ordered edge chain (REL-005).
        var depthThree = await traverse.ExecuteAsync(tenant, r, TraversalDirection.Outgoing, 3, 100);
        Assert.Equal(4, depthThree.Paths.Count);

        var chain = depthThree.Paths.Single(p => p.Depth == 3 && p.EndItemId == t);
        Assert.Equal(r, chain.StartItemId);
        Assert.Equal(3, chain.Edges.Count);
        Assert.Equal((r, d), (chain.Edges[0].FromItemId, chain.Edges[0].ToItemId));
        Assert.Equal((d, i), (chain.Edges[1].FromItemId, chain.Edges[1].ToItemId));
        Assert.Equal((i, t), (chain.Edges[2].FromItemId, chain.Edges[2].ToItemId));
        Assert.All(chain.Edges, e => Assert.Equal("references", e.RelationTypeCode));
    }

    [Fact]
    public async Task REL_004_traversal_enforces_the_result_limit_and_reports_truncation()
    {
        var fakes = CreateFakes();
        var tenant = TenantId.New();
        var project = ProjectId.New();

        var r = await SeedItemAsync(fakes, tenant, project, "doc");
        var d = await SeedItemAsync(fakes, tenant, project, "doc");
        var i = await SeedItemAsync(fakes, tenant, project, "doc");
        var t = await SeedItemAsync(fakes, tenant, project, "doc");

        await AddEdgeAsync(fakes, tenant, r, d);
        await AddEdgeAsync(fakes, tenant, d, i);
        await AddEdgeAsync(fakes, tenant, d, t);
        await AddEdgeAsync(fakes, tenant, i, t);

        var traverse = new TraverseRelationships(fakes.Relationships, fakes.Items);
        var limited = await traverse.ExecuteAsync(tenant, r, TraversalDirection.Outgoing, 3, 2);

        Assert.Equal(2, limited.Paths.Count);
        Assert.True(limited.Truncated);
    }

    [Fact]
    public async Task incoming_traversal_walks_edges_in_reverse()
    {
        var fakes = CreateFakes();
        var tenant = TenantId.New();
        var project = ProjectId.New();

        var r = await SeedItemAsync(fakes, tenant, project, "doc");
        var d = await SeedItemAsync(fakes, tenant, project, "doc");

        await AddEdgeAsync(fakes, tenant, r, d);

        var traverse = new TraverseRelationships(fakes.Relationships, fakes.Items);
        var result = await traverse.ExecuteAsync(tenant, d, TraversalDirection.Incoming, 2, 100);

        var path = Assert.Single(result.Paths);
        Assert.Equal(r, path.EndItemId);
        var edge = Assert.Single(path.Edges);
        Assert.Equal((d, r), (edge.FromItemId, edge.ToItemId));
    }

    [Fact]
    public async Task revoke_hides_the_revoked_edge_from_traversal()
    {
        var fakes = CreateFakes();
        var tenant = TenantId.New();
        var project = ProjectId.New();

        var r = await SeedItemAsync(fakes, tenant, project, "doc");
        var d = await SeedItemAsync(fakes, tenant, project, "doc");
        var relationship = await AddEdgeAsync(fakes, tenant, r, d);

        await new RevokeRelationship(fakes.Relationships, fakes.UnitOfWork)
            .ExecuteAsync(tenant, relationship.Id);

        var traverse = new TraverseRelationships(fakes.Relationships, fakes.Items);
        Assert.Empty((await traverse.ExecuteAsync(tenant, r, TraversalDirection.Outgoing, 3, 10)).Paths);
    }

    [Fact]
    public async Task register_relation_type_rejects_duplicate_codes_and_deprecate_hides_from_active_list()
    {
        var fakes = CreateFakes();
        var register = new RegisterRelationType(fakes.RelationTypes, fakes.UnitOfWork);

        var type = await register.ExecuteAsync("custom", "Custom", null, null, RelationDirection.Forward, RelationCardinality.ManyToMany);
        Assert.True(type.IsActive);

        await Assert.ThrowsAsync<ConflictException>(() =>
            register.ExecuteAsync("custom", "Another", null, null, RelationDirection.Forward, RelationCardinality.ManyToMany));

        await new DeprecateRelationType(fakes.RelationTypes, fakes.UnitOfWork).ExecuteAsync(type.Id);

        Assert.Empty(await new ListRelationTypes(fakes.RelationTypes).ExecuteAsync());
    }

    private static Fakes CreateFakes() => new();

    private static CreateRelationship CreateUseCase(Fakes fakes) =>
        new(fakes.Relationships, fakes.RelationTypes, fakes.Items, fakes.UnitOfWork);

    private static async Task<RelationType> SeedRelationTypeAsync(
        InMemoryRelationTypeRepository repository,
        string code,
        RelationCardinality cardinality,
        IReadOnlyCollection<string>? allowedSource = null,
        IReadOnlyCollection<string>? allowedTarget = null)
    {
        var type = RelationType.Create(code, code, allowedSource, allowedTarget, RelationDirection.Forward, cardinality);
        await repository.AddAsync(type);
        return type;
    }

    private static async Task<(KnowledgeItemId Source, KnowledgeItemId Target)> SeedTwoItemsAsync(
        Fakes fakes, TenantId tenantId, ProjectId projectId)
    {
        var source = await SeedItemAsync(fakes, tenantId, projectId, "doc");
        var target = await SeedItemAsync(fakes, tenantId, projectId, "doc");
        return (source, target);
    }

    private static async Task<KnowledgeItemId> SeedItemAsync(Fakes fakes, TenantId tenantId, ProjectId projectId, string typeId)
    {
        var item = KnowledgeItem.Create(
            tenantId,
            projectId,
            Guid.NewGuid().ToString("N"),
            "Item",
            Classification.Internal,
            RepresentationKind.Text,
            typeId,
            "1.0",
            PrincipalId.New());

        await fakes.Items.AddAsync(item);
        return item.Id;
    }

    private static async Task<Relationship> AddEdgeAsync(Fakes fakes, TenantId tenantId, KnowledgeItemId source, KnowledgeItemId target)
    {
        var sourceItem = await fakes.Items.GetByIdAsync(tenantId, source)
            ?? throw new InvalidOperationException($"Source item '{source}' was not found.");
        var relationship = Relationship.Create(tenantId, sourceItem.ProjectId, source, target, "references", PrincipalId.New());
        await fakes.Relationships.AddAsync(relationship);
        return relationship;
    }

    private sealed class Fakes
    {
        public InMemoryRelationshipRepository Relationships { get; } = new();
        public InMemoryRelationTypeRepository RelationTypes { get; } = new();
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryRelationTypeRepository : IRelationTypeRepository
    {
        public List<RelationType> Types { get; } = new();

        public Task AddAsync(RelationType relationType, CancellationToken ct = default)
        {
            Types.Add(relationType);
            return Task.CompletedTask;
        }

        public Task<RelationType?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Types.FirstOrDefault(t => t.Code == code));

        public Task<RelationType?> GetByIdAsync(RelationTypeId id, CancellationToken ct = default) =>
            Task.FromResult(Types.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<RelationType>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RelationType>>(Types.Where(t => t.IsActive).ToList());
    }

    private sealed class InMemoryRelationshipRepository : IRelationshipRepository
    {
        public List<Relationship> Relationships { get; } = new();

        public Task AddAsync(Relationship relationship, CancellationToken ct = default)
        {
            Relationships.Add(relationship);
            return Task.CompletedTask;
        }

        public Task<Relationship?> GetByIdAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default) =>
            Task.FromResult(Relationships.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

        public Task<IReadOnlyList<Relationship>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(
                Relationships.Where(r => r.TenantId == tenantId && r.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Relationship>> ListOutgoingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(
                Relationships.Where(r => r.TenantId == tenantId && r.SourceItemId == itemId).ToList());

        public Task<IReadOnlyList<Relationship>> ListIncomingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Relationship>>(
                Relationships.Where(r => r.TenantId == tenantId && r.TargetItemId == itemId).ToList());
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

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}