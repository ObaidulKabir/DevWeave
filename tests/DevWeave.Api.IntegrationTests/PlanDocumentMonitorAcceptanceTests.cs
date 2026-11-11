using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Contracts.Activities.V1;
using DevWeave.Contracts.Evidence.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Contracts.Schema.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Phase A control-plane readiness gate: authenticated human can plan work, document knowledge,
/// attach evidence, verify, and observe project metrics without demo fixtures.
/// </summary>
public sealed class PlanDocumentMonitorAcceptanceTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _anon;

    public PlanDocumentMonitorAcceptanceTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _anon = _factory.CreateClient();
    }

    [Fact]
    public async Task PhaseA_plan_document_evidence_verify_and_metrics_loop()
    {
        var tenant = await CreateTenantAsync("Readiness Agency");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@readiness.test");
        var client = _factory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id);

        var profile = await CreateLifecycleProfileAsync(tenant.Id, "delivery", "Delivery", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "control-plane", profile.Id, owner.Id);
        var route = $"/tenants/{tenant.Id}/projects/{project.Id}";

        await SeedPublishedSchemaAsync("design.decision", "1.0", """{"type":"object","required":["summary"],"properties":{"summary":{"type":"string"}}}""");

        // Plan
        var milestoneResponse = await client.PostAsJsonAsync($"{route}/milestones",
            new CreateMilestoneRequest("M-FOUNDATION", "Foundation", DateTimeOffset.UtcNow.AddDays(14)), Json);
        Assert.Equal(HttpStatusCode.Created, milestoneResponse.StatusCode);
        var milestone = (await milestoneResponse.Content.ReadFromJsonAsync<MilestoneResponse>(Json))!;

        var workResponse = await client.PostAsJsonAsync($"{route}/work-items",
            new CreateWorkItemRequest("CP-1", "Document checkout design", "Capture design decision",
                milestone.Id, null, null, WorkPriority.High, Array.Empty<Guid>()), Json);
        Assert.Equal(HttpStatusCode.Created, workResponse.StatusCode);
        var work = (await workResponse.Content.ReadFromJsonAsync<WorkItemResponse>(Json))!;

        // Document
        var itemResponse = await client.PostAsJsonAsync($"{route}/knowledge",
            new CreateKnowledgeItemRequest(
                "checkout-design",
                "Checkout design decision",
                Classification.Internal,
                RepresentationKind.StructuredData,
                "design.decision",
                "1.0",
                owner.Id), Json);
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
        var item = (await itemResponse.Content.ReadFromJsonAsync<KnowledgeItemResponse>(Json))!;

        var revisionResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/knowledge/{item.Id}/revisions",
            new AddRevisionRequest(0, "1.0", """{"summary":"Use idempotent payment intents"}""", owner.Id, "Initial design", null, null),
            Json);
        Assert.Equal(HttpStatusCode.Created, revisionResponse.StatusCode);
        var revision = (await revisionResponse.Content.ReadFromJsonAsync<KnowledgeRevisionResponse>(Json))!;

        // Evidence-backed verification
        var claimResponse = await client.PostAsJsonAsync($"{route}/activities",
            new RecordActivityRequest(
                ActivityType.Claim,
                owner.Id,
                "Design meets acceptance criterion",
                ActivityStatus.Completed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                Array.Empty<ActivityInputRequest>(),
                new[] { new ActivityOutputRequest(item.Id, revision.Id, 0) }),
            Json);
        Assert.Equal(HttpStatusCode.Created, claimResponse.StatusCode);
        var claim = (await claimResponse.Content.ReadFromJsonAsync<ActivityResponse>(Json))!;

        var evidenceResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/claims/{claim.Id}/evidence",
            new RecordEvidenceRequest("review-notes", "sha256:abc", null, "AC-design-1"),
            Json);
        Assert.Equal(HttpStatusCode.Created, evidenceResponse.StatusCode);
        var evidence = (await evidenceResponse.Content.ReadFromJsonAsync<EvidenceResponse>(Json))!;

        var reviewResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/evidence/{evidence.Id}/review",
            new ReviewEvidenceRequest(EvidenceReviewState.Accepted, owner.Id),
            Json);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);

        // A review must name the exact revision it read; a decision on any other revision is refused.
        var staleApprove = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/knowledge/{item.Id}/approve",
            new ReviewRevisionRequest(Guid.NewGuid(), null),
            Json);
        Assert.Equal(HttpStatusCode.Conflict, staleApprove.StatusCode);

        var approveResponse = await client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/knowledge/{item.Id}/approve",
            new ReviewRevisionRequest(revision.Id, "Matches the acceptance criterion"),
            Json);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // The approval is traceable: who, which revision, when, and why.
        var reviews = (await client.GetFromJsonAsync<List<ReviewDecisionResponse>>(
            $"/tenants/{tenant.Id}/knowledge/{item.Id}/reviews", Json))!;
        var decision = Assert.Single(reviews);
        Assert.Equal("Approved", decision.Decision);
        Assert.Equal(revision.Id, decision.RevisionId);
        Assert.Equal(owner.Id, decision.ReviewerId);
        Assert.Equal("Matches the acceptance criterion", decision.Comment);

        var verifyResponse = await client.PostAsync($"/tenants/{tenant.Id}/knowledge/{item.Id}/verify", null);
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        var verified = (await verifyResponse.Content.ReadFromJsonAsync<KnowledgeItemResponse>(Json))!;
        Assert.Equal(VerificationState.Verified, verified.VerificationState);

        // Progress monitoring
        var reportWork = await client.PostAsJsonAsync(
            $"{route}/work-items/{work.Id}/report",
            new ReportWorkRequest(ReportedExecutionState.Completed, "design documented", work.Version),
            Json);
        Assert.Equal(HttpStatusCode.OK, reportWork.StatusCode);

        var activities = await client.GetFromJsonAsync<ActivityResponse[]>($"{route}/activities", Json);
        Assert.NotNull(activities);
        Assert.Contains(activities, a => a.Id == claim.Id);

        var metricsResponse = await client.GetAsync($"{route}/metrics");
        Assert.Equal(HttpStatusCode.OK, metricsResponse.StatusCode);
        var metrics = (await metricsResponse.Content.ReadFromJsonAsync<List<MetricView>>(Json))!;
        Assert.Contains(metrics, m => m.Key == "knowledge.total_items" && m.Value >= 1);
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _anon.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(Json))!;
    }

    private async Task<PrincipalResponse> CreateOwnerAsync(Guid tenantId, string email)
    {
        using var principalResponse = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, PrincipalType.Human),
            Json);
        principalResponse.EnsureSuccessStatusCode();
        var owner = (await principalResponse.Content.ReadFromJsonAsync<PrincipalResponse>(Json))!;

        using var invite = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(owner.Id, MembershipRole.Owner),
            Json);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(Json))!;

        using var activate = await _anon.PostAsync($"/tenants/{tenantId}/members/{membership.Id}/activate", null);
        activate.EnsureSuccessStatusCode();
        return owner;
    }

    private async Task<LifecycleProfileResponse> CreateLifecycleProfileAsync(Guid tenantId, string key, string name, string version)
    {
        using var response = await _anon.PostAsJsonAsync(
            $"/tenants/{tenantId}/lifecycle-profiles",
            new CreateLifecycleProfileRequest(key, name, version),
            Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LifecycleProfileResponse>(Json))!;
    }

    private async Task<ProjectResponse> CreateProjectAsync(Guid tenantId, string key, Guid lifecycleProfileId, Guid createdBy)
    {
        var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("Control Plane", key, null, lifecycleProfileId, createdBy),
            Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(Json))!;
    }

    private async Task SeedPublishedSchemaAsync(string @namespace, string version, string document)
    {
        using var list = await _anon.GetAsync("/schemas");
        list.EnsureSuccessStatusCode();
        var schemas = await list.Content.ReadFromJsonAsync<List<SchemaResponse>>(Json);
        var existing = schemas?.FirstOrDefault(s => s.Namespace == @namespace && s.Version == version);
        if (existing is not null)
        {
            if (existing.Status != SchemaStatus.Published)
            {
                using var publishExisting = await _anon.PostAsync($"/schemas/{existing.Id}/publish", null);
                publishExisting.EnsureSuccessStatusCode();
            }
            return;
        }

        using var register = await _anon.PostAsJsonAsync(
            "/schemas",
            new RegisterSchemaRequest(@namespace, version, "platform", "Phase A schema", document),
            Json);
        register.EnsureSuccessStatusCode();
        var created = (await register.Content.ReadFromJsonAsync<SchemaResponse>(Json))!;
        using var publish = await _anon.PostAsync($"/schemas/{created.Id}/publish", null);
        publish.EnsureSuccessStatusCode();
    }
}
