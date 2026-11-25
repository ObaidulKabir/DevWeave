using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Archives;

public sealed record ArchiveKnowledgeRow(
    Guid Id,
    string Key,
    string Title,
    string Description,
    string Classification,
    string RepresentationKind,
    string TypeId,
    string SchemaVersion,
    string State,
    Guid CreatedBy);

public sealed record ArchiveRevisionRow(
    Guid Id,
    Guid ItemId,
    int RevisionNumber,
    string SchemaVersion,
    string Content,
    Guid Actor,
    string Reason,
    string? SourceReference,
    Guid? SupersededRevisionId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo);

public sealed record ArchiveRelationshipRow(
    Guid Id,
    Guid SourceItemId,
    Guid TargetItemId,
    string RelationTypeCode,
    string? Rationale,
    string? SourceReference,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    string Status,
    Guid CreatedBy);

public sealed record ArchivePinRow(Guid ItemId, Guid RevisionId, int Position);

public sealed record ArchiveActivityRow(
    Guid Id,
    string Type,
    Guid Actor,
    string Purpose,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    string? CorrelationId,
    string? Reason,
    Guid? ReferenceRevisionId,
    IReadOnlyList<ArchivePinRow> Inputs,
    IReadOnlyList<ArchivePinRow> Outputs);

public sealed record ArchiveEvidenceRow(
    Guid Id,
    Guid OriginActivityId,
    string Type,
    string? IntegrityHash,
    Guid CriterionItemId,
    Guid CriterionRevisionId,
    string RelatedCriterion,
    string ReviewState,
    Guid? ReviewedBy);

public sealed record ArchiveBundlePayload(
    Guid SourceTenantId,
    Guid SourceProjectId,
    IReadOnlyList<ArchiveKnowledgeRow> Knowledge,
    IReadOnlyList<ArchiveRevisionRow> Revisions,
    IReadOnlyList<ArchiveRelationshipRow> Relationships,
    IReadOnlyList<ArchiveActivityRow> Activities,
    IReadOnlyList<ArchiveEvidenceRow> Evidence);

public sealed record ArchiveDocument(string FormatVersion, string Checksum, ArchiveBundlePayload Payload);

public sealed record ArchiveReplayResult(
    IReadOnlyList<KnowledgeItem> Items,
    IReadOnlyList<KnowledgeRevision> Revisions,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<Activity> Activities,
    IReadOnlyList<EvidenceRecord> Evidence,
    IReadOnlyDictionary<Guid, Guid> IdentityMap);

/// <summary>
/// ArchiveBundleV1 codec. The checksum covers the payload only. Unknown format versions and
/// checksum mismatches fail closed.
/// </summary>
public static class ArchiveBundleCodec
{
    public const string FormatVersion = "devweave.archive.v1";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public static byte[] Encode(ArchiveBundlePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var payloadJson = Canonical(payload);
        var document = new ArchiveDocument(FormatVersion, Sha256(payloadJson), payload);
        return Encoding.UTF8.GetBytes(Canonical(document));
    }

    public static ArchiveDocument Decode(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            throw new ConflictException("Archive document is empty.");

        ArchiveDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<ArchiveDocument>(bytes, Options);
        }
        catch (JsonException ex)
        {
            throw new ConflictException($"Archive document is not readable: {ex.Message}");
        }

        if (document is null || string.IsNullOrWhiteSpace(document.FormatVersion))
            throw new ConflictException("Archive document has no format version.");
        if (!string.Equals(document.FormatVersion, FormatVersion, StringComparison.Ordinal))
            throw new ConflictException($"Unknown archive format '{document.FormatVersion}'.");
        if (document.Payload is null)
            throw new ConflictException("Archive payload is missing.");

