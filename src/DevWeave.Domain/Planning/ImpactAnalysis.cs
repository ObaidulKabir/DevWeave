using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Domain.Planning;

public sealed record ImpactAnalysisReportId(Guid Value)
{
    public static ImpactAnalysisReportId New() => new(Guid.NewGuid());
}

/// <summary>
/// One reachable resource's governed impact disposition. Uncertainty never becomes
/// <see cref="ImpactDisposition.Unaffected"/>.
/// </summary>
public sealed record ImpactFinding(
    int Position,
    Guid ItemId,
    Guid? RevisionId,
    string ResourceKind,
    ImpactDisposition Disposition,
    string RelationPath,
    string Rule)
{
    public static ImpactFinding Create(
        int position,
        KnowledgeItemId itemId,
        KnowledgeRevisionId? revisionId,
        string resourceKind,
        ImpactDisposition disposition,
        string relationPath,
        string rule) =>
        new(
            position,
            itemId.Value,
            revisionId?.Value,
            resourceKind,
            disposition,
            relationPath,
            rule);

    public static ImpactFinding ForResource(
        int position,
        Guid resourceId,
        Guid? revisionId,
        string resourceKind,
        ImpactDisposition disposition,
        string relationPath,
        string rule) =>
        new(position, resourceId, revisionId, resourceKind, disposition, relationPath, rule);
}

/// <summary>
/// An analytical claim describing the closure of impact from a <see cref="RequirementChangeSet"/>.
/// It is not an automatic approval or rebase authorization.
/// </summary>
public sealed class ImpactAnalysisReport
{
    public const string CurrentRulesVersion = "impact-rules-1.0";

    private readonly List<ImpactFinding> _findings;

    public ImpactAnalysisReportId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public RequirementChangeSetId ChangeSetId { get; }
    public string RulesVersion { get; }
    public string PolicyVersion { get; }
    public DateTimeOffset AnalyzedAt { get; }
    public PrincipalId AnalyzedBy { get; }
    public IReadOnlyList<ImpactFinding> Findings => _findings;

    private ImpactAnalysisReport(
        ImpactAnalysisReportId id,
        TenantId tenantId,
        ProjectId projectId,
        RequirementChangeSetId changeSetId,
        string rulesVersion,
        string policyVersion,
        DateTimeOffset analyzedAt,
        PrincipalId analyzedBy)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        ChangeSetId = changeSetId;
        RulesVersion = rulesVersion;
        PolicyVersion = policyVersion;
        AnalyzedAt = analyzedAt;
        AnalyzedBy = analyzedBy;
        _findings = [];
    }

    public static ImpactAnalysisReport Create(
        TenantId tenantId,
        ProjectId projectId,
        RequirementChangeSetId changeSetId,
        PrincipalId analyzedBy,
        IReadOnlyList<ImpactFinding> findings,
        string policyVersion = "platform-default")
    {
        ArgumentNullException.ThrowIfNull(analyzedBy);
        ArgumentNullException.ThrowIfNull(findings);
        if (string.IsNullOrWhiteSpace(policyVersion))
            throw new ArgumentException("Policy version is required.", nameof(policyVersion));
        if (findings.Any(finding => finding.Disposition == ImpactDisposition.Unaffected))
            throw new ArgumentException(
                "Impact findings cannot claim Unaffected without a separate recorded proof path.");

        var report = new ImpactAnalysisReport(
            ImpactAnalysisReportId.New(),
            tenantId,
            projectId,
            changeSetId,
            CurrentRulesVersion,
            policyVersion.Trim(),
            DateTimeOffset.UtcNow,
            analyzedBy);

        report._findings.AddRange(findings
            .Select((finding, index) => finding with { Position = index }));

        return report;
    }
}

/// <summary>Relation codes that expand the requirement-impact closure.</summary>
public static class ImpactRelationCodes
{
    public static readonly IReadOnlySet<string> Closure = new HashSet<string>(StringComparer.Ordinal)
    {
        RelationTypeCodes.Satisfies,
        RelationTypeCodes.Implements,
        RelationTypeCodes.Validates,
        RelationTypeCodes.DependsOn,
        RelationTypeCodes.DerivedFrom,
        RelationTypeCodes.Supersedes,
    };
}

public enum PlanRebaseDecisionKind
{
    ContinueUnchanged = 1,
    ContinueAfterContextRefresh = 2,
    Replan = 3,
    Block = 4,
    Cancel = 5,
    Supersede = 6,
}

public sealed record PlanRebaseDecisionId(Guid Value)
{
    public static PlanRebaseDecisionId New() => new(Guid.NewGuid());
}

/// <summary>
/// Owner/Admin authorization for how one work package responds to a baseline/impact change. Ambiguous
/// or unscoped work defaults to suspension outside this record.
/// </summary>
public sealed class PlanRebaseDecision
{
    public PlanRebaseDecisionId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public WorkPackageId WorkPackageId { get; }
    public ImpactAnalysisReportId ImpactReportId { get; }
    public PlanRebaseDecisionKind Kind { get; }
    public PrincipalId AuthorizedBy { get; }
    public DateTimeOffset AuthorizedAt { get; }
    public string Reason { get; }
    public WorkPackageId? ContinuationWorkPackageId { get; private set; }

    private PlanRebaseDecision(
        PlanRebaseDecisionId id,
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        ImpactAnalysisReportId impactReportId,
        PlanRebaseDecisionKind kind,
        PrincipalId authorizedBy,
        DateTimeOffset authorizedAt,
        string reason)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        WorkPackageId = workPackageId;
        ImpactReportId = impactReportId;
        Kind = kind;
        AuthorizedBy = authorizedBy;
        AuthorizedAt = authorizedAt;
        Reason = reason;
    }

    public static PlanRebaseDecision Authorize(
        TenantId tenantId,
        ProjectId projectId,
        WorkPackageId workPackageId,
        ImpactAnalysisReportId impactReportId,
        PlanRebaseDecisionKind kind,
        PrincipalId authorizedBy,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(authorizedBy);
        ArgumentNullException.ThrowIfNull(workPackageId);
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rebase decision reason is required.", nameof(reason));

        return new PlanRebaseDecision(
            PlanRebaseDecisionId.New(),
            tenantId,
            projectId,
            workPackageId,
            impactReportId,
            kind,
            authorizedBy,
            DateTimeOffset.UtcNow,
            reason.Trim());
    }

    public void AttachContinuation(WorkPackageId continuationWorkPackageId)
    {
        ArgumentNullException.ThrowIfNull(continuationWorkPackageId);
        if (ContinuationWorkPackageId is not null)
            throw new InvalidOperationException("Continuation package is already attached.");
        if (!CreatesContinuation)
            throw new InvalidOperationException($"Decision kind '{Kind}' does not create a continuation package.");

        ContinuationWorkPackageId = continuationWorkPackageId;
    }

    public bool AllowsCampaignResume =>
        Kind is PlanRebaseDecisionKind.ContinueUnchanged
            or PlanRebaseDecisionKind.ContinueAfterContextRefresh
            or PlanRebaseDecisionKind.Supersede;

    public bool CreatesContinuation => AllowsCampaignResume;

    public bool RequiresContextRefresh => Kind == PlanRebaseDecisionKind.ContinueAfterContextRefresh;
}
