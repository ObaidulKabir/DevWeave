using DevWeave.Application.Abstractions;
using DevWeave.Domain.Schema;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class KnowledgeTypeDefinitionRepository(DevWeaveDbContext db) : IKnowledgeTypeDefinitionRepository
{
    public Task AddAsync(KnowledgeTypeDefinition definition, CancellationToken ct = default)
    {
        db.KnowledgeTypeDefinitions.Add(definition);
        return Task.CompletedTask;
    }

    public Task<KnowledgeTypeDefinition?> GetByTypeIdAsync(string typeId, CancellationToken ct = default) =>
        db.KnowledgeTypeDefinitions.FirstOrDefaultAsync(type => type.TypeId == typeId, ct);

    public async Task<IReadOnlyList<KnowledgeTypeDefinition>> ListAsync(CancellationToken ct = default) =>
        await db.KnowledgeTypeDefinitions
            .AsNoTracking()
            .OrderByDescending(type => type.IsCanonical)
            .ThenBy(type => type.TypeId)
            .ToListAsync(ct);
}
