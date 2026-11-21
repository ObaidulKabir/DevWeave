using DevWeave.Domain.Planning;

namespace DevWeave.Contracts.Planning.V1;

/// <summary>One requirement-revision transition recorded inside a change set.</summary>
public sealed record RequirementChangeEntryRequest(
    RequirementChangeKind Kind,
    IReadOnlyList<Guid> PreviousRevisionIds,
    IReadOnlyList<Guid> ResultRevisionIds,
    string Reason,
    DateTimeOffset ValidAt,
    string IdempotencyKey);

/// <summary>Records a governed batch of requirement changes for later impact analysis.</summary>
public sealed record RecordRequirementChangeSetRequest(
    IReadOnlyList<RequirementChangeEntryRequest> Changes,
    string? CorrelationId = null);

/// <summary>Authorizes how one work package responds to an impact analysis report.</summary>
public sealed record AuthorizePlanRebaseDecisionRequest(
    Guid WorkPackageId,
    PlanRebaseDecisionKind Kind,
    string Reason);

public sealed record RequirementChangeEntryResponse(
    int Position,
    RequirementChangeKind Kind,
    IReadOnlyList<Guid> PreviousRevisionIds,
    IReadOnlyList<Guid> ResultRevisionIds,
    Guid ActorId,
    string Reason,
    DateTimeOffset ValidAt,
    string IdempotencyKey);

public sealed record RequirementChangeSetResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    Guid RecordedBy,
    DateTimeOffset RecordedAt,
    string? CorrelationId,
    IReadOnlyList<RequirementChangeEntryResponse> Changes);

public sealed record ImpactFindingResponse(
    int Position,
    Guid ItemId,
    Guid? RevisionId,
    string ResourceKind,
    ImpactDisposition Disposition,
    string RelationPath,
    string Rule);

public sealed record ImpactAnalysisReportResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    Guid ChangeSetId,
    string RulesVersion,
    string PolicyVersion,
    DateTimeOffset AnalyzedAt,
    Guid AnalyzedBy,
    IReadOnlyList<ImpactFindingResponse> Findings);

public sealed record PlanRebaseDecisionResponse(
    Guid Id,
    Guid TenantId,
    Guid ProjectId,
    Guid WorkPackageId,
    Guid ImpactReportId,
    PlanRebaseDecisionKind Kind,
    Guid AuthorizedBy,
    DateTimeOffset AuthorizedAt,
    string Reason,
    bool AllowsCampaignResume);

public sealed record ImpactError(string Code, string Message);
