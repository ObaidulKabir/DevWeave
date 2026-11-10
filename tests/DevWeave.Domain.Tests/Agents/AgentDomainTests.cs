using DevWeave.Domain.Activities;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Agents;

/// <summary>
/// Proves the Slice 8 agent domain invariants: the governed capability-grant lifecycle (AGT-002,
/// AGT-013), credential expiry/revocation (AGT-002), owner-agnostic revision-pinned context packages
/// (AGT-004, AGT-005), and the source-code exclusion guard (AGT-004).
/// </summary>
public sealed class AgentDomainTests
{
    [Fact]
    public void AGT_002_grant_moves_through_propose_approve_revoke_and_records_audit_facts()
    {
        var (tenant, agent) = Seed();
        var approver = PrincipalId.New();
        var revoker = PrincipalId.New();

        var grant = AgentGrant.Propose(tenant, ProjectId.New(), agent, AgentCapability.ReadKnowledge, null);

        Assert.Equal(AgentGrantStatus.Proposed, grant.Status);
        Assert.Null(grant.ApprovedBy);
        Assert.Null(grant.RevokedAt);

        grant.Approve(approver);
        Assert.Equal(AgentGrantStatus.Approved, grant.Status);
        Assert.Equal(approver, grant.ApprovedBy);
        Assert.NotNull(grant.ApprovedAt);

        grant.Revoke(revoker);
        Assert.Equal(AgentGrantStatus.Revoked, grant.Status);
        Assert.Equal(revoker, grant.RevokedBy);
        Assert.NotNull(grant.RevokedAt);
    }

    [Fact]
    public void AGT_013_grant_rejects_illegal_approve_and_revoke_transitions()
    {
        var (tenant, agent) = Seed();

        var approved = AgentGrant.Propose(tenant, null, agent, AgentCapability.ReadKnowledge, null);
        approved.Approve(PrincipalId.New());
        Assert.Throws<InvalidOperationException>(() => approved.Approve(PrincipalId.New()));

        var revoked = AgentGrant.Propose(tenant, null, agent, AgentCapability.ReadKnowledge, null);
        revoked.Revoke(PrincipalId.New());
        Assert.Throws<InvalidOperationException>(() => revoked.Revoke(PrincipalId.New()));
        Assert.Throws<InvalidOperationException>(() => revoked.Approve(PrincipalId.New()));
    }

    [Fact]
    public void AGT_002_grant_is_active_only_when_approved_and_unexpired()
    {
        var (tenant, agent) = Seed();
        var now = DateTimeOffset.UtcNow;

        var proposed = AgentGrant.Propose(tenant, null, agent, AgentCapability.ReadKnowledge, null);
        Assert.False(proposed.IsActive(now));

        var approved = AgentGrant.Propose(tenant, null, agent, AgentCapability.ReadKnowledge, null);
        approved.Approve(PrincipalId.New());
        Assert.True(approved.IsActive(now));

        var expired = AgentGrant.Propose(tenant, null, agent, AgentCapability.ReadKnowledge, now.AddMinutes(-1));
        expired.Approve(PrincipalId.New());
        Assert.False(expired.IsActive(now));

        var revoked = AgentGrant.Propose(tenant, null, agent, AgentCapability.ReadKnowledge, null);
        revoked.Approve(PrincipalId.New());
        revoked.Revoke(PrincipalId.New());
        Assert.False(revoked.IsActive(now));
    }

    [Fact]
    public void AGT_002_tenant_wide_grant_covers_every_project_but_project_grant_does_not()
    {
        var (tenant, agent) = Seed();
        var project = ProjectId.New();

        Assert.True(AgentGrant.Propose(tenant, null, agent, AgentCapability.AssembleContext, null).CoversProject(project));
        Assert.True(AgentGrant.Propose(tenant, project, agent, AgentCapability.AssembleContext, null).CoversProject(project));
        Assert.False(AgentGrant.Propose(tenant, ProjectId.New(), agent, AgentCapability.AssembleContext, null).CoversProject(project));
    }

