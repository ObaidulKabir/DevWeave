using DevWeave.Application.Abstractions;
using DevWeave.Application.Schema;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Tests.Schema;

/// <summary>
/// Exercises the Schema registry use cases against an in-memory repository, focusing on the
/// registry's duplicate-identity rule (SCH-001), publish/deprecate flows (SCH-003) and active-schema
/// discovery (KNW-008).
/// </summary>
public sealed class SchemaUseCaseTests
{
    [Fact]
    public async Task SCH_001_register_creates_a_draft_schema()
    {
        var repo = new InMemorySchemaDefinitionRepository();

        var schema = await new RegisterSchema(repo, new InMemoryUnitOfWork())
            .ExecuteAsync("core.resource", "1.0", "platform", "Core resource", "{\"type\":\"object\"}");

        Assert.Equal(SchemaStatus.Draft, schema.Status);
        Assert.Equal("core.resource", schema.Namespace);
        Assert.Equal("1.0", schema.Version);
        Assert.Contains(schema, await repo.ListAsync());
    }

    [Fact]
    public async Task SCH_001_register_rejects_a_duplicate_namespace_and_version()
    {
        var repo = new InMemorySchemaDefinitionRepository();
        var register = new RegisterSchema(repo, new InMemoryUnitOfWork());

        await register.ExecuteAsync("core.resource", "1.0", "platform", "desc", "{}");

        await Assert.ThrowsAsync<ConflictException>(() =>
            register.ExecuteAsync("core.resource", "1.0", "platform", "another", "{}"));
    }

    [Fact]
    public async Task SCH_001_register_allows_the_same_namespace_across_distinct_versions()
    {
        var repo = new InMemorySchemaDefinitionRepository();
        var register = new RegisterSchema(repo, new InMemoryUnitOfWork());

        var v1 = await register.ExecuteAsync("core.resource", "1.0", "platform", "desc", "{}");
        var v2 = await register.ExecuteAsync("core.resource", "2.0", "platform", "desc", "{}");

        Assert.NotEqual(v1.Id, v2.Id);
        Assert.Equal(2, (await repo.ListAsync()).Count);
    }

    [Fact]
    public async Task SCH_003_publish_updates_status_to_published()
    {
        var repo = new InMemorySchemaDefinitionRepository();
        var created = await new RegisterSchema(repo, new InMemoryUnitOfWork())
            .ExecuteAsync("core.resource", "1.0", "platform", "desc", "{}");

        var published = await new PublishSchema(repo, new InMemoryUnitOfWork()).ExecuteAsync(created.Id);

        Assert.Equal(SchemaStatus.Published, published.Status);
        Assert.True(published.IsActive);
    }

    [Fact]
    public async Task publish_an_unknown_schema_throws_not_found()
    {
        var repo = new InMemorySchemaDefinitionRepository();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new PublishSchema(repo, new InMemoryUnitOfWork()).ExecuteAsync(SchemaDefinitionId.New()));
    }

    [Fact]
    public async Task KNW_008_discover_lists_only_active_published_schemas()
    {
        var repo = new InMemorySchemaDefinitionRepository();
        var register = new RegisterSchema(repo, new InMemoryUnitOfWork());

        var draft = await register.ExecuteAsync("core.resource", "1.0", "platform", "desc", "{}");
        var published = await register.ExecuteAsync("core.resource", "2.0", "platform", "desc", "{}");
        await new PublishSchema(repo, new InMemoryUnitOfWork()).ExecuteAsync(published.Id);
        var deprecated = await register.ExecuteAsync("core.resource", "3.0", "platform", "desc", "{}");
        await new DeprecateSchema(repo, new InMemoryUnitOfWork()).ExecuteAsync(deprecated.Id);
        _ = draft;

        var discovered = await new DiscoverSchemas(repo).ExecuteAsync();

        Assert.Single(discovered);
        Assert.Equal("core.resource", discovered[0].Namespace);
        Assert.Equal("2.0", discovered[0].Version);
    }

    [Fact]
    public async Task get_schema_returns_null_for_an_unknown_id()
    {
        var repo = new InMemorySchemaDefinitionRepository();

        Assert.Null(await new GetSchema(repo).ExecuteAsync(SchemaDefinitionId.New()));
    }

    private sealed class InMemorySchemaDefinitionRepository : ISchemaDefinitionRepository
    {
        public List<SchemaDefinition> Schemas { get; } = new();

        public Task AddAsync(SchemaDefinition schema, CancellationToken ct = default)
        {
            Schemas.Add(schema);
            return Task.CompletedTask;
        }

        public Task<SchemaDefinition?> FindAsync(string @namespace, string version, CancellationToken ct = default) =>
            Task.FromResult(Schemas.FirstOrDefault(s => s.Namespace == @namespace && s.Version == version));

        public Task<SchemaDefinition?> GetByIdAsync(SchemaDefinitionId id, CancellationToken ct = default) =>
            Task.FromResult(Schemas.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchemaDefinition>>(Schemas.ToList());
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}