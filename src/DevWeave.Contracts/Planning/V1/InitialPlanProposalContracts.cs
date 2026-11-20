namespace DevWeave.Contracts.Planning.V1;

/// <summary>Agent-submitted plan bundle for human review (empty or seeded projects).</summary>
public sealed record InitialPlanProposalV1(
    string IdempotencyKey,
    bool RequireEmptyProject,
    IReadOnlyList<PlanImportKnowledgeDraftV1> Knowledge,
    IReadOnlyList<PlanImportOutlineDraftV1> Outline,
    IReadOnlyList<PlanImportRelationDraftV1> Relations,
    IReadOnlyList<PlanImportMilestoneDraftV1> Milestones,
    IReadOnlyList<PlanImportWorkItemDraftV1> WorkItems,
    IReadOnlyList<PlanImportWorkLinkDraftV1>? WorkLinks = null);

public sealed record PlanImportKnowledgeDraftV1(
    string Ref,
    string Key,
    string Title,
    string TypeId,
    string SchemaVersion,
    string Classification,
    string RepresentationKind,
    string Content,
    string Reason,
    string? Description = null);

public sealed record PlanImportOutlineDraftV1(
    string ItemRef,
    string? ParentRef,
    int Position);

public sealed record PlanImportRelationDraftV1(
    string SourceRef,
    string TargetRef,
    string RelationTypeCode);

public sealed record PlanImportMilestoneDraftV1(
    string Ref,
    string Key,
    string Title,
    DateTimeOffset? DueAt = null);

public sealed record PlanImportWorkItemDraftV1(
    string Ref,
    string Key,
    string Title,
    string Description,
    string? MilestoneRef,
    string Priority,
    IReadOnlyList<string>? DependsOnRefs = null);

public sealed record PlanImportWorkLinkDraftV1(
    string WorkItemRef,
    string KnowledgeRef);

public sealed record InitialPlanProposalSummaryResponse(
    Guid Id,
    string IdempotencyKey,
    string PayloadHash,
    bool RequireEmptyProject,
    string Status,
    Guid ProposedBy,
    DateTimeOffset ProposedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? DecisionNote,
    int KnowledgeCount,
    int OutlineCount,
    int RelationCount,
    int MilestoneCount,
    int WorkItemCount);

public sealed record InitialPlanProposalDetailResponse(
    Guid Id,
    string IdempotencyKey,
    string PayloadHash,
    bool RequireEmptyProject,
    string Status,
    Guid ProposedBy,
    DateTimeOffset ProposedAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? DecisionNote,
    InitialPlanProposalV1 Payload,
    IReadOnlyDictionary<string, Guid>? ResultMapping);

public sealed record DecideInitialPlanProposalRequest(string? Note);