    [Fact]
    public void AGT_002_credential_is_issued_active_and_revocation_is_terminal()
    {
        var (tenant, agent) = Seed();

        var credential = AgentCredential.Issue(tenant, agent, "build-agent", "sha256:abc", null);

        Assert.Equal(AgentCredentialStatus.Active, credential.Status);
        Assert.Equal("build-agent", credential.Label);
        Assert.Equal("sha256:abc", credential.SecretHash);
        Assert.Null(credential.RevokedAt);

        credential.Revoke();
        Assert.Equal(AgentCredentialStatus.Revoked, credential.Status);
        Assert.NotNull(credential.RevokedAt);
        Assert.Throws<InvalidOperationException>(() => credential.Revoke());
    }

    [Fact]
    public void AGT_002_credential_is_active_only_while_unrevoked_and_unexpired()
    {
        var (tenant, agent) = Seed();
        var now = DateTimeOffset.UtcNow;

        Assert.True(AgentCredential.Issue(tenant, agent, "a", "h", null).IsActive(now));
        Assert.True(AgentCredential.Issue(tenant, agent, "a", "h", now.AddMinutes(1)).IsActive(now));
        Assert.False(AgentCredential.Issue(tenant, agent, "a", "h", now.AddMinutes(-1)).IsActive(now));

        var revoked = AgentCredential.Issue(tenant, agent, "a", "h", null);
        revoked.Revoke();
        Assert.False(revoked.IsActive(now));
    }

    [Fact]
    public void AGT_002_credential_guards_required_label_and_hash()
    {
        var (tenant, agent) = Seed();

        Assert.Throws<ArgumentException>(() => AgentCredential.Issue(tenant, agent, " ", "h", null));
        Assert.Throws<ArgumentException>(() => AgentCredential.Issue(tenant, agent, "agent", " ", null));
    }

    [Fact]
    public void AGT_004_context_package_assembles_revision_pinned_entries_and_preserves_representation()
    {
        var (tenant, agent) = Seed();
        var project = ProjectId.New();
        var item = KnowledgeItemId.New();
        var revision = KnowledgeRevisionId.New();

        var package = ContextPackage.Assemble(
            tenant,
            project,
            "task-1",
            agent,
            "idem-1",
            new[] { new ContextPackageEntry(item, revision, 5, RepresentationKind.Text) });

        Assert.Equal(tenant, package.TenantId);
        Assert.Equal(project, package.ProjectId);
        Assert.Equal("task-1", package.TaskKey);
        Assert.Equal(agent, package.AgentId);
        Assert.Equal("idem-1", package.IdempotencyKey);

        var entry = Assert.Single(package.Entries);
        Assert.Equal(item, entry.ItemId);
        Assert.Equal(revision, entry.RevisionId);
        Assert.Equal(RepresentationKind.Text, entry.Representation);
        Assert.Equal(0, entry.Position);
    }

    [Fact]
    public void AGT_004_source_code_bearing_content_is_excluded_from_context_packages()
    {
        var (tenant, agent) = Seed();
        var textItem = KnowledgeItemId.New();
        var sourceItem = KnowledgeItemId.New();

        // The dedicated guard: only non-source representations survive assembly, and positions are
        // renumbered so the surviving entries stay stable and contiguous.
        var package = ContextPackage.Assemble(
            tenant,
            ProjectId.New(),
            "task-2",
            agent,
            null,
            new[]
            {
                new ContextPackageEntry(sourceItem, KnowledgeRevisionId.New(), 0, RepresentationKind.SourceCode),
                new ContextPackageEntry(textItem, KnowledgeRevisionId.New(), 1, RepresentationKind.Text),
            });

        var entry = Assert.Single(package.Entries);
        Assert.Equal(textItem, entry.ItemId);
        Assert.Equal(RepresentationKind.Text, entry.Representation);
        Assert.Equal(0, entry.Position);
        Assert.DoesNotContain(package.Entries, e => e.ItemId == sourceItem);
    }

