using DevWeave.Application.Abstractions;
using DevWeave.Application.Schema;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Schema;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Schema registry REST surface (SRS SCH-001, SCH-003, KNW-008). The registry is platform-global, so
/// schema endpoints are not tenant-scoped. A payload validation failure during a knowledge revision
/// writes maps here to HTTP 422 with a path-specific body (KNW-004).
/// </summary>
public static class SchemaEndpoints
{
    public static IEndpointRouteBuilder MapSchemaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/schemas");

        group.MapPost("", RegisterSchemaAsync);
        group.MapPost("/{id:guid}/publish", PublishSchemaAsync);
        group.MapPost("/{id:guid}/deprecate", DeprecateSchemaAsync);
        group.MapGet("", ListSchemasAsync);
        group.MapGet("/discover", DiscoverSchemasAsync);
        group.MapGet("/{id:guid}", GetSchemaAsync);

        return endpoints;
    }

    private static async Task<IResult> RegisterSchemaAsync(
        RegisterSchemaRequest request,
        RegisterSchema useCase,
        CancellationToken ct)
    {
        try
        {
            var schema = await useCase.ExecuteAsync(
                request.Namespace,
                request.Version,
                request.Owner,
                request.Description,
                request.Document,
                ct);

            return Results.Created($"/schemas/{schema.Id}", Map(schema));
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new SchemaConflict("duplicate-schema", ex.Message));
        }
    }

    private static async Task<IResult> PublishSchemaAsync(
        Guid id,
        PublishSchema useCase,
        CancellationToken ct)
    {
        try
        {
            var schema = await useCase.ExecuteAsync(new SchemaDefinitionId(id), ct);

            return Results.Ok(Map(schema));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> DeprecateSchemaAsync(
        Guid id,
        DeprecateSchema useCase,
        CancellationToken ct)
    {
        try
        {
            var schema = await useCase.ExecuteAsync(new SchemaDefinitionId(id), ct);

            return Results.Ok(Map(schema));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> ListSchemasAsync(
        ListSchemas useCase,
        CancellationToken ct)
    {
        var schemas = await useCase.ExecuteAsync(ct);

        return Results.Ok(schemas.Select(Map).ToList());
    }

    private static async Task<IResult> DiscoverSchemasAsync(
        DiscoverSchemas useCase,
        CancellationToken ct)
    {
        var schemas = await useCase.ExecuteAsync(ct);

        return Results.Ok(
            schemas.Select(s => new SchemaDiscoveryItemResponse(s.Namespace, s.Version)).ToList());
    }

    private static async Task<IResult> GetSchemaAsync(
        Guid id,
        GetSchema useCase,
        CancellationToken ct)
    {
        var schema = await useCase.ExecuteAsync(new SchemaDefinitionId(id), ct);

        return schema is null
            ? Results.NotFound()
            : Results.Ok(Map(schema));
    }

    private static SchemaResponse Map(SchemaDefinition schema) =>
        new(
            schema.Id.Value,
            schema.Namespace,
            schema.Version,
            schema.Owner,
            schema.Description,
            schema.Status,
            schema.Document,
            schema.CreatedAt,
            schema.PublishedAt);
}