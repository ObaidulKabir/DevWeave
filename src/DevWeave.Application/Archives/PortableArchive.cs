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
        var revisions = await _revisionRepository.ListByTenantAsync(scope.TenantId, ct);
        var resourceRefs = revisions.Select(r => new ResourceRef(r.KnowledgeItemId.Value, r.Id.Value)).ToList();

        var artifacts = await _artifactRepository.ListByTenantAsync(scope.TenantId, ct);
        EnsureArtifactChecksumIntegrity(artifacts);
        var checksums = artifacts.Select(a => a.Checksum).Distinct(StringComparer.Ordinal).ToList();

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
        var manifest = await DescribeExportAsync(scope, ct);

        var jobId = Guid.NewGuid();

        await _auditJournal.AppendAsync(
            scope,
            "ExportStarted",
            $"Tenant:{scope.TenantId.Value}",
            $"JobId:{jobId};RevisionCount:{manifest.Revisions.Count};ChecksumCount:{manifest.ArtifactChecksums.Count}",
            ct);

        return jobId;
    }

    public async Task<Guid> BeginImportAsync(AuthorizedScope scope, string validatedManifestRef,
        string idempotencyKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(validatedManifestRef))
            throw new ArgumentException("validatedManifestRef is required.", nameof(validatedManifestRef));

        // EXP integrity: reject imports that declare checksums not present in the tenant store.
        if (validatedManifestRef.StartsWith("checksums:", StringComparison.OrdinalIgnoreCase))
        {
            var declared = validatedManifestRef["checksums:".Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var artifacts = await _artifactRepository.ListByTenantAsync(scope.TenantId, ct);
            var known = artifacts.Select(a => a.Checksum).ToHashSet(StringComparer.Ordinal);
            var missing = declared.Where(c => !known.Contains(c)).ToList();
            if (missing.Count > 0)
                throw new ConflictException($"Import rejected: manifest checksums not found in tenant storage ({missing.Count} missing).");
        }

        var jobId = Guid.NewGuid();

        await _auditJournal.AppendAsync(
            scope,
            "ImportStarted",
            $"Tenant:{scope.TenantId.Value}",
            $"Manifest:{validatedManifestRef};JobId:{jobId}",
            ct);

        return jobId;
    }

    /// <summary>EXP-001/002: every stored artifact must carry a non-empty checksum before export.</summary>
    internal static void EnsureArtifactChecksumIntegrity(IReadOnlyList<Domain.Artifacts.Artifact> artifacts)
    {
        var broken = artifacts.Where(a => string.IsNullOrWhiteSpace(a.Checksum)).ToList();
        if (broken.Count > 0)
            throw new ConflictException($"Export blocked: {broken.Count} artifact(s) missing checksum integrity metadata.");
    }
}
