using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DevWeave.Infrastructure.Repositories;

/// <summary>
/// Tenant-scoped persistence for relationships (SRS REL-001). Every read predicates on
/// <see cref="Relationship.TenantId"/> so a cross-tenant identifier is indistinguishable from a
/// missing row (negative disclosure).
/// </summary>
internal sealed class RelationshipRepository : IRelationshipRepository
{
    private readonly DevWeaveDbContext _db;

    public RelationshipRepository(DevWeaveDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Relationship relationship, CancellationToken ct = default)
    {
        _db.Relationships.Add(relationship);
        return Task.CompletedTask;
    }

    public Task<Relationship?> GetByIdAsync(TenantId tenantId, RelationshipId id, CancellationToken ct = default) =>
        _db.Relationships.SingleOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public async Task<IReadOnlyList<Relationship>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
        await _db.Relationships
            .Where(r => r.TenantId == tenantId && r.ProjectId == projectId)
            .OrderBy(r => r.CreatedAt)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Relationship>> ListOutgoingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
        await _db.Relationships
            .Where(r => r.TenantId == tenantId && r.SourceItemId == itemId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Relationship>> ListIncomingAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
        await _db.Relationships
            .Where(r => r.TenantId == tenantId && r.TargetItemId == itemId)
            .ToListAsync(ct);
}