    [Fact]
    public void AGT_004_source_code_guard_covers_only_the_source_code_kind()
    {
        Assert.True(ContextPackage.IsSourceCode(RepresentationKind.SourceCode));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.Text));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.StructuredData));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.Diagram));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.Chart));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.Document));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.Media));
        Assert.False(ContextPackage.IsSourceCode(RepresentationKind.ExternalReference));
    }

    [Fact]
    public void AGT_004_context_package_requires_a_task_key()
    {
        var (tenant, agent) = Seed();

        Assert.Throws<ArgumentException>(() =>
            ContextPackage.Assemble(tenant, ProjectId.New(), " ", agent, null, Array.Empty<ContextPackageEntry>()));
    }

    [Fact]
    public void AGT_006_agent_submitted_knowledge_enters_proposed_pending_review_and_never_self_approves()
    {
        var agent = PrincipalId.New();

        var item = KnowledgeItem.Create(
            TenantId.New(), ProjectId.New(), "agent-summary", "Agent Summary", Classification.Internal,
            RepresentationKind.Text, "agent.summary", "1.0", agent);

        // An agent submission is stored in the proposed/pending-review state; approval requires an
        // explicit human action and can never be achieved merely by persisting the item.
        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal(ReviewState.Pending, item.ReviewState);
        Assert.NotEqual(InfoState.Approved, item.State);
    }

    [Fact]
    public void AGT_007_agent_execution_records_status_inputs_outputs_errors_and_external_references()
    {
        var (tenant, agent) = Seed();
        var project = ProjectId.New();
        var item = KnowledgeItemId.New();
        var revision = KnowledgeRevisionId.New();
        const string externalReference = "https://source.example/repo/app.js#L1";

        // Errors are representable in the status set, and the activity retains status, reason, inputs and outputs.
        var activity = Activity.Create(
            tenant,
            project,
            ActivityType.Generated,
            agent,
            "run build",
            ActivityStatus.Failed,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            correlationId: "pkg-1",
            reason: "exit code 1",
            reference: null,
            inputs: new[] { new ActivityInput(item, revision, 0) },
            outputs: Array.Empty<ActivityOutput>());

        Assert.Contains("Failed", Enum.GetNames<ActivityStatus>());
        Assert.Equal(ActivityStatus.Failed, activity.Status);
        Assert.Equal("exit code 1", activity.Reason);
        Assert.Equal(revision, Assert.Single(activity.Inputs).RevisionId);
        Assert.Empty(activity.Outputs);

        // External references are retained on the sourced revision, completing the provenance envelope.
        var sourced = KnowledgeRevision.Create(
            tenant, item, 1, "1.0", "{}", agent, "import", externalReference, supersededRevisionId: null);
        Assert.Equal(externalReference, sourced.SourceReference);
    }

    [Fact]
    public void AGT_011_stale_agent_revision_produces_conflict_instead_of_silently_overwriting()
    {
        var tenant = TenantId.New();
        var actor = PrincipalId.New();
        var item = KnowledgeItem.Create(
            tenant, ProjectId.New(), "api-design", "API Design", Classification.Internal,
            RepresentationKind.Text, "doc.note", "1.0", actor);
        item.AddRevision("1.0", "{\"v\":1}", actor, "baseline", null, null, 0);

        var ex = Assert.Throws<StaleRevisionException>(() =>
            item.AddRevision("1.0", "{\"v\":2}", actor, "stale writer", null, null, 0));

        Assert.Equal(0, ex.ExpectedRevisionNumber);
        Assert.Equal(1, ex.CurrentRevisionNumber);
        Assert.Equal(1, item.CurrentRevisionNumber);
    }

    private static (TenantId Tenant, PrincipalId Agent) Seed() =>
        (TenantId.New(), PrincipalId.New());
}