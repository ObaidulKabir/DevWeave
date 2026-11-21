using DevWeave.Api.Security;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Projections;
using DevWeave.Application.ProjectGraph;
using DevWeave.Contracts.Projections.V1;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Authorization-first stakeholder projections. Profiles are presentation intent only; membership and
/// agent grants remain the source of access. Restricted resources never appear in a profile whose
/// ceiling cannot see them, including counts and summary inputs.
/// </summary>
public static class ProjectionEndpoints
{
    public static IEndpointRouteBuilder MapProjectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}/projects/{projectId:guid}");
        group.MapGet("/projections/{profile}/{plan}", GetProjectionAsync);
        return endpoints;
    }

    private static async Task<IResult> GetProjectionAsync(
        Guid tenantId,
        Guid projectId,
        string profile,
        string plan,
        HttpContext http,
        StakeholderProjectionService projections,
        CancellationToken ct)
    {
        if (!Enum.TryParse<StakeholderProfile>(profile, ignoreCase: true, out var parsedProfile))
            return Results.BadRequest(new { error = $"Unknown stakeholder profile '{profile}'." });

        DateTimeOffset? asOf = DateTimeOffset.TryParse(http.Request.Query["asOf"], out var parsedAsOf)
            ? parsedAsOf
            : null;
        DateTimeOffset? asOfValid = DateTimeOffset.TryParse(http.Request.Query["asOfValid"], out var parsedValid)
            ? parsedValid
            : null;
        var startKind = http.Request.Query["startKind"].ToString();
        var startId = http.Request.Query["startId"].ToString();
        _ = int.TryParse(http.Request.Query["maxDepth"], out var maxDepth);
        _ = int.TryParse(http.Request.Query["maxResults"], out var maxResults);

        ProjectResourceRef? start = null;
        if (!string.IsNullOrWhiteSpace(startKind) &&
            !string.IsNullOrWhiteSpace(startId) &&
            Enum.TryParse<ProjectResourceKind>(startKind, ignoreCase: true, out var kind))
        {
            start = new ProjectResourceRef(kind, startId);
        }

        var scope = new AuthorizedScope(
            new TenantId(tenantId),
            AuthenticatedPrincipal.GetId(http),
            new ProjectId(projectId),
            $"Project {parsedProfile} {plan} projection",
            http.TraceIdentifier);

        try
        {
            var projection = await projections.BuildAsync(
                scope,
                parsedProfile,
                plan,
                asOf,
                ct,
                asOfValid,
                start,
                maxDepth,
                maxResults);
            return Results.Json(Map(projection));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static StakeholderProjectionResponse Map(StakeholderProjection projection) =>
        new(
            projection.AsOf,
            projection.Profile.ToString(),
            projection.Plan,
            projection.PolicyVersion,
            projection.Nodes.Select(node => new ProjectionNodeView(
                Map(node.Resource),
                node.Label,
                node.Classification.ToString(),
                node.RecordedAt)).ToArray(),
            projection.Edges.Select(Map).ToArray(),
            projection.Paths.Select(path => new ProjectionPathView(
                Map(path.Start),
                Map(path.End),
                path.Edges.Select(Map).ToArray())).ToArray(),
            projection.Counts.Select(count => new ProjectionCountView(
                count.Key,
                count.Value,
                count.Unit,
                count.IncludedPopulation,
                count.ExcludedPopulation)).ToArray(),
            projection.SummaryInputs.Select(Map).ToArray());

    private static ProjectionEdgeView Map(ProjectGraphEdge edge) =>
        new(Map(edge.From), Map(edge.To), edge.RelationType, edge.Origin is null ? null : Map(edge.Origin));

    private static ProjectionResourceRefView Map(ProjectResourceRef resource) =>
        new(resource.Kind.ToString(), resource.Identifier, resource.RevisionIdentifier);
}
