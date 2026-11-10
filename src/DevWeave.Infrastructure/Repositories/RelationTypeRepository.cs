using DevWeave.Application.Abstractions;
using DevWeave.Domain.Relations;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

/// <summary>
/// Platform-global persistence for relation types. As with schema definitions, relation types are
/// platform configuration and reads are not tenant-scoped (SRS REL-002, REL-003).
/// </summary>
internal sealed class RelationTypeRepository : IRelationTypeRepository
{
    private readonly DevWeaveDbContext _db;

    public RelationTypeRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(RelationType relationType, CancellationToken ct = default)
    {
        _db.RelationTypes.Add(relationType);
        return Task.CompletedTask;
    }

    public Task<RelationType?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _db.RelationTypes.SingleOrDefaultAsync(rt => rt.Code == code, ct);

    public Task<RelationType?> GetByIdAsync(RelationTypeId id, CancellationToken ct = default) =>
        _db.RelationTypes.SingleOrDefaultAsync(rt => rt.Id == id, ct);

    public async Task<IReadOnlyList<RelationType>> ListActiveAsync(CancellationToken ct = default) =>
        await _db.RelationTypes
            .Where(rt => rt.Status == RelationTypeStatus.Active)
            .OrderBy(rt => rt.Code)
            .ToListAsync(ct);
}