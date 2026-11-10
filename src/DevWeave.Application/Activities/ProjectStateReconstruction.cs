using DevWeave.Domain.Activities;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Activities;

/// <summary>
/// The pure result of reconstructing a project's state from its provenance timeline (SRS REV-007).
/// <see cref="Activities"/> carries the ordered activity history, and <see cref="Items"/> carries the
/// current (or as-of) revision snapshot for every knowledge item in the project.
/// </summary>
public sealed record ProjectStateReconstruction(
    IReadOnlyList<Activity> Activities,
    IReadOnlyList<ItemVersionSnapshot> Items);

/// <summary>
/// The revision each knowledge item had reached at the reconstruction point. A null
/// <see cref="RevisionId"/> with a zero <see cref="RevisionNumber"/> means "no revision yet".
/// </summary>
public sealed record ItemVersionSnapshot(KnowledgeItemId ItemId, KnowledgeRevisionId? RevisionId, int RevisionNumber);