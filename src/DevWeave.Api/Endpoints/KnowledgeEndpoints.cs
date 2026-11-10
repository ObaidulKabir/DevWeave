using DevWeave.Application.Abstractions;
using DevWeave.Application.Knowledge;
using DevWeave.Api.Security;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Knowledge item and revision REST surface for Slice 3 (SRS KNW-001..KNW-013, REV-001..REV-008,
/// BR-004..BR-009). Tenant-scoped routes require a bearer identity matching the route tenant, and
/// tenant; optimistic-concurrency conflicts surface as HTTP 409 with a structured body. Write attribution
/// is derived from the authenticated principal, never from request-body identity fields.
/// </summary>
public static class KnowledgeEndpoints
{
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}");

        group.MapPost("/projects/{projectId:guid}/knowledge-items", CreateKnowledgeItemAsync);
        group.MapGet("/projects/{projectId:guid}/knowledge-items", ListKnowledgeItemsForProjectAsync);
        group.MapGet("/knowledge-items", ListKnowledgeItemsForTenantAsync);
        group.MapGet("/knowledge-items/{itemId:guid}", GetKnowledgeItemAsync);
        group.MapPost("/knowledge-items/{itemId:guid}/revisions", AddRevisionAsync);
        group.MapPost("/knowledge-items/{itemId:guid}/approve", ApproveItemAsync);
        group.MapPost("/knowledge-items/{itemId:guid}/reject", RejectItemAsync);
        group.MapPost("/knowledge-items/{itemId:guid}/verify", VerifyItemAsync);
        group.MapGet("/revisions/{revisionId:guid}", GetRevisionAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateKnowledgeItemAsync(
        Guid tenantId,
        Guid projectId,
        CreateKnowledgeItemRequest request,
        HttpContext http,
        CreateKnowledgeItem useCase,
        CancellationToken ct)
    {
        try
        {
            var item = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                request.Key,
                request.Title,
                request.Classification,
                request.RepresentationKind,
                request.TypeId,
                request.SchemaVersion,
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/knowledge-items/{item.Id}",
                Map(item));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new KnowledgeConflict("duplicate-key", ex.Message));
        }
    }

    private static async Task<IResult> ListKnowledgeItemsForProjectAsync(
        Guid tenantId,
        Guid projectId,
        ListKnowledgeItemsForProject useCase,
        CancellationToken ct)
    {
        var items = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return Results.Ok(items.Select(Map).ToList());
    }

    private static async Task<IResult> ListKnowledgeItemsForTenantAsync(
        Guid tenantId,
        ListKnowledgeItemsForTenant useCase,
        CancellationToken ct)
    {
        var items = await useCase.ExecuteAsync(new TenantId(tenantId), ct);

        return Results.Ok(items.Select(Map).ToList());
    }

    private static async Task<IResult> GetKnowledgeItemAsync(
        Guid tenantId,
        Guid itemId,
        GetKnowledgeItem useCase,
        CancellationToken ct)
    {
        var item = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeItemId(itemId), ct);

        return item is null
            ? Results.NotFound()
            : Results.Ok(Map(item));
    }

    private static async Task<IResult> AddRevisionAsync(
        Guid tenantId,
        Guid itemId,
        AddRevisionRequest request,
        HttpContext http,
        AddRevision useCase,
        CancellationToken ct)
    {
        try
        {
            var revision = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new KnowledgeItemId(itemId),
                request.ExpectedCurrentRevisionNumber,
                request.SchemaVersion,
                request.Content,
                AuthenticatedPrincipal.GetId(http),
                request.Reason,
                request.SourceReference,
                request.SupersededRevisionId is { } superseded ? new KnowledgeRevisionId(superseded) : null,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/revisions/{revision.Id}",
                Map(revision));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (PayloadValidationException ex)
        {
            var errors = ex.Issues
                .Select(issue => new SchemaValidationError(issue.Path, issue.Message))
                .ToList();

            return Results.UnprocessableEntity(new SchemaValidationErrorResponse(errors));
        }
        catch (SchemaVersionMismatchException ex)
        {
            return Results.Conflict(new KnowledgeConflict("schema-version-mismatch", ex.Message));
        }
        catch (ProhibitedContentException ex)
        {
            return Results.UnprocessableEntity(new KnowledgeConflict("prohibited-content", ex.Message));
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new KnowledgeConflict("stale-revision", ex.Message));
        }
    }

    private static async Task<IResult> ApproveItemAsync(
        Guid tenantId,
        Guid itemId,
        ApproveItem useCase,
        CancellationToken ct)
    {
        try
        {
            var item = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeItemId(itemId), ct);

            return Results.Ok(Map(item));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> RejectItemAsync(
        Guid tenantId,
        Guid itemId,
        RejectItem useCase,
        CancellationToken ct)
    {
        try
        {
            var item = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeItemId(itemId), ct);

            return Results.Ok(Map(item));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> VerifyItemAsync(
        Guid tenantId,
        Guid itemId,
        VerifyItem useCase,
        CancellationToken ct)
    {
        try
        {
            var item = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeItemId(itemId), ct);

            return Results.Ok(Map(item));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetRevisionAsync(
        Guid tenantId,
        Guid revisionId,
        GetRevision useCase,
        CancellationToken ct)
    {
        var revision = await useCase.ExecuteAsync(new TenantId(tenantId), new KnowledgeRevisionId(revisionId), ct);

        return revision is null
            ? Results.NotFound()
            : Results.Ok(Map(revision));
    }

    private static KnowledgeItemResponse Map(KnowledgeItem item) =>
        new(
            item.Id.Value,
            item.TenantId.Value,
            item.ProjectId.Value,
            item.Key,
            item.Title,
            item.Classification,
            item.RepresentationKind,
            item.State,
            item.ReviewState,
            item.VerificationState,
            item.CurrencyState,
            item.TypeId,
            item.SchemaVersion,
            item.CurrentRevisionId?.Value,
            item.CurrentRevisionNumber,
            item.CreatedBy.Value,
            item.CreatedAt);

    private static KnowledgeRevisionResponse Map(KnowledgeRevision revision) =>
        new(
            revision.Id.Value,
            revision.TenantId.Value,
            revision.KnowledgeItemId.Value,
            revision.RevisionNumber,
            revision.SchemaVersion,
            revision.Content,
            revision.Actor.Value,
            revision.RecordedAt,
            revision.Reason,
            revision.SourceReference,
            revision.SupersededRevisionId?.Value);
}
