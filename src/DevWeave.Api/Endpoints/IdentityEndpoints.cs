using DevWeave.Application.Abstractions;
using DevWeave.Application.Identity;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Domain.Identity;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Tenant and principal REST surface. Tenant creation is the explicit anonymous bootstrap operation;
/// tenant-scoped routes require a bearer identity whose signed tenant claim matches the route.
/// Fine-grained tenant membership and administrative role checks remain application authorization gates.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants");

        group.MapPost("", CreateTenantAsync).AllowAnonymous();
        group.MapGet("/{tenantId:guid}", GetTenantAsync);
        group.MapPost("/{tenantId:guid}/principals", CreatePrincipalAsync);
        group.MapPost("/{tenantId:guid}/members", InviteMemberAsync);
        group.MapGet("/{tenantId:guid}/members", ListMembersAsync);
        group.MapPost("/{tenantId:guid}/members/{membershipId:guid}/activate", ActivateMemberAsync);
        group.MapPost("/{tenantId:guid}/members/{membershipId:guid}/revoke", RevokeMemberAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateTenantAsync(CreateTenantRequest request, CreateTenant useCase, CancellationToken ct)
    {
        var tenant = await useCase.ExecuteAsync(request.Name, ct);

        return Results.Created(
            $"/tenants/{tenant.Id}",
            new TenantResponse(tenant.Id.Value, tenant.Name, tenant.CreatedAt));
    }

    private static async Task<IResult> GetTenantAsync(Guid tenantId, GetTenant useCase, CancellationToken ct)
    {
        var tenant = await useCase.ExecuteAsync(new TenantId(tenantId), ct);

        return tenant is null
            ? Results.NotFound()
            : Results.Ok(new TenantResponse(tenant.Id.Value, tenant.Name, tenant.CreatedAt));
    }

    private static async Task<IResult> CreatePrincipalAsync(Guid tenantId, CreatePrincipalRequest request, CreatePrincipal useCase, CancellationToken ct)
    {
        var principal = await useCase.ExecuteAsync(new TenantId(tenantId), request.Email, request.Type, ct);

        return Results.Created(
            $"/tenants/{tenantId}/principals/{principal.Id}",
            new PrincipalResponse(principal.Id.Value, principal.TenantId.Value, principal.Email, principal.Type, principal.IsActive));
    }

    private static async Task<IResult> InviteMemberAsync(Guid tenantId, InviteMemberRequest request, InviteMember useCase, CancellationToken ct)
    {
        try
        {
            var membership = await useCase.ExecuteAsync(new TenantId(tenantId), new PrincipalId(request.PrincipalId), request.Role, ct);

            return Results.Created(
                $"/tenants/{tenantId}/members/{membership.Id}",
                Map(membership));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> ListMembersAsync(Guid tenantId, ListMembersForTenant useCase, CancellationToken ct)
    {
        var memberships = await useCase.ExecuteAsync(new TenantId(tenantId), ct);

        return Results.Ok(memberships.Select(Map).ToList());
    }

    private static async Task<IResult> ActivateMemberAsync(Guid tenantId, Guid membershipId, ActivateMember useCase, CancellationToken ct)
    {
        try
        {
            var membership = await useCase.ExecuteAsync(new TenantId(tenantId), new MembershipId(membershipId), ct);

            return Results.Ok(Map(membership));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> RevokeMemberAsync(Guid tenantId, Guid membershipId, RevokeMember useCase, CancellationToken ct)
    {
        try
        {
            var membership = await useCase.ExecuteAsync(new TenantId(tenantId), new MembershipId(membershipId), ct);

            return Results.Ok(Map(membership));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static MembershipResponse Map(Membership membership) =>
        new(
            membership.Id.Value,
            membership.TenantId.Value,
            membership.PrincipalId.Value,
            membership.Role,
            membership.Status,
            membership.CreatedAt,
            membership.ActivatedAt,
            membership.RevokedAt);
}
