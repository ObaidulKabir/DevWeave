using DevWeave.Domain.Activities;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Relations;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Activities;

/// <summary>
/// The pure result of reconstructing a project's state from its provenance timeline (SRS REV-007,
/// NFR-035). Activities, relationships, and evidence are filtered by recorded time; item revision
/// pointers honour both recorded and optional valid time.
/// </summary>
public sealed record ProjectStateReconstruction(
    IReadOnlyList<Activity> Activities,
    IReadOnlyList<ItemVersionSnapshot> Items,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<EvidenceRecord> Evidence);

/// <summary>
/// The revision each knowledge item had reached at the reconstruction point. A null
/// <see cref="RevisionId"/> with a zero <see cref="RevisionNumber"/> means "no revision yet".
/// </summary>
public sealed record ItemVersionSnapshot(KnowledgeItemId ItemId, KnowledgeRevisionId? RevisionId, int RevisionNumber);
