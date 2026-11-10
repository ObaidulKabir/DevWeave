using DevWeave.Application.Abstractions;
using DevWeave.Application.Agents;
using DevWeave.Api.Security;
using DevWeave.Contracts.Agents.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Agent principal and context-package REST surface for Slice 8 (SRS AGT-001..AGT-005, AGT-010,
/// AGT-013). Registration reuses the standard principal identity with <see cref="PrincipalType.AiAgent"/>,
/// capabilities are governed by an approve-then-active grant lifecycle, context assembly is
/// authorization-first, and executions are recorded as append-only provenance activities.
/// </summary>
public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapPost("/agents", RegisterAgentAsync);
        group.MapPost("/agents/{agentId:guid}/credentials", IssueCredentialAsync);
        group.MapGet("/agents/{agentId:guid}/credentials", ListCredentialsAsync);
        group.MapPost("/agents/{agentId:guid}/credentials/{credentialId:guid}/revoke", RevokeCredentialAsync);
        group.MapPost("/agents/{agentId:guid}/credentials/{credentialId:guid}/disable", DisableCredentialAsync);
        group.MapPost("/agents/{agentId:guid}/credentials/{credentialId:guid}/enable", EnableCredentialAsync);
        group.MapPost("/agents/{agentId:guid}/credentials/{credentialId:guid}/rotate", RotateCredentialAsync);
        group.MapPost("/agent-grants", GrantCapabilityAsync);
        group.MapPost("/agent-grants/{grantId:guid}/approve", ApproveGrantAsync);
        group.MapPost("/agent-grants/{grantId:guid}/revoke", RevokeGrantAsync);

        var agentGroup = endpoints.MapGroup("/tenants/{tenantId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Agent);
        agentGroup.MapGet("/agents/{agentId:guid}/capabilities", DiscoverCapabilitiesAsync);
        agentGroup.MapPost(
            "/projects/{projectId:guid}/agents/{agentId:guid}/context-packages",
            AssembleContextPackageAsync);
        agentGroup.MapPost("/projects/{projectId:guid}/agent-executions", RecordExecutionAsync);

        return endpoints;
    }

    private static async Task<IResult> RegisterAgentAsync(
        Guid tenantId,
        RegisterAgentRequest request,
        RegisterAgent useCase,
        CancellationToken ct)
    {
        var agent = await useCase.ExecuteAsync(new TenantId(tenantId), request.Email, ct);

        return Results.Created(
            $"/tenants/{tenantId}/agents/{agent.Id}",
            MapAgent(agent));
    }

    private static async Task<IResult> IssueCredentialAsync(
        Guid tenantId,
        Guid agentId,
        IssueAgentCredentialRequest request,
        IssueAgentCredential useCase,
        CancellationToken ct)
    {
        try
        {
            var issued = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                request.Label,
                request.ExpiresAt,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/agents/{agentId}/credentials/{issued.Credential.Id}",
                MapCredential(issued));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
    }

    private static async Task<IResult> RevokeCredentialAsync(
        Guid tenantId,
        Guid agentId,
        Guid credentialId,
        RevokeAgentCredentialRequest request,
        HttpContext http,
        RevokeAgentCredential useCase,
        CancellationToken ct)
    {
        try
        {
            var credential = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                new AgentCredentialId(credentialId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Ok(MapCredentialView(credential));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> DisableCredentialAsync(
        Guid tenantId,
        Guid agentId,
        Guid credentialId,
        DisableAgentCredentialRequest request,
        HttpContext http,
        DisableAgentCredential useCase,
        CancellationToken ct)
    {
        try
        {
            var credential = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                new AgentCredentialId(credentialId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Ok(MapCredentialView(credential));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> EnableCredentialAsync(
        Guid tenantId,
        Guid agentId,
        Guid credentialId,
        EnableAgentCredentialRequest request,
        HttpContext http,
        EnableAgentCredential useCase,
        CancellationToken ct)
    {
        try
        {
            var credential = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                new AgentCredentialId(credentialId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Ok(MapCredentialView(credential));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> RotateCredentialAsync(
        Guid tenantId,
        Guid agentId,
        Guid credentialId,
        RotateAgentCredentialRequest request,
        HttpContext http,
        RotateAgentCredential useCase,
        CancellationToken ct)
    {
        try
        {
            var issued = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                new AgentCredentialId(credentialId),
                AuthenticatedPrincipal.GetId(http),
                request.ExpiresAt,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/agents/{agentId}/credentials/{issued.Credential.Id}",
                MapCredential(issued));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> ListCredentialsAsync(
        Guid tenantId,
        Guid agentId,
        ListAgentCredentials useCase,
        CancellationToken ct)
    {
        try
        {
            var credentials = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(agentId),
                ct);

            return Results.Ok(credentials.Select(MapCredentialView).ToList());
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GrantCapabilityAsync(
        Guid tenantId,
        GrantAgentCapabilityRequest request,
        GrantAgentCapability useCase,
        CancellationToken ct)
    {
        try
        {
            var grant = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new PrincipalId(request.AgentId),
                request.ProjectId is { } projectId ? new ProjectId(projectId) : null,
                request.Capability,
                request.ExpiresAt,
                request.ClassificationCeiling,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/agent-grants/{grant.Id}",
                MapGrant(grant));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
    }

    private static async Task<IResult> ApproveGrantAsync(
        Guid tenantId,
        Guid grantId,
        ApproveAgentGrantRequest request,
        HttpContext http,
        ApproveAgentGrant useCase,
        CancellationToken ct)
    {
        try
        {
            var grant = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new AgentGrantId(grantId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Ok(MapGrant(grant));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> RevokeGrantAsync(
        Guid tenantId,
        Guid grantId,
        RevokeAgentGrantRequest request,
        HttpContext http,
        RevokeAgentGrant useCase,
        CancellationToken ct)
    {
        try
        {
            var grant = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new AgentGrantId(grantId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Ok(MapGrant(grant));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> DiscoverCapabilitiesAsync(
        Guid tenantId,
        Guid agentId,
        HttpContext http,
        DiscoverAgentCapabilities useCase,
        CancellationToken ct)
    {
        if (!IsAgentSubject(http.User, agentId))
            return Results.NotFound();

        try
        {
            var discovery = await useCase.ExecuteAsync(new TenantId(tenantId), new PrincipalId(agentId), ct);

            return Results.Ok(new AgentDiscoveryResponse(
                discovery.Grants.Select(MapGrant).ToList(),
                discovery.PermittedSchemas
                    .Select(schema => new SchemaDiscoveryResponse(
                        schema.Id.Value, schema.Namespace, schema.Version, schema.Description))
                    .ToList(),
                discovery.PermittedRelationTypes
                    .Select(type => new RelationTypeDiscoveryResponse(type.Id.Value, type.Code, type.Name))
                    .ToList(),
                discovery.PermittedTools.Select(MapToolName).ToList()));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AssembleContextPackageAsync(
        Guid tenantId,
        Guid projectId,
        Guid agentId,
        HttpContext http,
        AssembleContextPackageRequest request,
        AssembleContextPackage useCase,
        CancellationToken ct)
    {
        if (!IsAgentSubject(http.User, agentId))
            return Results.NotFound();

        try
        {
            var package = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                CurrentAgent(http.User),
                request.TaskKey,
                request.IdempotencyKey,
                (request.ItemIds ?? Array.Empty<Guid>()).Select(itemId => new KnowledgeItemId(itemId)).ToList(),
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/context-packages/{package.Id}",
                MapPackage(package));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new AgentError("agent-conflict", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new AgentError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> RecordExecutionAsync(
        Guid tenantId,
        Guid projectId,
        HttpContext http,
        RecordAgentExecutionRequest request,
        RecordAgentExecution useCase,
        CancellationToken ct)
    {
        try
        {
            var activity = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                CurrentAgent(http.User),
                new ContextPackageId(request.ContextPackageId),
                request.Purpose,
                request.Status,
                request.StartedAt,
                request.EndedAt,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/agent-executions/{activity.Id}",
                new AgentExecutionResponse(
                    activity.Id.Value,
                    request.ContextPackageId,
                    activity.Status,
                    activity.StartedAt,
                    activity.EndedAt));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static bool IsAgentSubject(System.Security.Claims.ClaimsPrincipal user, Guid expectedAgentId) =>
        Guid.TryParse(user.FindFirst(AuthClaims.PrincipalId)?.Value, out var authenticatedAgentId) &&
        authenticatedAgentId == expectedAgentId;

    private static PrincipalId CurrentAgent(System.Security.Claims.ClaimsPrincipal user) =>
        new(Guid.Parse(user.FindFirst(AuthClaims.PrincipalId)!.Value));

    private static AgentResponse MapAgent(Principal agent) =>
        new(agent.Id.Value, agent.TenantId.Value, agent.Email, agent.Type, agent.IsActive);

    private static AgentCredentialIssuedResponse MapCredential(AgentCredentialIssued issued) =>
        new(
            issued.Credential.Id.Value,
            issued.Credential.TenantId.Value,
            issued.Credential.AgentId.Value,
            issued.Credential.Label,
            issued.Credential.Status,
            issued.Credential.CreatedAt,
            issued.Credential.ExpiresAt,
            issued.PlainTextSecret);

    private static AgentCredentialResponse MapCredentialView(AgentCredential credential) =>
        new(
            credential.Id.Value,
            credential.TenantId.Value,
            credential.AgentId.Value,
            credential.Label,
            credential.Status,
            credential.CreatedAt,
            credential.ExpiresAt,
            credential.RevokedAt);

    private static AgentGrantResponse MapGrant(AgentGrant grant) =>
        new(
            grant.Id.Value,
            grant.TenantId.Value,
            grant.ProjectId?.Value,
            grant.AgentId.Value,
            grant.Capability,
            grant.ClassificationCeiling,
            grant.Status,
            grant.GrantedAt,
            grant.ExpiresAt,
            grant.ApprovedBy?.Value,
            grant.ApprovedAt,
            grant.RevokedBy?.Value,
            grant.RevokedAt);

    private static string MapToolName(AgentCapability capability) => capability switch
    {
        AgentCapability.ReadKnowledge => "read_knowledge",
        AgentCapability.WriteKnowledge => "write_knowledge",
        AgentCapability.AssembleContext => "assemble_context",
        AgentCapability.TraverseRelations => "traverse_relations",
        AgentCapability.ExecuteWorkflow => "execute_workflow",
        _ => capability.ToString(),
    };

    private static ContextPackageResponse MapPackage(ContextPackage package) =>
        new(
            package.Id.Value,
            package.TenantId.Value,
            package.ProjectId.Value,
            package.TaskKey,
            package.AgentId.Value,
            package.IdempotencyKey,
            package.CreatedAt,
            package.Entries
                .OrderBy(entry => entry.Position)
                .Select(entry => new ContextPackageEntryResponse(
                    entry.ItemId.Value,
                    entry.RevisionId.Value,
                    entry.Position,
                    entry.Representation))
                .ToList());
}
