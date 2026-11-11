using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Archives;

public sealed class PortableArchive : IPortableArchive
{
    private readonly IKnowledgeRevisionRepository _revisionRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditJournal _auditJournal;

    public PortableArchive(
        IKnowledgeRevisionRepository revisionRepository,
        IArtifactRepository artifactRepository,
        ITenantContext tenantContext,
        IAuditJournal auditJournal)
    {
        _revisionRepository = revisionRepository;
        _artifactRepository = artifactRepository;
        _tenantContext = tenantContext;
        _auditJournal = auditJournal;
    }

    public async Task<ExportManifest> DescribeExportAsync(AuthorizedScope scope, CancellationToken ct = default)
    {
        // SRS DATA-01, DATA-02: Describe the tenant's portable data set.
        // For MVP, we collect all knowledge revisions and artifact checksums for the tenant.
        
        // Note: Real implementation would use a more efficient query or projection.
        // Here we simulate the manifest generation.
        
        var revisions = await _revisionRepository.ListByTenantAsync(scope.TenantId, ct);
        var resourceRefs = revisions.Select(r => new ResourceRef(r.KnowledgeItemId.Value, r.Id.Value)).ToList();
        
        var artifacts = await _artifactRepository.ListByTenantAsync(scope.TenantId, ct);
        var checksums = artifacts.Select(a => a.Checksum).Distinct().ToList();

        return new ExportManifest(
            scope.TenantId.Value,
            "1.0",
            DateTimeOffset.UtcNow,
            resourceRefs,
            checksums);
    }

    public async Task<Guid> BeginExportAsync(AuthorizedScope scope, string idempotencyKey,
        CancellationToken ct = default)
    {
        // SRS DATA-03: Asynchronous export start.
        var manifest = await DescribeExportAsync(scope, ct);
        
        var jobId = Guid.NewGuid();
        
        await _auditJournal.AppendAsync(
            scope,
            "ExportStarted",
            $"Tenant:{scope.TenantId.Value}",
            $"JobId:{jobId}",
            ct);
        
        return jobId;
    }

    public async Task<Guid> BeginImportAsync(AuthorizedScope scope, string validatedManifestRef,
        string idempotencyKey, CancellationToken ct = default)
    {
        // SRS DATA-05: Asynchronous import start.
        var jobId = Guid.NewGuid();
        
        await _auditJournal.AppendAsync(
            scope,
            "ImportStarted",
            $"Tenant:{scope.TenantId.Value}",
            $"Manifest:{validatedManifestRef};JobId:{jobId}",
            ct);
        
        return jobId;
    }
}
