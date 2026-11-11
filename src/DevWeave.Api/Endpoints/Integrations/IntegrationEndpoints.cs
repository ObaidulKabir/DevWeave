using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Api.Security;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints.Integrations;

public static class IntegrationEndpoints
{
    public static IEndpointRouteBuilder MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapPost("/integrations", ConnectAsync);
        group.MapPost("/integrations/{integrationId:guid}/rotate", RotateCredentialAsync);
        group.MapDelete("/integrations/{integrationId:guid}", DisconnectAsync);

        // Events ingress (SRS INT-004). The adapter verifies signed headers and freshness.
        // In a real implementation, this would likely be an anonymous endpoint with custom header validation.
        group.MapPost("/integrations/{integrationId:guid}/events", AcceptEventAsync);

        return endpoints;
    }

    private static async Task<IResult> ConnectAsync(
        Guid tenantId,
        [FromBody] IntegrationDraft draft,
        [FromServices] IIntegrationRegistry registry,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Connect integration",
            http.TraceIdentifier);

        var id = await registry.ConnectAsync(scope, draft);
        return Results.Accepted($"/tenants/{tenantId}/integrations/{id}", new { id });
    }

    private static async Task<IResult> RotateCredentialAsync(
        Guid tenantId,
        Guid integrationId,
        [FromBody] string newCredentialRef,
        [FromServices] IIntegrationRegistry registry,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Rotate integration credential",
            http.TraceIdentifier);

        await registry.RotateCredentialAsync(scope, integrationId, newCredentialRef);
        return Results.NoContent();
    }

    private static async Task<IResult> DisconnectAsync(
        Guid tenantId,
        Guid integrationId,
        [FromServices] IIntegrationRegistry registry,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Disconnect integration",
            http.TraceIdentifier);

        await registry.DisconnectAsync(scope, integrationId);
        return Results.NoContent();
    }

    private static async Task<IResult> AcceptEventAsync(
        Guid tenantId,
        Guid integrationId,
        [FromBody] IngressEnvelope envelope,
        [FromServices] IInboundEventProcessor processor,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Accept inbound event",
            http.TraceIdentifier);

        var decision = await processor.AcceptAsync(scope, integrationId, envelope);

        return decision.Outcome switch
        {
            "Accepted" => Results.Accepted(null, decision),
            "Conflict" => Results.Conflict(decision),
            _ => Results.BadRequest(decision)
        };
    }
}
