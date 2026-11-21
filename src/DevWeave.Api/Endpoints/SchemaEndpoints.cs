using DevWeave.Application.Abstractions;
using DevWeave.Application.Schema;
using DevWeave.Api.Security;
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

        group.MapPost("", RegisterSchemaAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.PlatformAdministrator);
        group.MapPost("/{id:guid}/publish", PublishSchemaAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.PlatformAdministrator);
        group.MapPost("/{id:guid}/deprecate", DeprecateSchemaAsync)
            .RequireAuthorization(ApiAuthorizationPolicies.PlatformAdministrator);
        group.MapGet("", ListSchemasAsync);
        group.MapGet("/types", ListKnowledgeTypesAsync);
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
        IKnowledgeTypeDefinitionRepository types,
        CancellationToken ct)
    {
        var schemas = await useCase.ExecuteAsync(ct);
        var typeById = (await types.ListAsync(ct)).ToDictionary(type => type.TypeId, StringComparer.Ordinal);
        return Results.Ok(schemas.Select(schema => Map(schema, typeById.GetValueOrDefault(schema.Namespace))).ToList());
    }

    private static async Task<IResult> ListKnowledgeTypesAsync(
        ListKnowledgeTypes useCase,
        CancellationToken ct)
    {
        var types = await useCase.ExecuteAsync(ct);
        return Results.Ok(types.Select(Map).ToList());
    }

    private static async Task<IResult> DiscoverSchemasAsync(
        DiscoverSchemas useCase,
        IKnowledgeTypeDefinitionRepository types,
        CancellationToken ct)
    {
        var schemas = await useCase.ExecuteAsync(ct);
        var typeById = (await types.ListAsync(ct)).ToDictionary(type => type.TypeId, StringComparer.Ordinal);
        return Results.Ok(
            schemas.Select(schema =>
            {
                typeById.TryGetValue(schema.Namespace, out var type);
                return new SchemaDiscoveryItemResponse(
                    schema.Namespace,
                    schema.Version,
                    type?.IsCanonical ?? false,
                    type?.StorageKind.ToString());
            }).ToList());
    }

    private static async Task<IResult> GetSchemaAsync(
        Guid id,
        GetSchema useCase,
        IKnowledgeTypeDefinitionRepository types,
        CancellationToken ct)
    {
        var schema = await useCase.ExecuteAsync(new SchemaDefinitionId(id), ct);
        if (schema is null)
            return Results.NotFound();

        var type = await types.GetByTypeIdAsync(schema.Namespace, ct);
        return Results.Ok(Map(schema, type));
    }

    private static SchemaResponse Map(SchemaDefinition schema, KnowledgeTypeDefinition? type = null) =>
        new(
            schema.Id.Value,
            schema.Namespace,
            schema.Version,
            schema.Owner,
            schema.Description,
            schema.Status,
            schema.Document,
            schema.CreatedAt,
            schema.PublishedAt,
            type is null ? null : Map(type));

    private static KnowledgeTypeMetadataResponse Map(KnowledgeTypeDefinition type) =>
        new(
            type.TypeId,
            type.Owner,
            type.Description,
            type.StorageKind.ToString(),
            type.LifecyclePolicy,
            type.DefaultClassification,
            type.IsCanonical,
            type.MigrationStrategy);
}
