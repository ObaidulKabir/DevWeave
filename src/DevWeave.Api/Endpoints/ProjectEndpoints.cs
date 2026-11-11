using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Lifecycle;
using DevWeave.Application.Projects;
using DevWeave.Api.Security;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Project and lifecycle REST surface. Tenant-scoped routes require a bearer identity whose signed
/// tenant claim matches the route. Project membership and role checks are applied by the lifecycle
/// application services where implemented; broader resource authorization remains a launch gate.
/// </summary>
public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}");

        group.MapPost("/projects", CreateProjectAsync);
        group.MapGet("/projects", ListProjectsAsync);
        group.MapGet("/projects/{projectId:guid}", GetProjectAsync);
        group.MapPatch("/projects/{projectId:guid}/archive", ArchiveProjectAsync);
        group.MapPatch("/projects/{projectId:guid}/restore", RestoreProjectAsync);
        group.MapDelete("/projects/{projectId:guid}", DeleteProjectAsync);
        group.MapPost("/projects/{projectId:guid}/references", AddProjectReferenceAsync);
        group.MapPost("/projects/{projectId:guid}/members", GrantProjectMembershipAsync);
        group.MapPost("/projects/{projectId:guid}/members/{membershipId:guid}/revoke", RevokeProjectMembershipAsync);
        group.MapGet("/projects/{projectId:guid}/metrics", GetProjectMetricsAsync);
        group.MapPost("/projects/{projectId:guid}/reports", GenerateProjectReportAsync);
        group.MapPost("/lifecycle-profiles", CreateLifecycleProfileAsync);
        group.MapGet("/lifecycle-profiles", ListLifecycleProfilesAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateProjectAsync(
        Guid tenantId,
        CreateProjectRequest request,
        HttpContext http,
        CreateProject useCase,
        CancellationToken ct)
    {
        try
        {
            var project = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                request.Name,
                request.Key,
                request.Description,
                new LifecycleProfileId(request.LifecycleProfileId),
                AuthenticatedPrincipal.GetId(http),
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{project.Id}",
                Map(project));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException)
        {
            return Results.Conflict();
        }
    }

    private static async Task<IResult> ListProjectsAsync(
        Guid tenantId,
        HttpContext http,
        ListProjectsForTenant useCase,
        CancellationToken ct)
    {
        var projects = http.Request.Headers.Authorization.Count > 0
            ? await useCase.ExecuteAsync(
                new TenantId(tenantId),
                AuthenticatedPrincipal.GetId(http),
                ct)
            : await useCase.ExecuteAsync(new TenantId(tenantId), ct);

        return Results.Ok(projects.Select(Map).ToList());
    }

    private static async Task<IResult> GetProjectAsync(Guid tenantId, Guid projectId, GetProject useCase, CancellationToken ct)
    {
        var project = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return project is null
            ? Results.NotFound()
            : Results.Ok(Map(project));
    }

    private static async Task<IResult> ArchiveProjectAsync(Guid tenantId, Guid projectId, ArchiveProject useCase, CancellationToken ct)
    {
        try
        {
            var project = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

            return Results.Ok(Map(project));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> RestoreProjectAsync(Guid tenantId, Guid projectId, RestoreProject useCase, CancellationToken ct)
    {
        try
        {
            var project = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

            return Results.Ok(Map(project));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> DeleteProjectAsync(Guid tenantId, Guid projectId, DeleteProject useCase, CancellationToken ct)
    {
        try
        {
            await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

            return Results.NoContent();
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> AddProjectReferenceAsync(Guid tenantId, Guid projectId, AddProjectReferenceRequest request, AddProjectReference useCase, CancellationToken ct)
    {
        try
        {
            var reference = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                request.Type,
                request.ExternalId,
                request.Url,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/references/{reference.Id}",
                Map(reference));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GrantProjectMembershipAsync(Guid tenantId, Guid projectId, GrantProjectMembershipRequest request, GrantProjectMembership useCase, CancellationToken ct)
    {
        try
        {
            var membership = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                new PrincipalId(request.PrincipalId),
                request.Role,
                ct);

            return Results.Created(
                $"/tenants/{tenantId}/projects/{projectId}/members/{membership.Id}",
                Map(membership));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> RevokeProjectMembershipAsync(Guid tenantId, Guid projectId, Guid membershipId, RevokeProjectMembership useCase, CancellationToken ct)
    {
        try
        {
            var membership = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectMembershipId(membershipId), ct);

            return Results.Ok(Map(membership));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> GetProjectMetricsAsync(
        Guid tenantId,
        Guid projectId,
        [FromQuery] DateTimeOffset? asOf,
        [FromServices] IProjectReporting reporting,
        HttpContext http,
        CancellationToken ct)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            new ProjectId(projectId),
            "Get project metrics",
            http.TraceIdentifier);

        var metrics = await reporting.GetMetricsAsync(scope, asOf ?? DateTimeOffset.UtcNow, ct);
        return Results.Ok(metrics);
    }

    private static async Task<IResult> GenerateProjectReportAsync(
        Guid tenantId,
        Guid projectId,
        [FromBody] ReportDraft draft,
        [FromServices] IProjectReporting reporting,
        HttpContext http,
        CancellationToken ct)
    {
        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            new ProjectId(projectId),
            "Generate project report",
            http.TraceIdentifier);

        var reportId = await reporting.GenerateReportAsync(scope, draft.DeclaredPurpose, ct);
        return Results.Accepted($"/tenants/{tenantId}/projects/{projectId}/reports/{reportId}", new { id = reportId });
    }

    private static async Task<IResult> CreateLifecycleProfileAsync(Guid tenantId, CreateLifecycleProfileRequest request, CreateLifecycleProfile useCase, CancellationToken ct)
    {
        var profile = await useCase.ExecuteAsync(new TenantId(tenantId), request.Key, request.Name, request.Version, ct);

        return Results.Created(
            $"/tenants/{tenantId}/lifecycle-profiles/{profile.Id}",
            Map(profile));
    }

    private static async Task<IResult> ListLifecycleProfilesAsync(Guid tenantId, ListLifecycleProfilesForTenant useCase, CancellationToken ct)
    {
        var profiles = await useCase.ExecuteAsync(new TenantId(tenantId), ct);

        return Results.Ok(profiles.Select(Map).ToList());
    }

    private static ProjectResponse Map(Project project) =>
        new(
            project.Id.Value,
            project.TenantId.Value,
            project.Name,
            project.Key,
            project.Description,
            project.Status,
            project.LifecycleProfileId.Value,
            project.CreatedBy.Value,
            project.CreatedAt,
            project.UpdatedAt,
            project.ArchivedAt,
            project.RestoredAt,
            project.DeletedAt,
            project.IsDeleted);

    private static ProjectReferenceResponse Map(ProjectReference reference) =>
        new(
            reference.Id.Value,
            reference.TenantId.Value,
            reference.ProjectId.Value,
            reference.Type,
            reference.ExternalId,
            reference.Url,
            reference.CreatedAt);

    private static ProjectMembershipResponse Map(ProjectMembership membership) =>
        new(
            membership.Id.Value,
            membership.TenantId.Value,
            membership.ProjectId.Value,
            membership.PrincipalId.Value,
            membership.Role,
            membership.Status,
            membership.CreatedAt,
            membership.RevokedAt);

    private static LifecycleProfileResponse Map(LifecycleProfile profile) =>
        new(
            profile.Id.Value,
            profile.TenantId.Value,
            profile.Key,
            profile.Name,
            profile.Version,
            profile.CreatedAt);
}
