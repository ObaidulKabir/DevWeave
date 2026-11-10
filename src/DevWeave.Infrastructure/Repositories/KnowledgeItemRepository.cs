using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

internal sealed class KnowledgeItemRepository : IKnowledgeItemRepository
{
    private readonly DevWeaveDbContext _db;

    public KnowledgeItemRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(KnowledgeItem item, CancellationToken ct = default)
    {
        _db.KnowledgeItems.Add(item);
        return Task.CompletedTask;
    }

    // Every read is tenant-scoped: a cross-tenant item is indistinguishable from a missing one.
    public Task<KnowledgeItem?> GetByIdAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default) =>
        _db.KnowledgeItems.SingleOrDefaultAsync(i => i.TenantId == tenantId && i.Id == id, ct);

    public async Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.KnowledgeItems
            .Where(i => i.TenantId == tenantId && i.ProjectId == projectId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
        await _db.KnowledgeItems
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(ct);

    // Key uniqueness is over (TenantId, ProjectId, Key), mirroring the unique index.
    public Task<bool> KeyExistsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
        _db.KnowledgeItems.AnyAsync(i => i.TenantId == tenantId && i.ProjectId == projectId && i.Key == key, ct);
}