using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Planning;
using DevWeave.Contracts.Planning.V1;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Requirement change sets, impact analysis reports, and plan rebase decisions (dynamic project
/// state §6–§8). Recording a change set never mutates prior revisions; analysis is analytical only;
/// rebase authorization is the Owner/Admin gate before suspended campaigns may resume.
/// </summary>
public static class ImpactEndpoints
{
    public static IEndpointRouteBuilder MapImpactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapPost("/requirement-change-sets", RecordChangeSetAsync);
        group.MapGet("/requirement-change-sets/{changeSetId:guid}", GetChangeSetAsync);
        group.MapPost("/requirement-change-sets/{changeSetId:guid}/analyze", AnalyzeAsync);

        group.MapGet("/impact-reports", ListReportsAsync);
        group.MapGet("/impact-reports/{reportId:guid}", GetReportAsync);
        group.MapPost("/impact-reports/{reportId:guid}/rebase-decisions", AuthorizeRebaseAsync);
        group.MapGet("/impact-reports/{reportId:guid}/rebase-decisions", ListRebaseDecisionsAsync);

        return endpoints;
    }

    private static async Task<IResult> RecordChangeSetAsync(
        Guid tenantId,
        Guid projectId,
        RecordRequirementChangeSetRequest request,
        HttpContext http,
        RecordRequirementChangeSet useCase,
        CancellationToken ct)
    {
        try
        {
            var actor = AuthenticatedPrincipal.GetId(http);
            var changes = request.Changes.Select(entry => new RequirementChangeDescriptor(
                entry.Kind,
                entry.PreviousRevisionIds.Select(id => new KnowledgeRevisionId(id)).ToArray(),
                entry.ResultRevisionIds.Select(id => new KnowledgeRevisionId(id)).ToArray(),
                actor,
                entry.Reason,
                entry.ValidAt,
                entry.IdempotencyKey)).ToList();

            var changeSet = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                actor,
                changes,
                request.CorrelationId,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/requirement-change-sets/{changeSet.Id.Value}",
                Map(changeSet));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ImpactError("invalid-change", ex.Message));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetChangeSetAsync(
        Guid tenantId,
        Guid projectId,
        Guid changeSetId,
        GetRequirementChangeSet useCase,
        CancellationToken ct)
    {
        try
        {
            var changeSet = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new RequirementChangeSetId(changeSetId),
                ct);
            return Results.Ok(Map(changeSet));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AnalyzeAsync(
        Guid tenantId,
        Guid projectId,
        Guid changeSetId,
        HttpContext http,
        AnalyzeRequirementImpact useCase,
        CancellationToken ct)
    {
        try
        {
            var report = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new RequirementChangeSetId(changeSetId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/impact-reports/{report.Id.Value}",
                Map(report));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ImpactError("invalid-analysis", ex.Message));
        }
    }

    private static async Task<IResult> ListReportsAsync(
        Guid tenantId,
        Guid projectId,
        HttpContext http,
        ListImpactAnalysisReportsForProject useCase,
        CancellationToken ct)
    {
        try
        {
            var reports = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                AuthenticatedPrincipal.GetId(http),
                ct);
            return Results.Ok(reports.Select(Map).ToList());
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetReportAsync(
        Guid tenantId,
        Guid projectId,
        Guid reportId,
        GetImpactAnalysisReport useCase,
        CancellationToken ct)
    {
        try
        {
            var report = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new ImpactAnalysisReportId(reportId),
                ct);
            return Results.Ok(Map(report));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AuthorizeRebaseAsync(
        Guid tenantId,
        Guid projectId,
        Guid reportId,
        AuthorizePlanRebaseDecisionRequest request,
        HttpContext http,
        AuthorizePlanRebaseDecision useCase,
        CancellationToken ct)
    {
        try
        {
            var decision = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new ImpactAnalysisReportId(reportId),
                new WorkPackageId(request.WorkPackageId),
                request.Kind,
                AuthenticatedPrincipal.GetId(http),
                request.Reason,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/impact-reports/{reportId}/rebase-decisions/{decision.Id.Value}",
                Map(decision));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ImpactError("invalid-rebase", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new ImpactError("invalid-campaign-state", ex.Message));
        }
    }

    private static async Task<IResult> ListRebaseDecisionsAsync(
        Guid tenantId,
        Guid projectId,
        Guid reportId,
        ListPlanRebaseDecisionsForImpactReport useCase,
        CancellationToken ct)
    {
        try
        {
            var decisions = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new ImpactAnalysisReportId(reportId),
                ct);
            return Results.Ok(decisions.Select(Map).ToList());
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static RequirementChangeSetResponse Map(RequirementChangeSet changeSet) =>
        new(
            changeSet.Id.Value,
            changeSet.TenantId.Value,
            changeSet.ProjectId.Value,
            changeSet.RecordedBy.Value,
            changeSet.RecordedAt,
            changeSet.CorrelationId,
            changeSet.Changes.Select(entry => new RequirementChangeEntryResponse(
                entry.Position,
                entry.Kind,
                entry.PreviousRevisionIds,
                entry.ResultRevisionIds,
                entry.ActorId,
                entry.Reason,
                entry.ValidAt,
                entry.IdempotencyKey)).ToList());

    private static ImpactAnalysisReportResponse Map(ImpactAnalysisReport report) =>
        new(
            report.Id.Value,
            report.TenantId.Value,
            report.ProjectId.Value,
            report.ChangeSetId.Value,
            report.RulesVersion,
            report.PolicyVersion,
            report.AnalyzedAt,
            report.AnalyzedBy.Value,
            report.Findings.Select(finding => new ImpactFindingResponse(
                finding.Position,
                finding.ItemId,
                finding.RevisionId,
                finding.ResourceKind,
                finding.Disposition,
                finding.RelationPath,
                finding.Rule)).ToList());

    private static PlanRebaseDecisionResponse Map(PlanRebaseDecision decision) =>
        new(
            decision.Id.Value,
            decision.TenantId.Value,
            decision.ProjectId.Value,
            decision.WorkPackageId.Value,
            decision.ImpactReportId.Value,
            decision.Kind,
            decision.AuthorizedBy.Value,
            decision.AuthorizedAt,
            decision.Reason,
            decision.AllowsCampaignResume);
}
