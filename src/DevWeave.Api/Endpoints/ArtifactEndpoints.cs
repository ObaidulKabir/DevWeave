using DevWeave.Application.Abstractions;
using DevWeave.Application.Artifacts;
using DevWeave.Contracts.Artifacts.V1;
using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.AspNetCore.Mvc;

namespace DevWeave.Api.Endpoints;

/// <summary>
/// Artifact REST surface for Slice 7 (SRS ART-001..ART-004, ART-006, ART-008). Bytes are uploaded as
/// multipart form data and persisted to object storage while canonical metadata lands in the database
/// (ART-001). The surface is append-only for versions (ART-003): no update or delete route exists, and
/// a replacement upload produces a new version. Downloads are granted only as short-lived presigned URLs
/// (ART-004) — never a permanent public URL. Rejected content yields 422 before persistence, quarantined
/// content yields 422 after persistence, and the checksum is revalidable through the verify route
/// (ART-006). All reads and writes are tenant-scoped (deny-by-default).
/// </summary>
public static class ArtifactEndpoints
{
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/tenants/{tenantId:guid}");

        // Multipart form upload triggers ASP.NET Core's automatic anti-forgery requirement for
        // IFormCollection-bound endpoints. Authentication is out of scope for this slice (the tenant
        // identifier travels in the route), so this specific route opts out of anti-forgery validation.
        group.MapPost("/projects/{projectId:guid}/artifacts", UploadArtifactAsync)
            .DisableAntiforgery();
        group.MapGet("/projects/{projectId:guid}/artifacts", ListArtifactsForProjectAsync);
        group.MapGet("/projects/{projectId:guid}/artifacts/versions", ListArtifactVersionsAsync);
        group.MapGet("/artifacts/{artifactId:guid}", GetArtifactAsync);
        group.MapGet("/artifacts/{artifactId:guid}/download", GetArtifactDownloadUrlAsync);
        group.MapGet("/artifacts/{artifactId:guid}/verify", VerifyArtifactAsync);

        return endpoints;
    }

    private static async Task<IResult> UploadArtifactAsync(
        Guid tenantId,
        Guid projectId,
        [FromForm] IFormCollection form,
        UploadArtifact useCase,
        CancellationToken ct)
    {
        var file = form.Files.GetFile("file")
            ?? form.Files.FirstOrDefault();

        if (file is null)
            return Results.BadRequest(new ArtifactError("file-required", "A file is required."));

        var key = form["key"].ToString();
        var fileName = form["fileName"].ToString();
        var mediaType = form["mediaType"].ToString();

        if (!Enum.TryParse<Classification>(form["classification"].ToString(), ignoreCase: true, out var classification))
            return Results.BadRequest(new ArtifactError("invalid-classification", "A valid classification is required."));

        if (!Guid.TryParse(form["creator"].ToString(), out var creatorGuid))
            return Results.BadRequest(new ArtifactError("invalid-creator", "A valid creator is required."));

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        var content = buffer.ToArray();

        try
        {
            var artifact = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ProjectId(projectId),
                key,
                fileName,
                mediaType,
                content,
                classification,
                new PrincipalId(creatorGuid),
                ct);

            return Results.Created($"/tenants/{tenantId}/artifacts/{artifact.Id}", Map(artifact));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (ProhibitedContentException ex)
        {
            return Results.UnprocessableEntity(new ArtifactError("prohibited-content", ex.Message));
        }
        catch (QuarantinedContentException ex)
        {
            return Results.UnprocessableEntity(new ArtifactError("quarantined-content", ex.Message));
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new ArtifactError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> ListArtifactsForProjectAsync(
        Guid tenantId,
        Guid projectId,
        ListArtifactsForProject useCase,
        CancellationToken ct)
    {
        var artifacts = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), ct);

        return Results.Ok(artifacts.Select(Map).ToList());
    }

    private static async Task<IResult> ListArtifactVersionsAsync(
        Guid tenantId,
        Guid projectId,
        string key,
        ListArtifactVersions useCase,
        CancellationToken ct)
    {
        var artifacts = await useCase.ExecuteAsync(new TenantId(tenantId), new ProjectId(projectId), key, ct);

        return Results.Ok(artifacts.Select(Map).ToList());
    }

    private static async Task<IResult> GetArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        GetArtifact useCase,
        CancellationToken ct)
    {
        var artifact = await useCase.ExecuteAsync(new TenantId(tenantId), new ArtifactId(artifactId), ct);

        return artifact is null
            ? Results.NotFound()
            : Results.Ok(Map(artifact));
    }

    private static async Task<IResult> GetArtifactDownloadUrlAsync(
        Guid tenantId,
        Guid artifactId,
        TimeSpan? expiry,
        GetArtifactDownloadUrl useCase,
        CancellationToken ct)
    {
        try
        {
            var download = await useCase.ExecuteAsync(
                new TenantId(tenantId),
                new ArtifactId(artifactId),
                expiry,
                ct);

            return Results.Ok(new ArtifactDownloadResponse(download.Url, download.ExpiresAt));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (UnauthorizedTenantException ex)
        {
            return Results.Json(new ArtifactError("forbidden", ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static async Task<IResult> VerifyArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        VerifyArtifact useCase,
        CancellationToken ct)
    {
        try
        {
            var result = await useCase.ExecuteAsync(new TenantId(tenantId), new ArtifactId(artifactId), ct);

            return Results.Ok(new ArtifactVerificationResponse(
                result.ArtifactId,
                result.ExpectedChecksum,
                result.ActualChecksum,
                result.ChecksumAlgorithm,
                result.Match));
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
    }

    private static ArtifactResponse Map(Artifact artifact) =>
        new(
            artifact.Id.Value,
            artifact.TenantId.Value,
            artifact.ProjectId.Value,
            artifact.Key,
            artifact.Version,
            artifact.MediaType,
            artifact.SizeInBytes,
            artifact.Checksum,
            artifact.ChecksumAlgorithm,
            artifact.ObjectKey,
            artifact.Classification,
            artifact.Creator.Value,
            artifact.CreatedAt,
            artifact.Status);
}