using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class KnowledgeRevisionRepository : IKnowledgeRevisionRepository
{
    private readonly DevWeaveDbContext _db;

    public KnowledgeRevisionRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(KnowledgeRevision revision, CancellationToken ct = default)
    {
        _db.KnowledgeRevisions.Add(revision);
        return Task.CompletedTask;
    }

    // Deny-by-default: a cross-tenant revision is indistinguishable from a missing one.
    public Task<KnowledgeRevision?> GetByIdAsync(TenantId tenantId, KnowledgeRevisionId id, CancellationToken ct = default) =>
        _db.KnowledgeRevisions.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public async Task<IReadOnlyList<KnowledgeRevision>> ListByItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
        await _db.KnowledgeRevisions
            .Where(r => r.TenantId == tenantId && r.KnowledgeItemId == itemId)
            .OrderBy(r => r.RevisionNumber)
            .ToListAsync(ct);
}