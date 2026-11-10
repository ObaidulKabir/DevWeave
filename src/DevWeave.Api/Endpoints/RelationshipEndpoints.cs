using DevWeave.Application.Abstractions;
using DevWeave.Application.Relations;
using DevWeave.Contracts.Relations.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Relationships REST surface for Slice 5 (SRS REL-001, REL-002, REL-003, REL-004, REL-005, REL-007,
/// SCH-007, BR-007). Relationship reads/writes are tenant-scoped; relation types are platform-global
/// configuration (like schemas). OIDC is out of scope: the tenant identifier is passed in the route.
/// </summary>
public static class RelationshipEndpoints
{
    public static IEndpointRouteBuilder MapRelationshipEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapRelationTypeEndpoints();

        var group = endpoints.MapGroup("/tenants/{tenantId:guid}");

        group.MapPost("/projects/{projectId:guid}/relationships", CreateRelationshipAsync);
        group.MapGet("/projects/{projectId:guid}/relationships", ListRelationshipsForProjectAsync);
        group.MapGet("/relationships/{relationshipId:guid}", GetRelationshipAsync);
        group.MapPost("/relationships/{relationshipId:guid}/revoke", RevokeRelationshipAsync);
        group.MapGet("/knowledge-items/{itemId:guid}/relationships", ListRelationshipsForItemAsync);
        group.MapPost("/knowledge-items/{itemId:guid}/relationships/traverse", TraverseRelationshipsAsync);

        return endpoints;
    }

    private static IEndpointRouteBuilder MapRelationTypeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/relation-types");

        group.MapGet("", ListRelationTypesAsync);
        group.MapPost("", RegisterRelationTypeAsync);
        group.MapGet("/{relationTypeId:guid}", GetRelationTypeAsync);
        group.MapPost("/{relationTypeId:guid}/deprecate", DeprecateRelationTypeAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateRelationshipAsync(
        Guid tenantId,
        Guid projectId,
        CreateRelationshipRequest request,
        CreateRelationship useCase,
        CancellationToken ct)
    {
        try
        {
            var relationship = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new KnowledgeItemId(request.SourceItemId),
                new KnowledgeItemId(request.TargetItemId),
                request.RelationTypeCode,
                new PrincipalId(request.CreatedBy),
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/relationships/{relationship.Id}",
                Map(relationship));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (CrossProjectReferenceException ex)
        {
            return Results.Conflict(new RelationshipError(CrossProjectReferenceException.Code, ex.Message));
        }
        catch (RelationTypeMismatchException ex)
        {
            return MapRelationTypeMismatch(ex);
        }
    }

    private static async Task<IResult> ListRelationshipsForProjectAsync(
        Guid tenantId,
        Guid projectId,
        ListRelationshipsForProject useCase,
        CancellationToken ct)
    {
        var relationships = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return Results.Ok(relationships.Select(Map).ToList());
    }

    private static async Task<IResult> GetRelationshipAsync(
        Guid tenantId,
        Guid relationshipId,
        GetRelationship useCase,
        CancellationToken ct)
    {
        var relationship = await useCase.ExecuteAsync(new TenantId(tenantId), new RelationshipId(relationshipId), ct);

        return relationship is null
            ? Results.NotFound()
            : Results.Ok(Map(relationship));
    }

    private static async Task<IResult> RevokeRelationshipAsync(
        Guid tenantId,
        Guid relationshipId,
        RevokeRelationship useCase,
        CancellationToken ct)
    {
        try
        {
            var relationship = await useCase.ExecuteAsync(new TenantId(tenantId), new RelationshipId(relationshipId), ct);

            return Results.Ok(Map(relationship));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> ListRelationshipsForItemAsync(
        Guid tenantId,
        Guid itemId,
        ListRelationshipsForItem useCase,
        CancellationToken ct)
    {
        try
        {
            var relationships = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeItemId(itemId), ct);

            return Results.Ok(relationships.Select(Map).ToList());
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> TraverseRelationshipsAsync(
        Guid tenantId,
        Guid itemId,
        TraverseRelationshipsRequest request,
        TraverseRelationships useCase,
        CancellationToken ct)
    {
        if (request.MaxDepth < 1 || request.MaxResults < 1)
            return Results.UnprocessableEntity(new RelationshipError(
                "invalid-traversal-limit",
                "MaxDepth and MaxResults must both be at least 1."));

        try
        {
            var result = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new KnowledgeItemId(itemId),
                request.Direction,
                request.MaxDepth,
                request.MaxResults,
                ct);

            return Results.Ok(Map(result));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> ListRelationTypesAsync(
        ListRelationTypes useCase,
        CancellationToken ct)
    {
        var types = await useCase.ExecuteAsync(ct);

        return Results.Ok(types.Select(Map).ToList());
    }

    private static async Task<IResult> RegisterRelationTypeAsync(
        RegisterRelationTypeRequest request,
        RegisterRelationType useCase,
        CancellationToken ct)
    {
        try
        {
            var type = await useCase.ExecuteAsync(
                request.Code,
                request.Name,
                request.AllowedSourceTypeIds,
                request.AllowedTargetTypeIds,
                request.Direction,
                request.Cardinality,
                ct);

            return Results.Created($"/relation-types/{type.Id}", Map(type));
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new RelationshipError("duplicate-relation-type", ex.Message));
        }
    }

    private static async Task<IResult> GetRelationTypeAsync(
        Guid relationTypeId,
        GetRelationType useCase,
        CancellationToken ct)
    {
        var type = await useCase.ExecuteAsync(new RelationTypeId(relationTypeId), ct);

        return type is null
            ? Results.NotFound()
            : Results.Ok(Map(type));
    }

    private static async Task<IResult> DeprecateRelationTypeAsync(
        Guid relationTypeId,
        DeprecateRelationType useCase,
        CancellationToken ct)
    {
        try
        {
            var type = await useCase.ExecuteAsync(new RelationTypeId(relationTypeId), ct);

            return Results.Ok(Map(type));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static IResult MapRelationTypeMismatch(RelationTypeMismatchException ex) =>
        ex.Code switch
        {
            // Request-level rule violations (BR-007 unknown type, REL-002/SCH-007 endpoint types) are
            // unprocessable; data-state conflicts (inactive type, cardinality) are 409.
            "unknown-relation-type" => Results.UnprocessableEntity(new RelationshipError(ex.Code, ex.Message)),
            "endpoint-type-not-allowed" => Results.UnprocessableEntity(new RelationshipError(ex.Code, ex.Message)),
            _ => Results.Conflict(new RelationshipError(ex.Code, ex.Message)),
        };

    private static RelationshipResponse Map(Relationship relationship) =>
        new(
            relationship.Id.Value,
            relationship.TenantId.Value,
            relationship.ProjectId.Value,
            relationship.SourceItemId.Value,
            relationship.TargetItemId.Value,
            relationship.RelationTypeCode,
            relationship.Status,
            relationship.CreatedAt,
            relationship.CreatedBy.Value);

    private static RelationTypeResponse Map(RelationType type) =>
        new(
            type.Id.Value,
            type.Code,
            type.Name,
            type.AllowedSourceTypeIds,
            type.AllowedTargetTypeIds,
            type.Direction,
            type.Cardinality,
            type.Status);

    private static TraversalResponse Map(TraversalResult result) =>
        new(
            result.Paths.Select(Map).ToList(),
            result.Truncated);

    private static TraversalPathResponse Map(TraversalPath path) =>
        new(
            path.StartItemId.Value,
            path.EndItemId.Value,
            path.Depth,
            path.Edges.Select(Map).ToList());

    private static TraversalEdgeResponse Map(TraversalEdge edge) =>
        new(
            edge.RelationshipId.Value,
            edge.FromItemId.Value,
            edge.ToItemId.Value,
            edge.RelationTypeCode);
}