using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Audit;

namespace DevWeave.Application.Audit;

public sealed class AuditJournal : IAuditJournal
{
    private readonly IAuditRepository _auditRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AuditJournal(IAuditRepository auditRepository, IUnitOfWork unitOfWork)
    {
        _auditRepository = auditRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task AppendAsync(AuthorizedScope scope, string action, string target, string outcome,
        CancellationToken ct = default)
    {
        // For target, we expect "ResourceType:ResourceId" or just "ResourceId"
        var parts = target.Split(':', 2);
        var resourceType = parts.Length > 1 ? parts[0] : "Unknown";
        var resourceId = parts.Length > 1 ? parts[1] : parts[0];

        var record = AuditRecord.Record(
            scope.TenantId,
            scope.ActorId,
            action,
            resourceType,
            resourceId,
            metadataJson: $"{{\"outcome\":\"{outcome}\"}}",
            correlationId: scope.CorrelationId);

        await _auditRepository.AddAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<Page<AuditView>> ListAsync(AuthorizedScope scope, PageRequest page,
        CancellationToken ct = default)
    {
        var records = await _auditRepository.ListByTenantAsync(scope.TenantId, ct);
        
        // Simple paging implementation for MVP (ignoring cursor)
        var items = records
            .Take(page.Limit)
            .Select(r => new AuditView(
                r.Id.Value,
                r.ActorId.Value,
                r.Action,
                $"{r.ResourceType}:{r.ResourceId}",
                r.MetadataJson ?? "{}",
                r.OccurredAt,
                r.CorrelationId ?? ""))
            .ToList();

        return new Page<AuditView>(items, null);
    }
}
