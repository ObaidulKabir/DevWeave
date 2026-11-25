using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Archives;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Archives;

public sealed record ArchiveJobView(Guid Id, string ObjectKey, string Checksum, string FormatVersion, string Kind);

/// <summary>
/// Writes and reads an ArchiveBundleV1 package in object storage, with a durable job row.
/// Import replays into a project that has no knowledge yet and records the identity map.
/// </summary>
public sealed class ProjectArchiveService
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IKnowledgeRevisionRepository _revisions;
    private readonly IRelationshipRepository _relationships;
    private readonly IActivityRepository _activities;
    private readonly IEvidenceRepository _evidence;
    private readonly IArchiveJobRepository _jobs;
    private readonly IObjectStorage _storage;
    private readonly IContentInspector _inspector;
    private readonly ITransactionalOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ArchiveReaderRegistry _readers;

    public ProjectArchiveService(
        IKnowledgeItemRepository items,
        IKnowledgeRevisionRepository revisions,
        IRelationshipRepository relationships,
        IActivityRepository activities,
        IEvidenceRepository evidence,
        IArchiveJobRepository jobs,
        IObjectStorage storage,
        IContentInspector inspector,
        ITransactionalOutbox outbox,
        IUnitOfWork unitOfWork,
        ArchiveReaderRegistry readers)
    {
        _items = items;
        _revisions = revisions;
        _relationships = relationships;
        _activities = activities;
        _evidence = evidence;
        _jobs = jobs;
        _storage = storage;
        _inspector = inspector;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _readers = readers;
    }

    public async Task<ArchiveJobView> ExportAsync(AuthorizedScope scope, string idempotencyKey, CancellationToken ct = default)
    {
        var projectId = RequireProject(scope);
        var existing = await _jobs.FindAsync(scope.TenantId, projectId, "Export", idempotencyKey, ct);
        if (existing is not null)
            return View(existing);

        var items = await _items.ListByProjectAsync(scope.TenantId, projectId, ct);
        var revisions = new List<Domain.Knowledge.KnowledgeRevision>();
        foreach (var item in items)
            revisions.AddRange(await _revisions.ListByItemAsync(scope.TenantId, item.Id, ct));
        var relationships = await _relationships.ListByProjectAsync(scope.TenantId, projectId, ct);
        var activities = await _activities.ListForProjectAsync(scope.TenantId, projectId, ct);
        var evidence = (await _evidence.ListForProjectAsync(scope.TenantId, projectId, ct))
            .Where(record => record.CriterionItemId is not null && record.CriterionRevisionId is not null)
            .ToArray();

        var payload = ArchiveGraphs.Capture(
            scope.TenantId.Value,
            projectId.Value,
            items,
            revisions,
            relationships,
            activities,
            evidence);
        var bytes = ArchiveBundleCodec.Encode(payload);
        await RequireAllowedAsync(bytes, ct);

        var jobId = Guid.NewGuid();
        var objectKey = $"archives/{scope.TenantId.Value:N}/{projectId.Value:N}/{jobId:N}.json";
        await _storage.PutAsync(objectKey, bytes, "application/json", ct);
        var document = _readers.Read(bytes);
        var job = ArchiveJob.Create(
            scope.TenantId,
            projectId,
            "Export",
            idempotencyKey,
            objectKey,
            document.Checksum,
            document.FormatVersion,
            identityMapJson: null);
        await _jobs.AddAsync(job, ct);
        await _outbox.EnqueueAsync(
            scope,
            "ArchiveExported",
            ArchiveBundleCodec.FormatVersion,
            JsonSerializer.Serialize(new { jobId = job.Id, objectKey, checksum = document.Checksum }),
            ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return View(job);
    }

    public async Task<ArchiveJobView> ImportAsync(
        AuthorizedScope scope,
        string objectKey,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var projectId = RequireProject(scope);
        var existing = await _jobs.FindAsync(scope.TenantId, projectId, "Import", idempotencyKey, ct);
        if (existing is not null)
            return View(existing);

        var present = await _items.ListByProjectAsync(scope.TenantId, projectId, ct);
        ArchiveGraphs.EnsureClean(present.Count);

        await using var stream = await _storage.GetAsync(objectKey, ct);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        await RequireAllowedAsync(bytes, ct);
        var document = _readers.Read(bytes);
        var replay = ArchiveGraphs.Replay(document.Payload, scope.TenantId, projectId);

        foreach (var item in replay.Items)
            await _items.AddAsync(item, ct);
        foreach (var revision in replay.Revisions)
            await _revisions.AddAsync(revision, ct);
        foreach (var edge in replay.Relationships)
            await _relationships.AddAsync(edge, ct);
        foreach (var activity in replay.Activities)
            await _activities.AddAsync(activity, ct);
        foreach (var record in replay.Evidence)
            await _evidence.AddAsync(record, ct);

        var mapJson = JsonSerializer.Serialize(replay.IdentityMap);
        var job = ArchiveJob.Create(
            scope.TenantId,
            projectId,
            "Import",
            idempotencyKey,
            objectKey,
            document.Checksum,
            document.FormatVersion,
            mapJson);
        await _jobs.AddAsync(job, ct);
        await _outbox.EnqueueAsync(
            scope,
            "ArchiveImported",
            ArchiveBundleCodec.FormatVersion,
            JsonSerializer.Serialize(new { jobId = job.Id, objectKey, checksum = document.Checksum, mapped = replay.IdentityMap.Count }),
            ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return View(job);
    }

    private async Task RequireAllowedAsync(byte[] bytes, CancellationToken ct)
    {
        var inspection = await _inspector.InspectAsync("archive.json", "application/json", bytes, ct);
        if (inspection.Verdict != ContentVerdict.Allow)
            throw new ConflictException("Archive package was rejected by the content policy.");
    }

    private static ProjectId RequireProject(AuthorizedScope scope) =>
        scope.ProjectId ?? throw new ArgumentException("Project ID is required for an archive job.");

    private static ArchiveJobView View(ArchiveJob job) =>
        new(job.Id, job.ObjectKey, job.Checksum, job.FormatVersion, job.Kind);
}
