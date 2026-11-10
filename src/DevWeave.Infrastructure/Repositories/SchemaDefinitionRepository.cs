using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

/// <summary>
/// Platform-global persistence for schema definitions. Unlike tenant-owned repositories, reads are
/// not tenant-scoped: schema definitions are platform configuration (SRS SCH-001, KNW-008).
/// </summary>
internal sealed class SchemaDefinitionRepository : ISchemaDefinitionRepository
{
    private readonly DevWeaveDbContext _db;

    public SchemaDefinitionRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(SchemaDefinition schema, CancellationToken ct = default)
    {
        _db.SchemaDefinitions.Add(schema);
        return Task.CompletedTask;
    }

    public Task<SchemaDefinition?> FindAsync(string @namespace, string version, CancellationToken ct = default) =>
        _db.SchemaDefinitions.SingleOrDefaultAsync(s => s.Namespace == @namespace && s.Version == version, ct);

    public Task<SchemaDefinition?> GetByIdAsync(SchemaDefinitionId id, CancellationToken ct = default) =>
        _db.SchemaDefinitions.SingleOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken ct = default) =>
        await _db.SchemaDefinitions
            .OrderBy(s => s.Namespace)
            .ThenBy(s => s.Version)
            .ToListAsync(ct);
}