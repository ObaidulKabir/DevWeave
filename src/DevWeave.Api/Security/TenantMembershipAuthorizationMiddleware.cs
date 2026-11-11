using DevWeave.Application.Abstractions;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;

namespace DevWeave.Api.Security;

/// <summary>
/// Requires authenticated human callers on tenant routes to have an active local principal and
/// tenant membership. Agent credentials use their separate grant and project-membership policies.
/// </summary>
public sealed class TenantMembershipAuthorizationMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(
        HttpContext context,
        IPrincipalRepository principals,
        IMembershipRepository memberships,
        IProjectMembershipRepository projectMemberships,
        IKnowledgeItemRepository knowledgeItems,
        IKnowledgeRevisionRepository revisions,
        IArtifactRepository artifacts,
        IRelationshipRepository relationships,
        IActivityRepository activities,
        IEvidenceRepository evidence)
    {
        if (!configuration.GetValue("Authorization:RequireTenantMembership", defaultValue: true) ||
            context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.Request.RouteValues["tenantId"]?.ToString(), out var routeTenantId))
        {
            await next(context);
            return;
        }

        if (!Enum.TryParse<PrincipalType>(
                context.User.FindFirst(AuthClaims.PrincipalType)?.Value,
                ignoreCase: false,
                out var principalType) ||
            !Guid.TryParse(context.User.FindFirst(AuthClaims.PrincipalId)?.Value, out var principalGuid))
        {
            await HideResource(context);
            return;
        }

        // Agent identities are authenticated independently and authorized by the endpoint's agent
        // policy plus agent-specific credential, grant, and project-membership checks.
        if (principalType == PrincipalType.AiAgent)
        {
            await next(context);
            return;
        }

        if (principalType != PrincipalType.Human)
        {
            await HideResource(context);
            return;
        }

        var tenantId = new TenantId(routeTenantId);
        var principalId = new PrincipalId(principalGuid);
        var principal = await principals.GetByIdAsync(tenantId, principalId, context.RequestAborted);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
        {
            await HideResource(context);
            return;
        }

        var tenantMemberships = await memberships.ListByPrincipalAsync(
            tenantId,
            principalId,
            context.RequestAborted);
        var activeTenantMembership = tenantMemberships.FirstOrDefault(
            membership => membership.Status == MembershipStatus.Active);
        if (activeTenantMembership is null)
        {
            await HideResource(context);
            return;
        }

        if (RequiresTenantAdministrator(context) &&
            activeTenantMembership.Role is not (MembershipRole.Owner or MembershipRole.Admin))
        {
            await HideResource(context);
            return;
        }

        var projectId = await ResolveProjectIdAsync(
            context,
            tenantId,
            knowledgeItems,
            revisions,
            artifacts,
            relationships,
            activities,
            evidence);
        if (projectId is not null)
        {
            var projectRows = await projectMemberships.ListByPrincipalAsync(
                tenantId,
                principalId,
                context.RequestAborted);
            var projectMembership = projectRows.FirstOrDefault(membership =>
                membership.ProjectId == projectId &&
                membership.Status == ProjectMembershipStatus.Active);

            if (projectMembership is null ||
                (IsAdministrativeProjectOperation(context) &&
                 projectMembership.Role is not (ProjectRole.Owner or ProjectRole.Admin)) ||
                (!IsReadOnly(context) && projectMembership.Role == ProjectRole.Viewer))
            {
                await HideResource(context);
                return;
            }
        }

        await next(context);
    }

    private static bool IsAdministrativeProjectOperation(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        return path.Contains("/members", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/archive", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/restore", StringComparison.OrdinalIgnoreCase) ||
               (method == HttpMethods.Delete && path.Contains("/projects/", StringComparison.OrdinalIgnoreCase)) ||
               (method == HttpMethods.Post &&
                path.EndsWith("/coordination/baselines", StringComparison.OrdinalIgnoreCase)) ||
               path.EndsWith("/approve", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/reject", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/verify", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/review", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/revoke", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ProjectId?> ResolveProjectIdAsync(
        HttpContext context,
        TenantId tenantId,
        IKnowledgeItemRepository knowledgeItems,
        IKnowledgeRevisionRepository revisions,
        IArtifactRepository artifacts,
        IRelationshipRepository relationships,
        IActivityRepository activities,
        IEvidenceRepository evidence)
    {
        var route = context.Request.RouteValues;
        var path = context.Request.Path.Value ?? string.Empty;
        if (Guid.TryParse(route["projectId"]?.ToString(), out var projectGuid))
            return new ProjectId(projectGuid);

        if (Guid.TryParse(route["itemId"]?.ToString(), out var itemGuid) &&
            (path.Contains("/knowledge-items/", StringComparison.OrdinalIgnoreCase) || 
             path.Contains("/knowledge/", StringComparison.OrdinalIgnoreCase)))
        {
            return (await knowledgeItems.GetByIdAsync(
                tenantId,
                new KnowledgeItemId(itemGuid),
                context.RequestAborted))?.ProjectId;
        }

        if (Guid.TryParse(route["revisionId"]?.ToString(), out var revisionGuid))
        {
            var revision = await revisions.GetByIdAsync(
                tenantId,
                new KnowledgeRevisionId(revisionGuid),
                context.RequestAborted);
            if (revision is null)
                return null;

            return (await knowledgeItems.GetByIdAsync(
                tenantId,
                revision.KnowledgeItemId,
                context.RequestAborted))?.ProjectId;
        }

        if (Guid.TryParse(route["artifactId"]?.ToString(), out var artifactGuid))
        {
            return (await artifacts.GetByIdAsync(
                tenantId,
                new ArtifactId(artifactGuid),
                context.RequestAborted))?.ProjectId;
        }

        if (Guid.TryParse(route["relationshipId"]?.ToString(), out var relationshipGuid))
        {
            return (await relationships.GetByIdAsync(
                tenantId,
                new RelationshipId(relationshipGuid),
                context.RequestAborted))?.ProjectId;
        }

        if (Guid.TryParse(route["activityId"]?.ToString(), out var activityGuid))
        {
            return (await activities.GetByIdAsync(
                tenantId,
                new ActivityId(activityGuid),
                context.RequestAborted))?.ProjectId;
        }

        if (Guid.TryParse(route["claimId"]?.ToString(), out var claimGuid))
        {
            return (await activities.GetByIdAsync(
                tenantId,
                new ActivityId(claimGuid),
                context.RequestAborted))?.ProjectId;
        }

        if (Guid.TryParse(route["evidenceId"]?.ToString(), out var evidenceGuid))
        {
            return (await evidence.GetByIdAsync(
                tenantId,
                new EvidenceId(evidenceGuid),
                context.RequestAborted))?.ProjectId;
        }

        return null;
    }

    private static bool RequiresTenantAdministrator(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        var isTenantWideCollection =
            !Guid.TryParse(context.Request.RouteValues["projectId"]?.ToString(), out _) &&
            (HttpMethods.IsGet(method) &&
             (path.EndsWith("/projects", StringComparison.OrdinalIgnoreCase) ||
              path.EndsWith("/knowledge-items", StringComparison.OrdinalIgnoreCase) ||
              (path.Contains("/principals/", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith("/activities", StringComparison.OrdinalIgnoreCase))));
        var isTenantMembershipRoute =
            !Guid.TryParse(context.Request.RouteValues["projectId"]?.ToString(), out _) &&
            path.Contains("/members", StringComparison.OrdinalIgnoreCase);
        return isTenantMembershipRoute ||
               isTenantWideCollection ||
               path.Contains("/agents", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/agent-grants", StringComparison.OrdinalIgnoreCase) ||
               (HttpMethods.IsPost(method) &&
                (path.EndsWith("/principals", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith("/projects", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith("/lifecycle-profiles", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsReadOnly(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        return HttpMethods.IsGet(method) ||
               HttpMethods.IsHead(method) ||
               HttpMethods.IsOptions(method) ||
               (HttpMethods.IsPost(method) &&
                path.EndsWith("/search", StringComparison.OrdinalIgnoreCase));
    }

    private static Task HideResource(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
