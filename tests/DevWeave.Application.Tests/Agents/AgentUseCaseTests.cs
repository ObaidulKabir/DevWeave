using DevWeave.Application.Abstractions;
using DevWeave.Application.Agents;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using DevWeave.Domain.Schema;

namespace DevWeave.Application.Tests.Agents;

/// <summary>
/// Exercises the Slice 8 agent use cases against in-memory fakes, focusing on flow correctness: agent
/// registration as a machine principal (AGT-001), one-time credential issuance with hashing (AGT-002),
/// governed grant lifecycle and discovery (AGT-002/AGT-003/AGT-013), authorization-first context
/// assembly with source-code exclusion and idempotency (AGT-004/AGT-010), and revision-pinned execution
/// provenance (AGT-005).
/// </summary>
public sealed class AgentUseCaseTests
{
    private static readonly string[] CodeQuarantineReasons = ["code-bearing"];

    [Fact]
    public async Task AGT_001_register_agent_creates_a_machine_principal()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");

        var agent = await new RegisterAgent(fakes.Principals, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, "agent-1@alphacorp.com");

        Assert.Equal(tenant.Id, agent.TenantId);
        Assert.Equal(PrincipalType.AiAgent, agent.Type);
        Assert.True(agent.IsActive);
        Assert.Single(fakes.Principals.Principals);
    }

    [Fact]
    public async Task AGT_002_issue_credential_hashes_and_returns_plaintext_once()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");

        var issued = await new IssueAgentCredential(fakes.Credentials, fakes.Principals, fakes.SecretHasher, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, agent.Id, "deploy-key", expiresAt: null);

        Assert.Equal("hash:" + issued.PlainTextSecret, issued.Credential.SecretHash);
        Assert.Equal(agent.Id, issued.Credential.AgentId);
        Assert.Single(fakes.Credentials.Credentials);
    }

    [Fact]
    public async Task AGT_002_issue_credential_rejects_a_non_agent_principal()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var human = Principal.Create(tenant.Id, "human@alphacorp.com", PrincipalType.Human);
        await fakes.Principals.AddAsync(human);

        var useCase = new IssueAgentCredential(fakes.Credentials, fakes.Principals, fakes.SecretHasher, fakes.UnitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(tenant.Id, human.Id, "key", null));
    }

    [Fact]
    public async Task AGT_002_revoke_credential_requires_authority_and_is_terminal()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var owner = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");
        var otherAgent = await SeedAgentAsync(fakes, tenant.Id, "agent-2@alphacorp.com");

        var issued = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");
        var revoke = new RevokeAgentCredential(fakes.Credentials, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        // An agent principal is not a human authority and cannot revoke credentials (AGT-013).
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            revoke.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, otherAgent.Id));

        var revoked = await revoke.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, owner.Id);
        Assert.Equal(AgentCredentialStatus.Revoked, revoked.Status);

        // Revocation is terminal.
        await Assert.ThrowsAsync<ConflictException>(() =>
            revoke.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, owner.Id));
    }

    [Fact]
    public async Task AGT_002_disable_then_enable_credential_is_reversible()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var owner = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");

        var issued = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");
        var verify = new VerifyAgentCredential(fakes.Credentials, fakes.SecretHasher);
        Assert.True(await verify.ExecuteAsync(tenant.Id, issued.Credential.Id, issued.PlainTextSecret));

        var disable = new DisableAgentCredential(fakes.Credentials, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var disabled = await disable.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, owner.Id);
        Assert.Equal(AgentCredentialStatus.Disabled, disabled.Status);

        // A disabled credential is no longer usable.
        Assert.False(await verify.ExecuteAsync(tenant.Id, issued.Credential.Id, issued.PlainTextSecret));

        var enable = new EnableAgentCredential(fakes.Credentials, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var active = await enable.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, owner.Id);
        Assert.Equal(AgentCredentialStatus.Active, active.Status);
        Assert.True(await verify.ExecuteAsync(tenant.Id, issued.Credential.Id, issued.PlainTextSecret));
    }

    [Fact]
    public async Task AGT_002_rotate_credential_revokes_old_and_issues_new()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var owner = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");

        var issued = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");

        var rotate = new RotateAgentCredential(fakes.Credentials, fakes.Principals, fakes.Memberships, fakes.SecretHasher, fakes.UnitOfWork);
        var rotated = await rotate.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, owner.Id, expiresAt: null);

        Assert.NotEqual(issued.Credential.Id, rotated.Credential.Id);
        Assert.NotEqual(issued.PlainTextSecret, rotated.PlainTextSecret);
        Assert.Equal(AgentCredentialStatus.Revoked, issued.Credential.Status);
        Assert.Equal(AgentCredentialStatus.Active, rotated.Credential.Status);

        // Both the revoked outgoing credential and its active replacement remain listed.
        var list = await new ListAgentCredentials(fakes.Credentials, fakes.Principals).ExecuteAsync(tenant.Id, agent.Id);
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task AGT_002_verify_rejects_wrong_secret_and_expired_credentials()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");

        var verify = new VerifyAgentCredential(fakes.Credentials, fakes.SecretHasher);

        var issued = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");
        Assert.False(await verify.ExecuteAsync(tenant.Id, issued.Credential.Id, "wrong-secret"));
        Assert.True(await verify.ExecuteAsync(tenant.Id, issued.Credential.Id, issued.PlainTextSecret));

        var expired = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "expired", DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.False(await verify.ExecuteAsync(tenant.Id, expired.Credential.Id, expired.PlainTextSecret));
    }

    [Fact]
    public async Task AGT_002_list_credentials_is_tenant_scoped()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var agentA = await SeedAgentAsync(fakes, tenantA.Id, "agent@alphacorp.com");

        await IssueCredentialAsync(fakes, tenantA.Id, agentA.Id, "deploy-key");

        // Deny-by-default: agentA is indistinguishable from a missing principal in tenant B.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new ListAgentCredentials(fakes.Credentials, fakes.Principals).ExecuteAsync(tenantB.Id, agentA.Id));
    }

    [Fact]
    public async Task TEN_007_authenticate_agent_returns_verified_principal_only_for_valid_secret()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var issued = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");

        var authenticate = new AuthenticateAgent(fakes.Principals, fakes.Credentials, fakes.SecretHasher);

        var authenticated = await authenticate.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, issued.PlainTextSecret);
        Assert.Equal(agent.Id, authenticated.Id);

        // A mismatched secret is denied indistinguishably from a missing credential (AGT-002/TEN-007).
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            authenticate.ExecuteAsync(tenant.Id, agent.Id, issued.Credential.Id, "wrong-secret"));
    }

    [Fact]
    public async Task TEN_007_authenticate_agent_rejects_credential_of_another_agent()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var other = await SeedAgentAsync(fakes, tenant.Id, "agent-2@alphacorp.com");
        var issued = await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");

        var authenticate = new AuthenticateAgent(fakes.Principals, fakes.Credentials, fakes.SecretHasher);

        // A credential is bound to its issuing agent, so presenting it as another agent is denied.
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            authenticate.ExecuteAsync(tenant.Id, other.Id, issued.Credential.Id, issued.PlainTextSecret));
    }

    [Fact]
    public async Task AGT_002_003_013_grant_is_proposed_then_approved_then_discoverable()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");

        var propose = new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork);
        var approve = new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var discover = new DiscoverAgentCapabilities(fakes.Grants, fakes.Principals, fakes.Schemas, fakes.RelationTypes);

        var grant = await propose.ExecuteAsync(tenant.Id, agent.Id, projectId: null, AgentCapability.ReadKnowledge, null);

        // A proposed grant is not yet effective.
        Assert.Empty((await discover.ExecuteAsync(tenant.Id, agent.Id)).Grants);

        var approved = await approve.ExecuteAsync(tenant.Id, grant.Id, approver.Id);

        Assert.Equal(AgentGrantStatus.Approved, approved.Status);
        Assert.Equal(approver.Id, approved.ApprovedBy);

        var effective = (await discover.ExecuteAsync(tenant.Id, agent.Id)).Grants;
        Assert.Single(effective);
        Assert.Equal(AgentCapability.ReadKnowledge, effective[0].Capability);
    }

    [Fact]
    public async Task AGT_003_discovery_exposes_permitted_schemas_relation_types_and_tools()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");

        var published = SchemaDefinition.Create("architecture", "1.0", "platform", "Architecture doc", "{\"type\":\"object\"}");
        published.Publish();
        var draft = SchemaDefinition.Create("unpublished", "1.0", "platform", "Draft schema", "{\"type\":\"object\"}");
        await fakes.Schemas.AddAsync(published);
        await fakes.Schemas.AddAsync(draft);

        var relationType = RelationType.Create("depends-on", "Depends on");
        await fakes.RelationTypes.AddAsync(relationType);

        var discover = new DiscoverAgentCapabilities(fakes.Grants, fakes.Principals, fakes.Schemas, fakes.RelationTypes);

        // With no effective grant the discovery surface is empty in every dimension.
        var empty = await discover.ExecuteAsync(tenant.Id, agent.Id);
        Assert.Empty(empty.Grants);
        Assert.Empty(empty.PermittedSchemas);
        Assert.Empty(empty.PermittedRelationTypes);
        Assert.Empty(empty.PermittedTools);

        // ReadKnowledge unlocks published schemas; TraverseRelations unlocks active relation types.
        var read = await new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, agent.Id, null, AgentCapability.ReadKnowledge, null);
        await new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, read.Id, approver.Id);
        var traverse = await new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, agent.Id, null, AgentCapability.TraverseRelations, null);
        await new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, traverse.Id, approver.Id);

        var discovery = await discover.ExecuteAsync(tenant.Id, agent.Id);

        Assert.Equal(2, discovery.Grants.Count);
        Assert.Equal(published.Id, Assert.Single(discovery.PermittedSchemas).Id);
        Assert.Equal(relationType.Id, Assert.Single(discovery.PermittedRelationTypes).Id);
        Assert.Equal(
            new[] { AgentCapability.ReadKnowledge, AgentCapability.TraverseRelations },
            discovery.PermittedTools);
    }

    [Fact]
    public async Task AGT_013_approving_an_already_approved_grant_conflicts()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");

        var propose = new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork);
        var approve = new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        var grant = await propose.ExecuteAsync(tenant.Id, agent.Id, null, AgentCapability.ReadKnowledge, null);
        await approve.ExecuteAsync(tenant.Id, grant.Id, approver.Id);

        await Assert.ThrowsAsync<ConflictException>(() =>
            approve.ExecuteAsync(tenant.Id, grant.Id, approver.Id));
    }

    [Fact]
    public async Task AGT_013_revoking_a_grant_removes_it_from_discovery()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");

        var propose = new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork);
        var approve = new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var revoke = new RevokeAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);
        var discover = new DiscoverAgentCapabilities(fakes.Grants, fakes.Principals, fakes.Schemas, fakes.RelationTypes);

        var grant = await propose.ExecuteAsync(tenant.Id, agent.Id, null, AgentCapability.ReadKnowledge, null);
        var approved = await approve.ExecuteAsync(tenant.Id, grant.Id, approver.Id);
        Assert.Single((await discover.ExecuteAsync(tenant.Id, agent.Id)).Grants);

        var revoked = await revoke.ExecuteAsync(tenant.Id, approved.Id, approver.Id);

        Assert.Equal(AgentGrantStatus.Revoked, revoked.Status);
        Assert.Empty((await discover.ExecuteAsync(tenant.Id, agent.Id)).Grants);
    }

    [Fact]
    public async Task AGT_013_approval_by_a_member_without_owner_or_admin_role_is_denied()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");

        // A human with only a Member membership has no approval authority over agent grants.
        var member = Principal.Create(tenant.Id, "member@alphacorp.com", PrincipalType.Human);
        await fakes.Principals.AddAsync(member);
        var memberMembership = Membership.Invite(tenant.Id, member.Id, MembershipRole.Member);
        memberMembership.Activate();
        await fakes.Memberships.AddAsync(memberMembership);

        var grant = await new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, agent.Id, null, AgentCapability.ReadKnowledge, null);
        var approve = new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            approve.ExecuteAsync(tenant.Id, grant.Id, member.Id));
    }

    [Fact]
    public async Task AGT_013_approval_by_an_agent_principal_is_denied()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var otherAgent = await SeedAgentAsync(fakes, tenant.Id, "agent-2@alphacorp.com");

        var grant = await new GrantAgentCapability(fakes.Grants, fakes.Principals, fakes.Projects, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, agent.Id, null, AgentCapability.ReadKnowledge, null);
        var approve = new ApproveAgentGrant(fakes.Grants, fakes.Principals, fakes.Memberships, fakes.UnitOfWork);

        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            approve.ExecuteAsync(tenant.Id, grant.Id, otherAgent.Id));
    }

    [Fact]
    public async Task AGT_004_assemble_denies_without_an_effective_grant_and_excludes_source_code()
    {
        var fakes = CreateFakes();
        var seed = await SeedAuthorizedScenarioAsync(fakes);
        var assemble = AssembleUseCase(fakes);

        // Authorization-first: with no grant the request is denied without disclosing knowledge.
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            assemble.ExecuteAsync(
                seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
                new[] { seed.TextItem.Id }));

        // An approved ReadKnowledge grant over the project authorizes assembly.
        var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(seed.Approver.Id);
        await fakes.Grants.AddAsync(grant);

        var package = await assemble.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
            new[] { seed.TextItem.Id, seed.SourceCodeItem.Id });

        // Source-code-bearing content is excluded; only the text item remains, renumbered from 0.
        var entry = Assert.Single(package.Entries);
        Assert.Equal(seed.TextItem.Id, entry.ItemId);
        Assert.Equal(0, entry.Position);
        Assert.DoesNotContain(package.Entries, e => e.ItemId == seed.SourceCodeItem.Id);
    }

    [Fact]
    public async Task AGT_004_context_assembly_excludes_code_that_is_labelled_as_text()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");
        var project = Project.Create(tenant.Id, "Shop API", "shop-api", null, LifecycleProfileId.New(), approver.Id);
        await fakes.Projects.AddAsync(project);
        await fakes.ProjectMemberships.AddAsync(ProjectMembership.Grant(tenant.Id, project.Id, agent.Id, ProjectRole.Member));

        var note = KnowledgeItem.Create(
            tenant.Id, project.Id, "architecture", "Architecture", Classification.Internal,
            RepresentationKind.Text, "doc.note", "1.0", approver.Id);
        var noteRevision = note.AddRevision("1.0", "{\"diagram\":\"layers\"}", approver.Id, "Baseline", null, null, note.CurrentRevisionNumber);
        await fakes.Items.AddAsync(note);
        await fakes.Revisions.AddAsync(noteRevision);

        // A Text-labelled item whose payload is actually source code: the enum-only domain guard would
        // let it through, so the shared content policy must exclude it (SRS 7.7, AGT-004, AC-09).
        var disguised = KnowledgeItem.Create(
            tenant.Id, project.Id, "helper", "Helper", Classification.Internal,
            RepresentationKind.Text, "doc.note", "1.0", approver.Id);
        var disguisedRevision = disguised.AddRevision(
            "1.0", "using System; public class Helper { public static void Main() { } }",
            approver.Id, "Baseline", null, null, disguised.CurrentRevisionNumber);
        await fakes.Items.AddAsync(disguised);
        await fakes.Revisions.AddAsync(disguisedRevision);

        fakes.Inspector.Evaluate = text => text.Contains("public class ", StringComparison.Ordinal)
            ? ContentInspectionResult.Quarantine(CodeQuarantineReasons)
            : ContentInspectionResult.Allow();

        var grant = AgentGrant.Propose(tenant.Id, project.Id, agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(approver.Id);
        await fakes.Grants.AddAsync(grant);

        var package = await AssembleUseCase(fakes).ExecuteAsync(
            tenant.Id, project.Id, agent.Id, "review", "idem-1", new[] { note.Id, disguised.Id });

        var entry = Assert.Single(package.Entries);
        Assert.Equal(note.Id, entry.ItemId);
    }

    [Fact]
    public async Task AGT_013_context_assembly_excludes_items_above_classification_ceiling()
    {
        var fakes = CreateFakes();
        var seed = await SeedAuthorizedScenarioAsync(fakes);

        // A Restricted item exceeds the default Internal ceiling and must be withheld.
        var restrictedItem = KnowledgeItem.Create(
            seed.Tenant.Id, seed.Project.Id, "credentials", "Credentials", Classification.Restricted,
            RepresentationKind.Text, "credentials.json", "1.0", seed.Approver.Id);
        var restrictedRevision = restrictedItem.AddRevision(
            "1.0", "{\"secret\":\"api-key\"}", seed.Approver.Id, "Baseline", null, null, restrictedItem.CurrentRevisionNumber);
        await fakes.Items.AddAsync(restrictedItem);
        await fakes.Revisions.AddAsync(restrictedRevision);

        var grant = AgentGrant.Propose(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, AgentCapability.ReadKnowledge, null, Classification.Internal);
        grant.Approve(seed.Approver.Id);
        await fakes.Grants.AddAsync(grant);

        var package = await AssembleUseCase(fakes).ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
            new[] { seed.TextItem.Id, restrictedItem.Id });

        var entry = Assert.Single(package.Entries);
        Assert.Equal(seed.TextItem.Id, entry.ItemId);
        Assert.DoesNotContain(package.Entries, e => e.ItemId == restrictedItem.Id);
    }

    [Fact]
    public async Task AGT_002_context_assembly_requires_active_credential()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");
        var project = Project.Create(tenant.Id, "Shop API", "shop-api", null, LifecycleProfileId.New(), approver.Id);
        await fakes.Projects.AddAsync(project);

        var item = KnowledgeItem.Create(
            tenant.Id, project.Id, "architecture", "Architecture", Classification.Internal,
            RepresentationKind.Text, "architecture.mmd", "1.0", approver.Id);
        var revision = item.AddRevision("1.0", "{\"diagram\":\"layers\"}", approver.Id, "Baseline", null, null, item.CurrentRevisionNumber);
        await fakes.Items.AddAsync(item);
        await fakes.Revisions.AddAsync(revision);

        var grant = AgentGrant.Propose(tenant.Id, project.Id, agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(approver.Id);
        await fakes.Grants.AddAsync(grant);

        // A covering grant without an active credential is still denied (authenticated agent status).
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            AssembleUseCase(fakes).ExecuteAsync(
                tenant.Id, project.Id, agent.Id, "review", "idem-1", new[] { item.Id }));
    }

    [Fact]
    public async Task AGT_002_context_assembly_requires_project_admission()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");
        var project = Project.Create(tenant.Id, "Shop API", "shop-api", null, LifecycleProfileId.New(), approver.Id);
        await fakes.Projects.AddAsync(project);

        var item = KnowledgeItem.Create(
            tenant.Id, project.Id, "architecture", "Architecture", Classification.Internal,
            RepresentationKind.Text, "architecture.mmd", "1.0", approver.Id);
        var revision = item.AddRevision("1.0", "{\"diagram\":\"layers\"}", approver.Id, "Baseline", null, null, item.CurrentRevisionNumber);
        await fakes.Items.AddAsync(item);
        await fakes.Revisions.AddAsync(revision);

        var grant = AgentGrant.Propose(tenant.Id, project.Id, agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(approver.Id);
        await fakes.Grants.AddAsync(grant);

        // A covering grant and active credential are not enough: the agent must be explicitly admitted
        // to this project (a tenant-wide grant alone cannot reach a project the agent never joined).
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            AssembleUseCase(fakes).ExecuteAsync(
                tenant.Id, project.Id, agent.Id, "review", "idem-1", new[] { item.Id }));
    }

    [Fact]
    public async Task AGT_004_context_assembly_rejects_oversized_request()
    {
        var fakes = CreateFakes();
        var seed = await SeedAuthorizedScenarioAsync(fakes);

        var grant = AgentGrant.Propose(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(seed.Approver.Id);
        await fakes.Grants.AddAsync(grant);

        var oversized = Enumerable.Range(0, 101).Select(_ => KnowledgeItemId.New()).ToArray();

        await Assert.ThrowsAsync<ConflictException>(() =>
            AssembleUseCase(fakes).ExecuteAsync(
                seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1", oversized));
    }

    [Fact]
    public async Task AGT_010_context_assembly_deduplicates_on_idempotency_key()
    {
        var fakes = CreateFakes();
        var seed = await SeedAuthorizedScenarioAsync(fakes);
        var assemble = AssembleUseCase(fakes);

        var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(seed.Approver.Id);
        await fakes.Grants.AddAsync(grant);

        var first = await assemble.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
            new[] { seed.TextItem.Id });
        var second = await assemble.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
            new[] { seed.TextItem.Id });

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fakes.Packages.Packages);
    }

    [Fact]
    public async Task AGT_005_record_execution_produces_revision_pinned_provenance()
    {
        var fakes = CreateFakes();
        var seed = await SeedAuthorizedScenarioAsync(fakes);
        var assemble = AssembleUseCase(fakes);

        var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(seed.Approver.Id);
        await fakes.Grants.AddAsync(grant);

        var package = await assemble.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
            new[] { seed.TextItem.Id });

        var record = new RecordAgentExecution(fakes.Activities, fakes.Packages, fakes.Principals, fakes.Projects, fakes.Credentials, fakes.Grants, fakes.UnitOfWork);
        var activity = await record.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, package.Id, "summarize",
            ActivityStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        Assert.Equal(package.Id.ToString(), activity.CorrelationId);
        Assert.Equal(seed.TextRevision.Id, Assert.Single(activity.Inputs).RevisionId);
        Assert.Single(fakes.Activities.Items);
    }

    [Fact]
    public async Task AGT_005_record_execution_is_idempotent_per_package_and_agent()
    {
        var fakes = CreateFakes();
        var seed = await SeedAuthorizedScenarioAsync(fakes);

        var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, AgentCapability.ReadKnowledge, null);
        grant.Approve(seed.Approver.Id);
        await fakes.Grants.AddAsync(grant);

        var package = await AssembleUseCase(fakes).ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, "review", "idem-1",
            new[] { seed.TextItem.Id });

        var record = new RecordAgentExecution(fakes.Activities, fakes.Packages, fakes.Principals, fakes.Projects, fakes.Credentials, fakes.Grants, fakes.UnitOfWork);
        var first = await record.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, package.Id, "summarize",
            ActivityStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var second = await record.ExecuteAsync(
            seed.Tenant.Id, seed.Project.Id, seed.Agent.Id, package.Id, "summarize",
            ActivityStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fakes.Activities.Items);
    }

    [Fact]
    public async Task reads_never_disclose_cross_tenant_agent_records()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var agentB = await SeedAgentAsync(fakes, tenantB.Id, "agent@betacorp.com");

        var grantB = AgentGrant.Propose(tenantB.Id, null, agentB.Id, AgentCapability.ReadKnowledge, null);
        grantB.Approve(PrincipalId.New());
        await fakes.Grants.AddAsync(grantB);

        var discover = new DiscoverAgentCapabilities(fakes.Grants, fakes.Principals, fakes.Schemas, fakes.RelationTypes);

        // tenant A cannot resolve tenant B's agent, so discovery surfaces 404 rather than a fact.
        await Assert.ThrowsAsync<NotFoundException>(() => discover.ExecuteAsync(tenantA.Id, agentB.Id));
    }

    private static AssembleContextPackage AssembleUseCase(Fakes fakes) =>
        new(fakes.Packages, fakes.Grants, fakes.Principals, fakes.Projects, fakes.Items, fakes.Revisions, fakes.Credentials, fakes.ProjectMemberships, fakes.Inspector, fakes.UnitOfWork);

    private static Fakes CreateFakes() => new();

    private static async Task<Principal> SeedAgentAsync(Fakes fakes, TenantId tenantId, string email)
    {
        var agent = Principal.Create(tenantId, email, PrincipalType.AiAgent);
        await fakes.Principals.AddAsync(agent);
        return agent;
    }

    private static async Task<Principal> SeedHumanAsync(Fakes fakes, TenantId tenantId, string email)
    {
        var human = Principal.Create(tenantId, email, PrincipalType.Human);
        await fakes.Principals.AddAsync(human);

        // Seed an active Owner membership so the human holds tenant-level approval authority (AGT-013).
        var membership = Membership.Invite(tenantId, human.Id, MembershipRole.Owner);
        membership.Activate();
        await fakes.Memberships.AddAsync(membership);

        return human;
    }

    private static async Task<AgentCredentialIssued> IssueCredentialAsync(
        Fakes fakes,
        TenantId tenantId,
        PrincipalId agentId,
        string label,
        DateTimeOffset? expiresAt = null) =>
        await new IssueAgentCredential(fakes.Credentials, fakes.Principals, fakes.SecretHasher, fakes.UnitOfWork)
            .ExecuteAsync(tenantId, agentId, label, expiresAt);

    private static async Task<Seed> SeedAuthorizedScenarioAsync(Fakes fakes)
    {
        var tenant = Tenant.Create("Alpha Corp");
        var agent = await SeedAgentAsync(fakes, tenant.Id, "agent-1@alphacorp.com");
        await IssueCredentialAsync(fakes, tenant.Id, agent.Id, "deploy-key");
        var approver = await SeedHumanAsync(fakes, tenant.Id, "owner@alphacorp.com");
        var project = Project.Create(tenant.Id, "Shop API", "shop-api", null, LifecycleProfileId.New(), approver.Id);
        await fakes.Projects.AddAsync(project);
        await fakes.ProjectMemberships.AddAsync(ProjectMembership.Grant(tenant.Id, project.Id, agent.Id, ProjectRole.Member));

        var textItem = KnowledgeItem.Create(
            tenant.Id, project.Id, "architecture", "Architecture", Classification.Internal,
            RepresentationKind.Text, "architecture.mmd", "1.0", approver.Id);
        var textRevision = textItem.AddRevision("1.0", "{\"diagram\":\"layers\"}", approver.Id, "Baseline", null, null, textItem.CurrentRevisionNumber);
        await fakes.Items.AddAsync(textItem);
        await fakes.Revisions.AddAsync(textRevision);

        var sourceCodeItem = KnowledgeItem.Create(
            tenant.Id, project.Id, "app.js", "Application Source", Classification.Internal,
            RepresentationKind.SourceCode, "source", "1.0", approver.Id);
        var sourceRevision = sourceCodeItem.AddRevision("1.0", "console.log('secret-marker');", approver.Id, "Baseline", null, null, sourceCodeItem.CurrentRevisionNumber);
        await fakes.Items.AddAsync(sourceCodeItem);
        await fakes.Revisions.AddAsync(sourceRevision);

        return new Seed(tenant, project, agent, approver, textItem, textRevision, sourceCodeItem, sourceRevision);
    }

    private sealed record Seed(
        Tenant tenant,
        Project project,
        Principal agent,
        Principal approver,
        KnowledgeItem textItem,
        KnowledgeRevision textRevision,
        KnowledgeItem sourceCodeItem,
        KnowledgeRevision sourceCodeRevision)
    {
        public Tenant Tenant => tenant;
        public Project Project => project;
        public Principal Agent => agent;
        public Principal Approver => approver;
        public KnowledgeItem TextItem => textItem;
        public KnowledgeRevision TextRevision => textRevision;
        public KnowledgeItem SourceCodeItem => sourceCodeItem;
        public KnowledgeRevision SourceCodeRevision => sourceCodeRevision;
    }

    private sealed class Fakes
    {
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemoryMembershipRepository Memberships { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryKnowledgeRevisionRepository Revisions { get; } = new();
        public InMemoryActivityRepository Activities { get; } = new();
        public InMemoryAgentGrantRepository Grants { get; } = new();
        public InMemoryAgentCredentialRepository Credentials { get; } = new();
        public InMemoryProjectMembershipRepository ProjectMemberships { get; } = new();
        public InMemoryContextPackageRepository Packages { get; } = new();
        public InMemorySchemaDefinitionRepository Schemas { get; } = new();
        public InMemoryRelationTypeRepository RelationTypes { get; } = new();
        public FakeSecretHasher SecretHasher { get; } = new();
        public StubContentInspector Inspector { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryAgentGrantRepository : IAgentGrantRepository
    {
        public List<AgentGrant> Grants { get; } = new();

        public Task AddAsync(AgentGrant grant, CancellationToken ct = default)
        {
            Grants.Add(grant);
            return Task.CompletedTask;
        }

        public Task<AgentGrant?> GetByIdAsync(TenantId tenantId, AgentGrantId id, CancellationToken ct = default) =>
            Task.FromResult(Grants.FirstOrDefault(g => g.TenantId == tenantId && g.Id == id));

        public Task<IReadOnlyList<AgentGrant>> ListForAgentAsync(TenantId tenantId, PrincipalId agentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AgentGrant>>(
                Grants.Where(g => g.TenantId == tenantId && g.AgentId == agentId).ToList());
    }

    private sealed class InMemorySchemaDefinitionRepository : ISchemaDefinitionRepository
    {
        public List<SchemaDefinition> Schemas { get; } = new();

        public Task AddAsync(SchemaDefinition schema, CancellationToken ct = default)
        {
            Schemas.Add(schema);
            return Task.CompletedTask;
        }

        public Task<SchemaDefinition?> FindAsync(string @namespace, string version, CancellationToken ct = default) =>
            Task.FromResult(Schemas.FirstOrDefault(s => s.Namespace == @namespace && s.Version == version));

        public Task<SchemaDefinition?> GetByIdAsync(SchemaDefinitionId id, CancellationToken ct = default) =>
            Task.FromResult(Schemas.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<SchemaDefinition>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SchemaDefinition>>(Schemas.ToList());
    }

    private sealed class InMemoryRelationTypeRepository : IRelationTypeRepository
    {
        public List<RelationType> Types { get; } = new();

        public Task AddAsync(RelationType relationType, CancellationToken ct = default)
        {
            Types.Add(relationType);
            return Task.CompletedTask;
        }

        public Task<RelationType?> GetByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Types.FirstOrDefault(t => t.Code == code));

        public Task<RelationType?> GetByIdAsync(RelationTypeId id, CancellationToken ct = default) =>
            Task.FromResult(Types.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<RelationType>> ListActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<RelationType>>(Types.Where(t => t.IsActive).ToList());
    }

    private sealed class InMemoryAgentCredentialRepository : IAgentCredentialRepository
    {
        public List<AgentCredential> Credentials { get; } = new();

        public Task AddAsync(AgentCredential credential, CancellationToken ct = default)
        {
            Credentials.Add(credential);
            return Task.CompletedTask;
        }

        public Task<AgentCredential?> GetByIdAsync(TenantId tenantId, AgentCredentialId id, CancellationToken ct = default) =>
            Task.FromResult(Credentials.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));

        public Task<IReadOnlyList<AgentCredential>> ListForAgentAsync(TenantId tenantId, PrincipalId agentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AgentCredential>>(
                Credentials.Where(c => c.TenantId == tenantId && c.AgentId == agentId).ToList());
    }

    private sealed class InMemoryProjectMembershipRepository : IProjectMembershipRepository
    {
        public List<ProjectMembership> Memberships { get; } = new();

        public Task AddAsync(ProjectMembership membership, CancellationToken ct = default)
        {
            Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task<ProjectMembership?> GetByIdAsync(TenantId tenantId, ProjectMembershipId id, CancellationToken ct = default) =>
            Task.FromResult(Memberships.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                Memberships.Where(m => m.TenantId == tenantId && m.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                Memberships.Where(m => m.TenantId == tenantId && m.PrincipalId == principalId).ToList());
    }

    private sealed class InMemoryContextPackageRepository : IContextPackageRepository
    {
        public List<ContextPackage> Packages { get; } = new();

        public Task AddAsync(ContextPackage package, CancellationToken ct = default)
        {
            Packages.Add(package);
            return Task.CompletedTask;
        }

        public Task<ContextPackage?> GetByIdAsync(TenantId tenantId, ContextPackageId id, CancellationToken ct = default) =>
            Task.FromResult(Packages.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));

        public Task<ContextPackage?> FindByIdempotencyKeyAsync(TenantId tenantId, ProjectId projectId, PrincipalId agentId, string idempotencyKey, CancellationToken ct = default) =>
            Task.FromResult(Packages.FirstOrDefault(p =>
                p.TenantId == tenantId && p.ProjectId == projectId && p.AgentId == agentId && p.IdempotencyKey == idempotencyKey));
    }

    private sealed class FakeSecretHasher : ISecretHasher
    {
        public string Hash(string secret) => "hash:" + secret;

        public bool Verify(string secret, string hash) => Hash(secret) == hash;
    }

    private sealed class InMemoryPrincipalRepository : IPrincipalRepository
    {
        public List<Principal> Principals { get; } = new();

        public Task AddAsync(Principal principal, CancellationToken ct = default)
        {
            Principals.Add(principal);
            return Task.CompletedTask;
        }

        public Task<Principal?> GetByIdAsync(TenantId tenantId, PrincipalId id, CancellationToken ct = default) =>
            Task.FromResult(Principals.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));
    }

    private sealed class InMemoryMembershipRepository : IMembershipRepository
    {
        public List<Membership> Memberships { get; } = new();

        public Task AddAsync(Membership membership, CancellationToken ct = default)
        {
            Memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task<Membership?> GetByIdAsync(TenantId tenantId, MembershipId id, CancellationToken ct = default) =>
            Task.FromResult(Memberships.FirstOrDefault(m => m.TenantId == tenantId && m.Id == id));

        public Task<IReadOnlyList<Membership>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Membership>>(Memberships.Where(m => m.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<Membership>> ListByPrincipalAsync(TenantId tenantId, PrincipalId principalId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Membership>>(
                Memberships.Where(m => m.TenantId == tenantId && m.PrincipalId == principalId).ToList());
    }

    private sealed class InMemoryProjectRepository : IProjectRepository
    {
        public List<Project> Projects { get; } = new();

        public Task AddAsync(Project project, CancellationToken ct = default)
        {
            Projects.Add(project);
            return Task.CompletedTask;
        }

        public Task<Project?> GetByIdAsync(TenantId tenantId, ProjectId id, CancellationToken ct = default) =>
            Task.FromResult(Projects.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<Project>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Project>>(Projects.Where(p => p.TenantId == tenantId && !p.IsDeleted).ToList());

        public Task<bool> KeyExistsAsync(TenantId tenantId, string key, CancellationToken ct = default) =>
            Task.FromResult(Projects.Any(p => p.TenantId == tenantId && p.Key == key));
    }

    private sealed class InMemoryKnowledgeItemRepository : IKnowledgeItemRepository
    {
        public List<KnowledgeItem> Items { get; } = new();

        public Task AddAsync(KnowledgeItem item, CancellationToken ct = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task<KnowledgeItem?> GetByIdAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(i => i.TenantId == tenantId && i.Id == id));

        public Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(
                Items.Where(i => i.TenantId == tenantId && i.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(Items.Where(i => i.TenantId == tenantId).ToList());

        public Task<bool> KeyExistsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(i => i.TenantId == tenantId && i.ProjectId == projectId && i.Key == key));
    }

    private sealed class InMemoryKnowledgeRevisionRepository : IKnowledgeRevisionRepository
    {
        public List<KnowledgeRevision> Revisions { get; } = new();

        public Task AddAsync(KnowledgeRevision revision, CancellationToken ct = default)
        {
            Revisions.Add(revision);
            return Task.CompletedTask;
        }

        public Task<KnowledgeRevision?> GetByIdAsync(TenantId tenantId, KnowledgeRevisionId id, CancellationToken ct = default) =>
            Task.FromResult(Revisions.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

        public Task<IReadOnlyList<KnowledgeRevision>> ListByItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRevision>>(
                Revisions.Where(r => r.TenantId == tenantId && r.KnowledgeItemId == itemId).ToList());

        public Task<IReadOnlyList<KnowledgeRevision>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeRevision>>(Revisions.Where(r => r.TenantId == tenantId).ToList());
    }

    private sealed class InMemoryActivityRepository : IActivityRepository
    {
        public List<Activity> Items { get; } = new();

        public Task AddAsync(Activity activity, CancellationToken ct = default)
        {
            Items.Add(activity);
            return Task.CompletedTask;
        }

        public Task<Activity?> GetByIdAsync(TenantId tenantId, ActivityId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));

        public Task<IReadOnlyList<Activity>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Activity>> ListForItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && (a.Inputs.Any(i => i.ItemId == itemId) || a.Outputs.Any(o => o.ItemId == itemId))).ToList());

        public Task<IReadOnlyList<Activity>> ListByActorAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(Items.Where(a => a.TenantId == tenantId && a.Actor == actorId).ToList());

        public Task<Activity?> FindByCorrelationIdAsync(TenantId tenantId, ProjectId projectId, PrincipalId actorId, string correlationId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a =>
                a.TenantId == tenantId && a.ProjectId == projectId && a.Actor == actorId && a.CorrelationId == correlationId));
    }

    private sealed class StubContentInspector : IContentInspector
    {
        public Func<string, ContentInspectionResult> Evaluate { get; set; } =
            static _ => ContentInspectionResult.Allow();

        public Task<ContentInspectionResult> InspectAsync(
            string fileName,
            string mediaType,
            byte[] content,
            CancellationToken ct = default) =>
            Task.FromResult(Evaluate(System.Text.Encoding.UTF8.GetString(content)));
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}