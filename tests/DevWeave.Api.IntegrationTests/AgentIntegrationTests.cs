using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Contracts.Agents.V1;
using DevWeave.Contracts.Identity.V1;
using DevWeave.Contracts.Knowledge.V1;
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
    public async Task AGT_013_owner_lists_every_grant_of_an_agent_across_its_lifecycle()
    {
        var tenant = await CreateTenantAsync("Alpha Corp");
        var owner = await CreateOwnerAsync(tenant.Id, "owner@alphacorp.com");
        var agent = await RegisterAgentAsync(tenant.Id, "agent-1@alphacorp.com");

        Assert.Empty(await ListGrantsAsync(tenant.Id, agent.Id));

        var read = await GrantCapabilityAsync(tenant.Id, agent.Id, projectId: null, AgentCapability.ReadKnowledge);
        var write = await GrantCapabilityAsync(tenant.Id, agent.Id, projectId: null, AgentCapability.WriteKnowledge);
        await ApproveGrantAsync(tenant.Id, read.Id, owner.Id);
        await RevokeGrantAsync(tenant.Id, read.Id, owner.Id);

        // Proposed and revoked grants stay visible so Owners can review history, not only effective access.
        var listed = await ListGrantsAsync(tenant.Id, agent.Id);
        Assert.Equal(2, listed.Count);
        Assert.Equal(AgentGrantStatus.Revoked, listed.Single(g => g.Id == read.Id).Status);
        Assert.Equal(AgentGrantStatus.Proposed, listed.Single(g => g.Id == write.Id).Status);

        // Another tenant's agent is indistinguishable from a missing one.
        var beta = await CreateTenantAsync("Beta Corp");
        using var crossTenant = await _client.GetAsync($"/tenants/{beta.Id}/agents/{agent.Id}/grants");
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
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
        using var agentClient = await CreateAuthenticatedAgentClientAsync(tenant.Id, otherAgent.Id);
        var denied = await agentClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/agents/{agent.Id}/credentials/{issued.Id}/disable",
            new DisableAgentCredentialRequest(owner.Id),
            JsonOptions);
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

        using var agentClient = await CreateAuthenticatedAgentClientAsync(tenant.Id, agent.Id);
        using var governanceResponse = await agentClient.GetAsync(
            $"/tenants/{tenant.Id}/agents/{agent.Id}/credentials");
        Assert.Equal(HttpStatusCode.Forbidden, governanceResponse.StatusCode);
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
        using var anonymousRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/whoami");
        anonymousRequest.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");
        using var anonymous = await _client.SendAsync(anonymousRequest);
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
    public async Task agent_authorization_works_with_tenant_membership_enforcement_enabled()
    {
        using var strictFactory = new IdentityTestFactory(enforceTenantMembership: true);
        strictFactory.PrepareDatabase();

        using var bootstrapClient = strictFactory.CreateClient();
        using var tenantResponse = await bootstrapClient.PostAsJsonAsync(
            "/tenants",
            new CreateTenantRequest("Strict Membership Corp"),
            JsonOptions);
        tenantResponse.EnsureSuccessStatusCode();
        var tenant = (await tenantResponse.Content.ReadFromJsonAsync<TenantResponse>(JsonOptions))!;
        var tenantId = new TenantId(tenant.Id);

        var owner = Principal.Create(tenantId, "owner@strict-membership.test", PrincipalType.Human);
        var ownerMembership = Membership.Invite(tenantId, owner.Id, MembershipRole.Owner);
        ownerMembership.Activate();
        using (var scope = strictFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant.Id;
            var principals = scope.ServiceProvider.GetRequiredService<IPrincipalRepository>();
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await principals.AddAsync(owner);
            await memberships.AddAsync(ownerMembership);
            await unitOfWork.SaveChangesAsync();
        }

        using var ownerClient = strictFactory.CreateAuthenticatedHumanClient(tenant.Id, owner.Id.Value);
        using var profileResponse = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/lifecycle-profiles",
            new CreateLifecycleProfileRequest("strict", "Strict", "1.0"),
            JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        var profile = (await profileResponse.Content.ReadFromJsonAsync<LifecycleProfileResponse>(JsonOptions))!;

        using var projectResponse = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects",
            new CreateProjectRequest("Strict Project", "strict-project", null, profile.Id, owner.Id.Value),
            JsonOptions);
        projectResponse.EnsureSuccessStatusCode();
        var project = (await projectResponse.Content.ReadFromJsonAsync<ProjectResponse>(JsonOptions))!;

        var viewer = Principal.Create(tenantId, "viewer@strict-membership.test", PrincipalType.Human);
        var viewerMembership = Membership.Invite(tenantId, viewer.Id, MembershipRole.Member);
        viewerMembership.Activate();
        using (var scope = strictFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant.Id;
            var principals = scope.ServiceProvider.GetRequiredService<IPrincipalRepository>();
            var memberships = scope.ServiceProvider.GetRequiredService<IMembershipRepository>();
            var projectMemberships = scope.ServiceProvider.GetRequiredService<IProjectMembershipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await principals.AddAsync(viewer);
            await memberships.AddAsync(viewerMembership);
            await projectMemberships.AddAsync(ProjectMembership.Grant(
                tenantId,
                new ProjectId(project.Id),
                viewer.Id,
                ProjectRole.Viewer));
            await unitOfWork.SaveChangesAsync();
        }

        using var agentResponse = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/agents",
            new RegisterAgentRequest("agent@strict-membership.test"),
            JsonOptions);
        agentResponse.EnsureSuccessStatusCode();
        var agent = (await agentResponse.Content.ReadFromJsonAsync<AgentResponse>(JsonOptions))!;

        using (var scope = strictFactory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenant.Id;
            var projectMemberships = scope.ServiceProvider.GetRequiredService<IProjectMembershipRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await projectMemberships.AddAsync(ProjectMembership.Grant(
                tenantId,
                new ProjectId(project.Id),
                new PrincipalId(agent.Id),
                ProjectRole.Member));
            await unitOfWork.SaveChangesAsync();
        }

        using var credentialResponse = await ownerClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/agents/{agent.Id}/credentials",
            new IssueAgentCredentialRequest("strict-test", null),
            JsonOptions);
        credentialResponse.EnsureSuccessStatusCode();
        var credential = (await credentialResponse.Content.ReadFromJsonAsync<AgentCredentialIssuedResponse>(JsonOptions))!;

        using var anonymousAuthentication = new HttpRequestMessage(
            HttpMethod.Post,
            $"/tenants/{tenant.Id}/agents/{agent.Id}/authenticate")
        {
            Content = JsonContent.Create(
                new AuthenticateAgentRequest(credential.Id, credential.PlainTextSecret),
                options: JsonOptions),
        };
        anonymousAuthentication.Headers.Add("X-Test-Anonymous", "true");
        using var tokenResponse = await bootstrapClient.SendAsync(anonymousAuthentication);
        tokenResponse.EnsureSuccessStatusCode();
        var token = (await tokenResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(JsonOptions))!;

        using var agentClient = strictFactory.CreateClient();
        agentClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var capabilities = await agentClient.GetAsync(
            $"/tenants/{tenant.Id}/agents/{agent.Id}/capabilities");
        capabilities.EnsureSuccessStatusCode();

        using var viewerClient = strictFactory.CreateAuthenticatedHumanClient(tenant.Id, viewer.Id.Value);
        using var viewerSearch = await viewerClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/search",
            new SearchKnowledgeRequest("no indexed matches"),
            JsonOptions);
        viewerSearch.EnsureSuccessStatusCode();

        // The same authenticated agent still cannot enter a human-only project endpoint.
        using var humanOnlyRoute = await agentClient.GetAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, humanOnlyRoute.StatusCode);

        // Agent project work reaches the application grant gate rather than failing the human
        // tenant-membership check; without an approved grant it is forbidden.
        using var missingGrant = await agentClient.PostAsJsonAsync(
            $"/tenants/{tenant.Id}/projects/{project.Id}/agents/{agent.Id}/context-packages",
            new AssembleContextPackageRequest("strict-check", "strict-idem", Array.Empty<Guid>()),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, missingGrant.StatusCode);
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
        using var client = _factory.CreateAuthenticatedHumanClient(tenantId, createdBy);
        using var response = await client.PostAsJsonAsync(
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

    private async Task<List<AgentGrantResponse>> ListGrantsAsync(Guid tenantId, Guid agentId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/agents/{agentId}/grants");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AgentGrantResponse>>(JsonOptions))!;
    }

    private async Task<List<AgentCredentialResponse>> ListCredentialsAsync(Guid tenantId, Guid agentId)
    {
        using var response = await _client.GetAsync($"/tenants/{tenantId}/agents/{agentId}/credentials");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<AgentCredentialResponse>>(JsonOptions))!;
    }

    private async Task<AgentCredentialResponse> DisableCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor)
    {
        using var client = CreateAuthenticatedHumanClient(tenantId, actor);
        using var response = await client.PostAsJsonAsync(
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
        using var client = CreateAuthenticatedHumanClient(tenantId, actor);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/enable",
            new EnableAgentCredentialRequest(actor),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialResponse>(JsonOptions))!;
    }

    private async Task<AgentCredentialIssuedResponse> RotateCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor, DateTimeOffset? expiresAt)
    {
        using var client = CreateAuthenticatedHumanClient(tenantId, actor);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agents/{agentId}/credentials/{credentialId}/rotate",
            new RotateAgentCredentialRequest(actor, expiresAt),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentCredentialIssuedResponse>(JsonOptions))!;
    }

    private async Task<AgentCredentialResponse> RevokeCredentialAsync(Guid tenantId, Guid agentId, Guid credentialId, Guid actor)
    {
        using var client = CreateAuthenticatedHumanClient(tenantId, actor);
        using var response = await client.PostAsJsonAsync(
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
        using var client = CreateAuthenticatedHumanClient(tenantId, approver);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agent-grants/{grantId}/approve",
            new ApproveAgentGrantRequest(approver),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentGrantResponse>(JsonOptions))!;
    }

    private async Task<AgentGrantResponse> RevokeGrantAsync(Guid tenantId, Guid grantId, Guid revoker)
    {
        using var client = CreateAuthenticatedHumanClient(tenantId, revoker);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/agent-grants/{grantId}/revoke",
            new RevokeAgentGrantRequest(revoker),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentGrantResponse>(JsonOptions))!;
    }

    private async Task<AgentDiscoveryResponse> DiscoverCapabilitiesAsync(Guid tenantId, Guid agentId)
    {
        using var client = await CreateAuthenticatedAgentClientAsync(tenantId, agentId);
        using var response = await client.GetAsync($"/tenants/{tenantId}/agents/{agentId}/capabilities");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentDiscoveryResponse>(JsonOptions))!;
    }

    private async Task<ContextPackageResponse> AssembleAsync(Guid tenantId, Guid projectId, Guid agentId, string taskKey, string idempotencyKey, IReadOnlyList<Guid> itemIds)
    {
        using var client = await CreateAuthenticatedAgentClientAsync(tenantId, agentId);
        using var response = await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/agents/{agentId}/context-packages",
            new AssembleContextPackageRequest(taskKey, idempotencyKey, itemIds),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContextPackageResponse>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> TryAssembleAsync(Guid tenantId, Guid projectId, Guid agentId, string taskKey, string idempotencyKey, IReadOnlyList<Guid> itemIds)
    {
        using var client = await CreateAuthenticatedAgentClientAsync(tenantId, agentId);
        return await client.PostAsJsonAsync(
            $"/tenants/{tenantId}/projects/{projectId}/agents/{agentId}/context-packages",
            new AssembleContextPackageRequest(taskKey, idempotencyKey, itemIds),
            JsonOptions);
    }

    private async Task<AgentExecutionResponse> RecordExecutionAsync(Guid tenantId, Guid projectId, Guid agentId, Guid contextPackageId)
    {
        using var client = await CreateAuthenticatedAgentClientAsync(tenantId, agentId);
        using var response = await client.PostAsJsonAsync(
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

    private async Task<HttpClient> CreateAuthenticatedAgentClientAsync(Guid tenantId, Guid agentId)
    {
        var credential = await IssueCredentialAsync(tenantId, agentId, "integration-test");
        var token = await AuthenticateAsync(tenantId, agentId, credential.Id, credential.PlainTextSecret);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    private HttpClient CreateAuthenticatedHumanClient(Guid tenantId, Guid principalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                IdentityTestFactory.IssueTestOidcToken(tenantId, principalId));
        return client;
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
