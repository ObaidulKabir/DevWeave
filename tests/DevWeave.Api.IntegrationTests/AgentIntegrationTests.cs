using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Agents.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Lifecycle.V1;
using DevWeave.Contracts.Projects.V1;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests;

/// <summary>
/// Proves the Slice 8 agent surface end-to-end against a real database: machine-principal registration
/// (AGT-001), one-time hashed credential issuance (AGT-002), governed capability grants (AGT-002/AGT-003/
/// AGT-013), authorization-first context assembly with source-code exclusion and idempotency
/// (AGT-004/AGT-010), execution provenance (AGT-005), and cross-tenant negative disclosure.
/// </summary>
public sealed class AgentIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly IdentityTestFactory _factory;
    private readonly HttpClient _client;

    public AgentIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task AGT_001_002_003_013_agent_registration_credential_and_grant_lifecycle_end_to_end()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var approver = await CreateOwnerAsync(tenant.Id, "owner@alphacorp.com");

        var agent = await RegisterAgentAsync(tenant.Id, "agent-1@alphacorp.com");
        Assert.Equal(PrincipalType.AiAgent, agent.Type);
        Assert.True(agent.IsActive);

        // One-time secret issuance returns plaintext and persists only the hash (AGT-002).
        var credential = await IssueCredentialAsync(tenant.Id, agent.Id, "deploy-key");
        Assert.False(string.IsNullOrWhiteSpace(credential.PlainTextSecret));
        Assert.NotEqual(credential.PlainTextSecret, credential.Id.ToString());

        // A capability grant is proposed then must be approved before it is effective (AGT-013).
        var grant = await GrantCapabilityAsync(tenant.Id, agent.Id, projectId: null, AgentCapability.ReadKnowledge);
        Assert.Equal(AgentGrantStatus.Proposed, grant.Status);

        Assert.Empty((await DiscoverCapabilitiesAsync(tenant.Id, agent.Id)).Grants);

        var approved = await ApproveGrantAsync(tenant.Id, grant.Id, approver.Id);
        Assert.Equal(AgentGrantStatus.Approved, approved.Status);

        var discovered = await DiscoverCapabilitiesAsync(tenant.Id, agent.Id);
        var active = Assert.Single(discovered.Grants);
        Assert.Equal(AgentCapability.ReadKnowledge, active.Capability);

        // Revocation is terminal and removes the grant from the effective surface (AGT-013).
        var revoked = await RevokeGrantAsync(tenant.Id, grant.Id, approver.Id);
        Assert.Equal(AgentGrantStatus.Revoked, revoked.Status);
        Assert.Empty((await DiscoverCapabilitiesAsync(tenant.Id, agent.Id)).Grants);
    }

    [Fact]
    public async Task AGT_002_credential_lifecycle_disable_enable_rotate_revoke_end_to_end()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@alphacorp.com");
        var agent = await RegisterAgentAsync(tenant.Id, "agent-1@alphacorp.com");
        var otherAgent = await RegisterAgentAsync(tenant.Id, "agent-2@alphacorp.com");

        var issued = await IssueCredentialAsync(tenant.Id, agent.Id, "deploy-key");
        Assert.Equal(AgentCredentialStatus.Active, issued.Status);

        var listed = await ListCredentialsAsync(tenant.Id, agent.Id);
        Assert.Single(listed);

        // A non-human principal cannot govern credentials (AGT-013).
        var denied = await TryDisableCredentialAsync(tenant.Id, agent.Id, issued.Id, otherAgent.Id);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // Disable is a reversible suspension.
        var disabled = await DisableCredentialAsync(tenant.Id, agent.Id, issued.Id, owner.Id);
        Assert.Equal(AgentCredentialStatus.Disabled, disabled.Status);

        var enabled = await EnableCredentialAsync(tenant.Id, agent.Id, issued.Id, owner.Id);
        Assert.Equal(AgentCredentialStatus.Active, enabled.Status);

        // Rotation revokes the outgoing credential and returns a fresh secret exactly once.
        var rotated = await RotateCredentialAsync(tenant.Id, agent.Id, issued.Id, owner.Id, expiresAt: null);
        Assert.Equal(AgentCredentialStatus.Active, rotated.Status);
        Assert.NotEqual(issued.Id, rotated.Id);
        Assert.False(string.IsNullOrWhiteSpace(rotated.PlainTextSecret));

        var revoked = await RevokeCredentialAsync(tenant.Id, agent.Id, rotated.Id, owner.Id);
        Assert.Equal(AgentCredentialStatus.Revoked, revoked.Status);

        // Both the rotated-out original and the revoked replacement remain listed.
        var all = await ListCredentialsAsync(tenant.Id, agent.Id);
        Assert.Equal(2, all.Count);
        Assert.Contains(all, c => c.Status == AgentCredentialStatus.Revoked);
    }

    [Fact]
    public async Task AGT_004_010_context_assembly_is_authorized_excludes_source_code_and_is_idempotent()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var approver = await CreateOwnerAsync(tenant.Id, "owner@alphacorp.com");
        var profile = await CreateLifecycleProfileAsync(tenant.Id, "agile", "Agile", "1.0");
        var project = await CreateProjectAsync(tenant.Id, "shop-api", profile.Id, approver.Id);

        var agent = await RegisterAgentAsync(tenant.Id, "agent-1@alphacorp.com");
        var credential = await IssueCredentialAsync(tenant.Id, agent.Id, "deploy-key");
        await AddProjectMembershipAsync(tenant.Id, project.Id, agent.Id, ProjectRole.Member);

        var textItemId = await SeedKnowledgeItemAsync(tenant.Id, project.Id, "architecture", RepresentationKind.Text, "{\"diagram\":\"layers\"}");
        var sourceItemId = await SeedKnowledgeItemAsync(tenant.Id, project.Id, "app.js", RepresentationKind.SourceCode, "{\"source\":\"console.log('secret-marker');\"}");

        // Authorization-first (AGT-004): with no effective grant the request is denied outright.
        var denied = await TryAssembleAsync(tenant.Id, project.Id, agent.Id, "review", "idem-1", new[] { textItemId });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var grant = await GrantCapabilityAsync(tenant.Id, agent.Id, project.Id, AgentCapability.ReadKnowledge);
        await ApproveGrantAsync(tenant.Id, grant.Id, approver.Id);

        var assembled = await AssembleAsync(tenant.Id, project.Id, agent.Id, "review", "idem-1", new[] { textItemId, sourceItemId });
        // Source-code-bearing content is excluded (AGT-004).
        var entry = Assert.Single(assembled.Entries);
        Assert.Equal(textItemId, entry.ItemId);
        Assert.Equal(0, entry.Position);

        // Idempotency (AGT-010): a retry with the same key returns the same package.
        var retried = await AssembleAsync(tenant.Id, project.Id, agent.Id, "review", "idem-1", new[] { textItemId, sourceItemId });
        Assert.Equal(assembled.Id, retried.Id);

        // Execution provenance (AGT-005): the package is recorded as a correlated activity.
        var execution = await RecordExecutionAsync(tenant.Id, project.Id, agent.Id, assembled.Id);
        Assert.Equal(assembled.Id, execution.ContextPackageId);

        using var scope = _factory.Services.CreateScope();
        var activities = scope.ServiceProvider.GetRequiredService<IActivityRepository>();
        var recorded = await activities.GetByIdAsync(new TenantId(tenant.Id), new ActivityId(execution.ActivityId));
        Assert.NotNull(recorded);
        Assert.Equal(assembled.Id.ToString(), recorded!.CorrelationId);
        Assert.Equal(textItemId, Assert.Single(recorded.Inputs).ItemId.Value);
    }

    [Fact]
    public async Task AGT_009_core_agent_capabilities_are_exposed_over_rest()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var agent = await RegisterAgentAsync(tenant.Id, "agent-1@alphacorp.com");

        // The agent domain is reachable over protocol-neutral REST (AGT-009). Registration persisted a
        // machine principal, and capability discovery answers with an empty effective surface when there
        // are no grants rather than failing through an error path.
        Assert.Equal(PrincipalType.AiAgent, agent.Type);

        var capabilities = await DiscoverCapabilitiesAsync(tenant.Id, agent.Id);
        Assert.Empty(capabilities.Grants);
    }

    [Fact]
    public async Task TEN_007_bearer_token_authentication_end_to_end()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var agent = await RegisterAgentAsync(tenant.Id, "agent-1@alphacorp.com");
        var credential = await IssueCredentialAsync(tenant.Id, agent.Id, "deploy-key");

        // A bad secret is rejected without a token.
        var denied = await _client.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/agents/{agent.Id}/authenticate",
            new AuthenticateAgentRequest(credential.Id, "wrong-secret"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        // A valid secret mints a bearer token.
        var token = await AuthenticateAsync(tenant.Id, agent.Id, credential.Id, credential.PlainTextSecret);
        Assert.Equal("Bearer", token.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        // The verified-identity endpoint requires a bearer token.
        using var anonymous = await _client.GetAsync("/auth/whoami");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        using var ok = new HttpRequestMessage(HttpMethod.Get, "/auth/whoami");
        ok.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var whoami = await _client.SendAsync(ok);
        whoami.EnsureSuccessStatusCode();
        var body = await whoami.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(tenant.Id.ToString(), body.GetProperty("tenantId").GetString());
        Assert.Equal(agent.Id.ToString(), body.GetProperty("principalId").GetString());
        Assert.Equal(PrincipalType.AiAgent.ToString(), body.GetProperty("principalType").GetString());
    }

    [Fact]
    public async Task agent_records_never_disclose_cross_tenant_data()
    {
        var alpha = await CreateTenantAsync("Alpha Corp");
        var beta = await CreateTenantAsync("Beta Corp");

        var agentB = await RegisterAgentAsync(beta.Id, "agent@betacorp.com");
        var approverB = await CreateOwnerAsync(beta.Id, "owner@betacorp.com");

        var credentialB = await IssueCredentialAsync(beta.Id, agentB.Id, "deploy-key");
        var grantB = await GrantCapabilityAsync(beta.Id, agentB.Id, null, AgentCapability.ReadKnowledge);
        await ApproveGrantAsync(beta.Id, grantB.Id, approverB.Id);

        var profileB = await CreateLifecycleProfileAsync(beta.Id, "agile", "Agile", "1.0");
        var projectB = await CreateProjectAsync(beta.Id, "shop-api", profileB.Id, approverB.Id);
        await AddProjectMembershipAsync(beta.Id, projectB.Id, agentB.Id, ProjectRole.Member);
        var itemB = await SeedKnowledgeItemAsync(beta.Id, projectB.Id, "architecture", RepresentationKind.Text, "{\"diagram\":\"layers\"}");
        var packageB = await AssembleAsync(beta.Id, projectB.Id, agentB.Id, "review", "idem-b", new[] { itemB });

        using var scope = _factory.Services.CreateScope();
        var grants = scope.ServiceProvider.GetRequiredService<IAgentGrantRepository>();
        var credentials = scope.ServiceProvider.GetRequiredService<IAgentCredentialRepository>();
        var packages = scope.ServiceProvider.GetRequiredService<IContextPackageRepository>();

        // Cross-tenant reads are indistinguishable from a missing record (deny-by-default).
        Assert.Null(await grants.GetByIdAsync(new TenantId(alpha.Id), new AgentGrantId(grantB.Id)));
        Assert.Empty(await grants.ListForAgentAsync(new TenantId(alpha.Id), new PrincipalId(agentB.Id)));

        Assert.Null(await credentials.GetByIdAsync(new TenantId(alpha.Id), new AgentCredentialId(credentialB.Id)));

        Assert.Null(await packages.GetByIdAsync(new TenantId(alpha.Id), new ContextPackageId(packageB.Id)));
        Assert.Null(await packages.FindByIdempotencyKeyAsync(new TenantId(alpha.Id), new ProjectId(projectB.Id), new PrincipalId(agentB.Id), "idem-b"));
    }

    private async Task<TenantResponse> CreateTenantAsync(string name)
    {
        using var response = await _client.PostAsJsonAsync("/tenants", new CreateTenantRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;
    }

    private async Task<PrincipalResponse> CreatePrincipalAsync(Guid tenantId, string email, PrincipalType type)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/principals",
            new CreatePrincipalRequest(email, type),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PrincipalResponse>(JsonOptions))!;
    }

    private async Task<PrincipalResponse> CreateOwnerAsync(Guid tenantId, string email)
    {
        var owner = await CreatePrincipalAsync(tenantId, email, PrincipalType.Human);

        // Seeding an active Owner membership grants approval authority over agent grants (AGT-013).
        using var invite = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/members",
            new InviteMemberRequest(owner.Id, MembershipRole.Owner),
            JsonOptions);
        invite.EnsureSuccessStatusCode();
        var membership = (await invite.Content.ReadFromJsonAsync<MembershipResponse>(JsonOptions))!;

        using var activate = await _client.PostAsync(
            $"/tenants/{tenantId}/members/{membership.Id}/activate",
            content: null);
        activate.EnsureSuccessStatusCode();

        return owner;
    }

    private async Task<LifecycleProfileResponse> CreateLifecycleProfileAsync(Guid tenantId, string key, string name, string version)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/lifecycle-profiles",
            new CreateLifecycleProfileRequest(key, name, version),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;
    }

    private async Task<ProjectResponse> CreateProjectAsync(Guid tenantId, string key, Guid lifecycleProfileId, Guid createdBy)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects",
            new CreateProjectRequest("Shop API", key, null, lifecycleProfileId, createdBy),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;
    }

    private async Task<AgentResponse> RegisterAgentAsync(Guid tenantId, string email)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents",
            new RegisterAgentRequest(email),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions))!;
    }

    private async Task<AgentCredentialIssuedResponse> IssueCredentialAsync(Guid tenantId, Guid agentId, string label)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials",
            new IssueAgentCredentialRequest(label, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialIssuedResponse>(JsonOptions))!;
    }

    private async Task<AccessTokenResponse> AuthenticateAsync(Guid tenantId, Guid agentId, Guid credentialId, string secret)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/authenticate",
            new AuthenticateAgentRequest(credentialId, secret),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!;
    }

    private async Task<List<AgentCredentialResponse>> ListCredentialsAsync(Guid tenantId, Guid agentId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/agents/{agentId}/credentials");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AgentCredentialResponse>>(JsonOptions))!;
    }

    private async Task<AgentCredentialResponse> DisableCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/disable",
            new DisableAgentCredentialRequest(actor),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryDisableCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor)
    {
        return await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/disable",
            new DisableAgentCredentialRequest(actor),
            JsonOptions);
    }

    private async Task<AgentCredentialResponse> EnableCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/enable",
            new EnableAgentCredentialRequest(actor),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialResponse>(JsonOptions))!;
    }

    private async Task<AgentCredentialIssuedResponse> RotateCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor, DateTimeOffset? expiresAt)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/rotate",
            new RotateAgentCredentialRequest(actor, expiresAt),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialIssuedResponse>(JsonOptions))!;
    }

    private async Task<AgentCredentialResponse> RevokeCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/revoke",
            new RevokeAgentCredentialRequest(actor),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialResponse>(JsonOptions))!;
    }

    private async Task<ProjectMembershipResponse> AddProjectMembershipAsync(Guid tenantId, Guid projectId, Guid principalId, ProjectRole role)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/members",
            new GrantProjectMembershipRequest(principalId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectMembershipResponse>(JsonOptions))!;
    }

    private async Task<AgentGrantResponse> GrantCapabilityAsync(Guid tenantId, Guid agentId, Guid? projectId, AgentCapability capability)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agent-grants",
            new GrantAgentCapabilityRequest(agentId, projectId, capability, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentGrantResponse>(JsonOptions))!;
    }

    private async Task<AgentGrantResponse> ApproveGrantAsync(Guid tenantId, Guid grantId, Guid approver)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agent-grants/{grantId}/approve",
            new ApproveAgentGrantRequest(approver),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentGrantResponse>(JsonOptions))!;
    }

    private async Task<AgentGrantResponse> RevokeGrantAsync(Guid tenantId, Guid grantId, Guid revoker)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agent-grants/{grantId}/revoke",
            new RevokeAgentGrantRequest(revoker),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentGrantResponse>(JsonOptions))!;
    }

    private async Task<AgentDiscoveryResponse> DiscoverCapabilitiesAsync(Guid tenantId, Guid agentId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/agents/{agentId}/capabilities");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentDiscoveryResponse>(JsonOptions))!;
    }

    private async Task<ContextPackageResponse> AssembleAsync(Guid tenantId, Guid projectId, Guid agentId, string taskKey, string idempotencyKey, IReadOnlyList<Guid> itemIds)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/agents/{agentId}/context-packages",
            new AssembleContextPackageRequest(taskKey, idempotencyKey, itemIds),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContextPackageResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryAssembleAsync(Guid tenantId, Guid projectId, Guid agentId, string taskKey, string idempotencyKey, IReadOnlyList<Guid> itemIds)
    {
        return await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/agents/{agentId}/context-packages",
            new AssembleContextPackageRequest(taskKey, idempotencyKey, itemIds),
            JsonOptions);
    }

    private async Task<AgentExecutionResponse> RecordExecutionAsync(Guid tenantId, Guid projectId, Guid agentId, Guid contextPackageId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/agent-executions",
            new RecordAgentExecutionRequest(
                agentId,
                contextPackageId,
                "summarize architecture",
                ActivityStatus.Completed,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentExecutionResponse>(JsonOptions))!;
    }

    private async Task<Guid> SeedKnowledgeItemAsync(Guid tenantId, Guid projectId, string key, RepresentationKind representation, string content)
    {
        using var scope = _factory.Services.CreateScope();
        var items = scope.ServiceProvider.GetRequiredService<IKnowledgeItemRepository>();
        var revisions = scope.ServiceProvider.GetRequiredService<IKnowledgeRevisionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var item = KnowledgeItem.Create(
            new TenantId(tenantId),
            new ProjectId(projectId),
            key,
            key + " title",
            Classification.Internal,
            representation,
            "architecture.mmd",
            "1.0",
            PrincipalId.New());

        var revision = item.AddRevision(
            "1.0",
            content,
            PrincipalId.New(),
            "Baseline",
            sourceReference: null,
            supersededRevisionId: null,
            item.CurrentRevisionNumber);

        await items.AddAsync(item);
        await revisions.AddAsync(revision);
        await unitOfWork.SaveChangesAsync();

        return item.Id.Value;
    }
}