using DevWeave.Application.Abstractions;
using DevWeave.Application.Evidence;
using DevWeave.Contracts.Evidence.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Evidence REST surface for Slice 7 (SRS PRO-006). Evidence is recorded against a claim activity,
/// carries origin/time/type/integrity/related-criterion metadata, and transitions through a governed
/// review state. A claim remains unverified until at least one evidence record is accepted, which is
/// evaluated by the verification route. Every read and write is tenant-scoped (deny-by-default).
/// </summary>
public static class EvidenceEndpoints
{
    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}");

        group.MapPost("/claims/{claimId:guid}/evidence", RecordEvidenceAsync);
        group.MapPost("/evidence/{evidenceId:guid}/review", ReviewEvidenceAsync);
        group.MapGet("/claims/{claimId:guid}/evidence", ListEvidenceForClaimAsync);
        group.MapGet("/projects/{projectId:guid}/evidence", ListEvidenceForProjectAsync);
        group.MapGet("/claims/{claimId:guid}/verification", GetClaimVerificationAsync);

        return endpoints;
    }

    private static async Task<IResult> RecordEvidenceAsync(
        Guid tenantId,
        Guid claimId,
        RecordEvidenceRequest request,
        RecordEvidence useCase,
        CancellationToken ct)
    {
        try
        {
            var record = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ActivityId(claimId),
                request.Type,
                request.IntegrityHash,
                request.RelatedArtifactId is { } artifactId ? new ArtifactId(artifactId) : null,
                request.RelatedCriterion,
                ct);

            return Results.Created($"/tenants/{tenantId}/evidence/{record.Id}", Map(record));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new EvidenceError("claim-required", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new EvidenceError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> ReviewEvidenceAsync(
        Guid tenantId,
        Guid evidenceId,
        ReviewEvidenceRequest request,
        ReviewEvidence useCase,
        CancellationToken ct)
    {
        try
        {
            var record = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new EvidenceId(evidenceId),
                request.State,
                new PrincipalId(request.Reviewer),
                ct);

            return Results.Ok(Map(record));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ConflictException ex)
        {
            return Results.Conflict(new EvidenceError("invalid-review-transition", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new EvidenceError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> ListEvidenceForClaimAsync(
        Guid tenantId,
        Guid claimId,
        ListEvidenceForClaim useCase,
        CancellationToken ct)
    {
        var records = await useCase.ExecuteAsync(new TenantId(tenantId), new ActivityId(claimId), ct);

        return Results.Ok(records.Select(Map).ToList());
    }

    private static async Task<IResult> ListEvidenceForProjectAsync(
        Guid tenantId,
        Guid projectId,
        ListEvidenceForProject useCase,
        CancellationToken ct)
    {
        var records = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return Results.Ok(records.Select(Map).ToList());
    }

    private static async Task<IResult> GetClaimVerificationAsync(
        Guid tenantId,
        Guid claimId,
        GetClaimVerification useCase,
        CancellationToken ct)
    {
        try
        {
            var result = await useCase.ExecuteAsync(new TenantId(tenantId), new ActivityId(claimId), ct);

            return Results.Ok(new ClaimVerificationResponse(
                result.ClaimId,
                result.ProjectId.Value,
                result.IsVerified,
                result.Evidence.Select(Map).ToList()));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static EvidenceResponse Map(EvidenceRecord evidence) =>
        new(
            evidence.Id.Value,
            evidence.TenantId.Value,
            evidence.ProjectId.Value,
            evidence.OriginActivityId.Value,
            evidence.RecordedAt,
            evidence.Type,
            evidence.IntegrityHash,
            evidence.RelatedArtifactId?.Value,
            evidence.RelatedCriterion,
            evidence.ReviewState,
            evidence.ReviewedBy?.Value,
            evidence.ReviewedAt);
}