using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Api.Security;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints.Archives;

public static class ArchiveEndpoints
{
    public static IEndpointRouteBuilder MapArchiveEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}")
            .RequireAuthorization(ApiAuthorizationPolicies.Human);

        group.MapGet("/export/manifest", DescribeExportAsync);
        group.MapPost("/export", BeginExportAsync);
        group.MapPost("/import", BeginImportAsync);
        group.MapGet("/audit", ListAuditLogsAsync);

        return endpoints;
    }

    private static async Task<IResult> DescribeExportAsync(
        Guid tenantId,
        [FromServices] IPortableArchive archive,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Describe export",
            http.TraceIdentifier);

        var manifest = await archive.DescribeExportAsync(scope);
        return Results.Ok(manifest);
    }

    private static async Task<IResult> BeginExportAsync(
        Guid tenantId,
        [FromBody] ExportDraft draft,
        [FromServices] IPortableArchive archive,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Begin export",
            http.TraceIdentifier);

        var id = await archive.BeginExportAsync(scope, draft.IdempotencyKey);
        return Results.Accepted($"/tenants/{tenantId}/export/{id}", new { id });
    }

    private static async Task<IResult> BeginImportAsync(
        Guid tenantId,
        [FromBody] ImportDraft draft,
        [FromServices] IPortableArchive archive,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "Begin import",
            http.TraceIdentifier);

        var id = await archive.BeginImportAsync(scope, draft.ValidatedManifestRef, draft.IdempotencyKey);
        return Results.Accepted($"/tenants/{tenantId}/import/{id}", new { id });
    }

    private static async Task<IResult> ListAuditLogsAsync(
        Guid tenantId,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        [FromServices] IAuditJournal journal,
        HttpContext http)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            null,
            "List audit logs",
            http.TraceIdentifier);

        var page = await journal.ListAsync(scope, new PageRequest(limit ?? 100, cursor));
        return Results.Ok(page);
    }
}
