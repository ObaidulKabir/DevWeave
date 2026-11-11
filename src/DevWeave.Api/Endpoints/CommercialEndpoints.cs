using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;

namespace DevWeave.Api.Endpoints;

public static class CommercialEndpoints
{
    public static IEndpointRouteBuilder MapCommercialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/commercial");

        group.MapPost("/plans", PublishPlanAsync).RequireAuthorization(ApiAuthorizationPolicies.PlatformAdministrator);
        group.MapPost("/assignments", AssignPlanAsync).RequireAuthorization(ApiAuthorizationPolicies.PlatformAdministrator);
        group.MapPost("/usage", RecordUsageAsync).RequireAuthorization(ApiAuthorizationPolicies.Agent);
        group.MapGet("/usage", ListUsageAsync).RequireAuthorization(ApiAuthorizationPolicies.Human);
        group.MapPost("/evaluate", EvaluateEntitlementAsync).RequireAuthorization(ApiAuthorizationPolicies.Agent);

        return endpoints;
    }

    private static async Task<IResult> PublishPlanAsync(Guid tenantId, PlanDraft request, HttpContext http, IPlanCatalogue useCase, CancellationToken ct)
    {
        var scope = CreateScope(tenantId, http, "Publish plan");
        var planId = await useCase.PublishAsync(scope, request, ct);
        return Results.Created($"/commercial/plans/{planId}", new { id = planId });
    }

    private static async Task<IResult> AssignPlanAsync(Guid tenantId, AssignPlanRequest request, HttpContext http, IPlanCatalogue useCase, CancellationToken ct)
    {
        var scope = CreateScope(tenantId, http, "Assign plan");
        var assignmentId = await useCase.AssignAsync(scope, tenantId, request.PlanId, request.EffectiveAt, ct);
        return Results.Created($"/commercial/assignments/{assignmentId}", new { id = assignmentId });
    }

    private static async Task<IResult> RecordUsageAsync(Guid tenantId, UsageObservation request, HttpContext http, IUsageMeter useCase, CancellationToken ct)
    {
        var scope = CreateScope(tenantId, http, "Record usage");
        var id = await useCase.RecordAsync(scope, request, ct);
        return Results.Created($"/commercial/usage/{id}", new { id });
    }

    private static async Task<IResult> ListUsageAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, HttpContext http, IUsageMeter useCase, CancellationToken ct)
    {
        var scope = CreateScope(tenantId, http, "List usage");
        var summary = await useCase.SummarizeAsync(scope, from, to, ct);
        return Results.Ok(summary);
    }

    private static async Task<IResult> EvaluateEntitlementAsync(Guid tenantId, EntitlementCheck request, HttpContext http, IEntitlementEvaluator useCase, CancellationToken ct)
    {
        var scope = CreateScope(tenantId, http, "Evaluate entitlement");
        var decision = await useCase.EvaluateAsync(scope, request.EntitlementKey, request.Requested, ct);
        return Results.Ok(decision);
    }

    private static AuthorizedScope CreateScope(Guid tenantId, HttpContext http, string purpose) =>
        new(new TenantId(tenantId), AuthenticatedPrincipal.GetId(http), null, purpose, http.TraceIdentifier);
}

public record AssignPlanRequest(Guid PlanId, DateTimeOffset EffectiveAt);
