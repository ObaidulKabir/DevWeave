using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Archives;
using DevWeave.Api.Security;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
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
        group.MapPost("/projects/{projectId:guid}/archives/export", ExportProjectArchiveAsync);
        group.MapPost("/projects/{projectId:guid}/archives/import", ImportProjectArchiveAsync);
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

        try
        {
            var id = await archive.BeginImportAsync(scope, draft.ValidatedManifestRef, draft.IdempotencyKey);
            return Results.Accepted($"/tenants/{tenantId}/import/{id}", new { id });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ExportProjectArchiveAsync(
        Guid tenantId,
        Guid projectId,
        [FromBody] ExportDraft draft,
        [FromServices] ProjectArchiveService archives,
        HttpContext http)
    {
        try
        {
            var job = await archives.ExportAsync(ProjectScope(tenantId, projectId, http, "Export project archive"), draft.IdempotencyKey);
            return Results.Accepted($"/tenants/{tenantId}/projects/{projectId}/archives/{job.Id}", job);
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ImportProjectArchiveAsync(
        Guid tenantId,
        Guid projectId,
        [FromBody] ProjectArchiveImportRequest request,
        [FromServices] ProjectArchiveService archives,
        HttpContext http)
    {
        try
        {
            var job = await archives.ImportAsync(
                ProjectScope(tenantId, projectId, http, "Import project archive"),
                request.ObjectKey,
                request.IdempotencyKey);
            return Results.Accepted($"/tenants/{tenantId}/projects/{projectId}/archives/{job.Id}", job);
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static AuthorizedScope ProjectScope(Guid tenantId, Guid projectId, HttpContext http, string purpose) =>
        new(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            new ProjectId(projectId),
            purpose,
            http.TraceIdentifier);

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

public sealed record ProjectArchiveImportRequest(string ObjectKey, string IdempotencyKey);