        var checksum = Sha256(Canonical(document.Payload));
        if (!string.Equals(checksum, document.Checksum, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Archive checksum does not match the payload.");

        return document;
    }

    public static string Sha256(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Canonical<T>(T value) => JsonSerializer.Serialize(value, Options);
}

/// <summary>Reads a stored package. Only ArchiveBundleV1 is registered.</summary>
public sealed class ArchiveReaderRegistry
{
    public ArchiveDocument Read(byte[] bytes) => ArchiveBundleCodec.Decode(bytes);
}

public static class ArchiveGraphs
{
    public static ArchiveBundlePayload Capture(
        Guid sourceTenantId,
        Guid sourceProjectId,
        IReadOnlyList<KnowledgeItem> items,
        IReadOnlyList<KnowledgeRevision> revisions,
        IReadOnlyList<Relationship> relationships,
        IReadOnlyList<Activity> activities,
        IReadOnlyList<EvidenceRecord> evidence) =>
        new(
            sourceTenantId,
            sourceProjectId,
            items.Select(item => new ArchiveKnowledgeRow(
                item.Id.Value,
                item.Key,
                item.Title,
                item.Description,
                item.Classification.ToString(),
                item.RepresentationKind.ToString(),
                item.TypeId,
                item.SchemaVersion,
                item.State.ToString(),
                item.CreatedBy.Value)).ToArray(),
            revisions
                .OrderBy(revision => revision.RevisionNumber)
                .Select(revision => new ArchiveRevisionRow(
                    revision.Id.Value,
                    revision.KnowledgeItemId.Value,
                    revision.RevisionNumber,
                    revision.SchemaVersion,
                    revision.Content,
                    revision.Actor.Value,
                    revision.Reason,
                    revision.SourceReference,
                    revision.SupersededRevisionId?.Value,
                    revision.ValidFrom,
                    revision.ValidTo))
                .ToArray(),
            relationships.Select(edge => new ArchiveRelationshipRow(
                edge.Id.Value,
                edge.SourceItemId.Value,
                edge.TargetItemId.Value,
                edge.RelationTypeCode,
                edge.Rationale,
                edge.SourceReference,
                edge.ValidFrom,
                edge.ValidTo,
                edge.Status.ToString(),
                edge.CreatedBy.Value)).ToArray(),
            activities.Select(activity => new ArchiveActivityRow(
                activity.Id.Value,
                activity.Type.ToString(),
                activity.Actor.Value,
                activity.Purpose,
                activity.Status.ToString(),
                activity.StartedAt,
                activity.EndedAt,
                activity.CorrelationId,
                activity.Reason,
                activity.Reference?.Value,
                activity.Inputs.Select(pin => new ArchivePinRow(pin.ItemId.Value, pin.RevisionId.Value, pin.Position)).ToArray(),
                activity.Outputs.Select(pin => new ArchivePinRow(pin.ItemId.Value, pin.RevisionId.Value, pin.Position)).ToArray())).ToArray(),
            evidence.Select(record => new ArchiveEvidenceRow(
                record.Id.Value,
                record.OriginActivityId.Value,
                record.Type,
                record.IntegrityHash,
                record.CriterionItemId!.Value,
                record.CriterionRevisionId!.Value,
                record.RelatedCriterion,
                record.ReviewState.ToString(),
                record.ReviewedBy?.Value)).ToArray());

    public static void EnsureClean(int existingKnowledgeCount)
    {
        if (existingKnowledgeCount > 0)
            throw new ConflictException("Import requires a project with no knowledge items.");
    }

    public static ArchiveReplayResult Replay(
        ArchiveBundlePayload payload,
        TenantId tenantId,
        ProjectId projectId)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var map = new Dictionary<Guid, Guid>();
        var itemsBySource = new Dictionary<Guid, KnowledgeItem>();
        var items = new List<KnowledgeItem>();
        var revisions = new List<KnowledgeRevision>();
        var relationships = new List<Relationship>();
        var activities = new List<Activity>();
        var evidence = new List<EvidenceRecord>();

        foreach (var row in payload.Knowledge ?? Array.Empty<ArchiveKnowledgeRow>())
        {
            var item = KnowledgeItem.Create(
                tenantId,
                projectId,
                row.Key,
                row.Title,
                Enum.Parse<Classification>(row.Classification, ignoreCase: true),
                Enum.Parse<RepresentationKind>(row.RepresentationKind, ignoreCase: true),
                row.TypeId,
                row.SchemaVersion,
                new PrincipalId(row.CreatedBy),
                row.Description);
            map[row.Id] = item.Id.Value;
            itemsBySource[row.Id] = item;
            items.Add(item);
        }

        foreach (var row in (payload.Revisions ?? Array.Empty<ArchiveRevisionRow>()).OrderBy(row => row.RevisionNumber))
        {
            if (!itemsBySource.TryGetValue(row.ItemId, out var item))
                throw new ConflictException($"Archive revision '{row.Id}' refers to an unknown knowledge item.");

            var revision = item.AddRevision(
                row.SchemaVersion,
                row.Content,
                new PrincipalId(row.Actor),
                row.Reason,
                row.SourceReference,
                row.SupersededRevisionId is { } superseded && map.TryGetValue(superseded, out var mappedSuperseded)
                    ? new KnowledgeRevisionId(mappedSuperseded)
                    : null,
                item.CurrentRevisionNumber,
                row.ValidFrom,
                row.ValidTo);
            map[row.Id] = revision.Id.Value;
            revisions.Add(revision);
        }

        foreach (var row in payload.Knowledge ?? Array.Empty<ArchiveKnowledgeRow>())
        {
            if (!itemsBySource.TryGetValue(row.Id, out var item))
                continue;
            if (!Enum.TryParse<InfoState>(row.State, ignoreCase: true, out var state))
                throw new ConflictException($"Archive knowledge item '{row.Id}' has an unknown state.");
            if (state is InfoState.Approved or InfoState.Verified)
                item.Approve();
            else if (state == InfoState.Rejected)
                item.Reject();
        }

        foreach (var row in payload.Relationships ?? Array.Empty<ArchiveRelationshipRow>())
        {
            var edge = Relationship.Create(
                tenantId,
                projectId,
                MappedItem(map, row.SourceItemId),
                MappedItem(map, row.TargetItemId),
                row.RelationTypeCode,
                new PrincipalId(row.CreatedBy),
                row.Rationale,
                row.SourceReference,
                row.ValidFrom,
                row.ValidTo);
            if (string.Equals(row.Status, nameof(RelationshipStatus.Revoked), StringComparison.OrdinalIgnoreCase))
                edge.Revoke();
            map[row.Id] = edge.Id.Value;
            relationships.Add(edge);
        }

        foreach (var row in payload.Activities ?? Array.Empty<ArchiveActivityRow>())
        {
            if (!Enum.TryParse<ActivityType>(row.Type, ignoreCase: true, out var type))
                throw new ConflictException($"Archive activity '{row.Id}' has an unknown type.");
            if (!Enum.TryParse<ActivityStatus>(row.Status, ignoreCase: true, out var status))
                throw new ConflictException($"Archive activity '{row.Id}' has an unknown status.");

            var activity = Activity.Create(
                tenantId,
                projectId,
                type,
                new PrincipalId(row.Actor),
                row.Purpose,
                status,
                row.StartedAt,
                row.EndedAt,
                row.CorrelationId,
                row.Reason,
                row.ReferenceRevisionId is { } reference ? new KnowledgeRevisionId(Mapped(map, reference)) : null,
                Pins(map, row.Inputs, static (item, revision, position) => new ActivityInput(item, revision, position)),
                Pins(map, row.Outputs, static (item, revision, position) => new ActivityOutput(item, revision, position)));
            map[row.Id] = activity.Id.Value;
            activities.Add(activity);
        }

        foreach (var row in payload.Evidence ?? Array.Empty<ArchiveEvidenceRow>())
        {
            var record = EvidenceRecord.Create(
                tenantId,
                projectId,
                new ActivityId(Mapped(map, row.OriginActivityId)),
                row.Type,
                row.IntegrityHash,
                relatedArtifactId: null,
                MappedItem(map, row.CriterionItemId),
                new KnowledgeRevisionId(Mapped(map, row.CriterionRevisionId)),
                row.RelatedCriterion);
            if (Enum.TryParse<EvidenceReviewState>(row.ReviewState, ignoreCase: true, out var review) &&
                review != EvidenceReviewState.Unreviewed)
            {
                record.Review(review, new PrincipalId(row.ReviewedBy ?? row.OriginActivityId));
            }

            map[row.Id] = record.Id.Value;
            evidence.Add(record);
        }

        foreach (var row in payload.Knowledge ?? Array.Empty<ArchiveKnowledgeRow>())
        {
            if (!itemsBySource.TryGetValue(row.Id, out var item))
                continue;
            if (item.State != InfoState.Approved)
                continue;
            if (!string.Equals(row.State, nameof(InfoState.Verified), StringComparison.OrdinalIgnoreCase))
                continue;
            var qualifies = EvidenceRecord.ClaimIsVerified(
                evidence,
                criterionId => criterionId == item.Id ? item.CurrentRevisionId : null);
            if (qualifies)
                item.Verify(true);
        }

        return new ArchiveReplayResult(items, revisions, relationships, activities, evidence, map);
    }

    private static KnowledgeItemId MappedItem(IReadOnlyDictionary<Guid, Guid> map, Guid source) =>
        new(Mapped(map, source));

    private static Guid Mapped(IReadOnlyDictionary<Guid, Guid> map, Guid source)
    {
        if (!map.TryGetValue(source, out var destination))
            throw new ConflictException($"Archive reference '{source}' has no identity mapping.");
        return destination;
    }

    private static IReadOnlyList<TPin> Pins<TPin>(
        IReadOnlyDictionary<Guid, Guid> map,
        IReadOnlyList<ArchivePinRow>? pins,
        Func<KnowledgeItemId, KnowledgeRevisionId, int, TPin> create) =>
        (pins ?? Array.Empty<ArchivePinRow>())
            .Select(pin => create(MappedItem(map, pin.ItemId), new KnowledgeRevisionId(Mapped(map, pin.RevisionId)), pin.Position))
            .ToArray();
}
