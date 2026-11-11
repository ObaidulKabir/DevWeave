using DevWeave.Api.Security;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Mcp.Protocol;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}/mcp");

        // The MCP endpoint allows agents to interact with the project coordination plane (SRS MCP-001).
        // It requires an authenticated agent principal.
        group.MapPost("/rpc", HandleMcpRpcAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
    }

    private static async Task<IResult> HandleMcpRpcAsync(
        Guid tenantId,
        Guid projectId,
        [FromHeader(Name = "X-Runtime-Id")] Guid? runtimeId,
        [FromBody] JsonRpcRequest request,
        [FromServices] McpServer server,
        HttpContext http,
        CancellationToken ct)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            new ProjectId(projectId),
            "MCP RPC",
            http.TraceIdentifier);

        var response = await server.HandleRequestAsync(scope, runtimeId ?? Guid.Empty, request, ct);
        return Results.Ok(response);
    }
}
