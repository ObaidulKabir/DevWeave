using DevWeave.Application.Abstractions;
using DevWeave.Application.Knowledge;
using DevWeave.Domain.Activities;
using DevWeave.Domain.Evidence;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using EvidenceRecord = DevWeave.Domain.Evidence.Evidence;

namespace DevWeave.Application.Tests.Knowledge;

/// <summary>
/// Exercises the Knowledge use cases against in-memory fakes, focusing on flow correctness, the
/// cross-tenant deny-by-default invariant (KNW-001/KNW-013), optimistic concurrency (REV-004), the
/// information-state actions (KNW-011/BR-009) and the evidence-gated verification rule
/// (KNW-012/PRO-005/AC-08).
/// </summary>
public sealed class KnowledgeUseCaseTests
{
    private static readonly string[] SecretRejectionReasons = ["Secret marker 'AKIA' detected."];

    [Fact]
    public async Task KNW_003_create_item_establishes_envelope_and_source_attribution()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "doc.note", "1.0");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");

        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(
                tenant.Id, project.Id, "api-design", "API Design Notes",
                Classification.Internal, RepresentationKind.Text, "doc.note", "1.0", actor.Id);

        Assert.Equal(tenant.Id, item.TenantId);
        Assert.Equal(project.Id, item.ProjectId);
        Assert.Equal("api-design", item.Key);
        Assert.Equal(actor.Id, item.CreatedBy);
        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal("doc.note", item.TypeId);
        Assert.Equal("1.0", item.SchemaVersion);
    }

    [Fact]
    public async Task create_rejects_a_project_from_another_tenant()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var projectB = await SeedProjectAsync(fakes, tenantB.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenantA.Id, "a@alphacorp.com");

        var useCase = new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(
                tenantA.Id, projectB.Id, "k", "t", Classification.Public, RepresentationKind.Text, "t", "1", actor.Id));
    }

    [Fact]
    public async Task create_rejects_an_actor_from_another_tenant()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var projectA = await SeedProjectAsync(fakes, tenantA.Id, "shop-api");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");

        var useCase = new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(
                tenantA.Id, projectA.Id, "k", "t", Classification.Public, RepresentationKind.Text, "t", "1", actorB.Id));
    }

    [Fact]
    public async Task create_enforces_project_scoped_key_uniqueness()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var otherTenant = Tenant.Create("Beta Corp");
        var projectA = await SeedProjectAsync(fakes, tenant.Id, "a");
        var projectB = await SeedProjectAsync(fakes, tenant.Id, "b");
        var projectOther = await SeedProjectAsync(fakes, otherTenant.Id, "a");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var actorOther = await SeedPrincipalAsync(fakes, otherTenant.Id, "b@betacorp.com");

        var create = new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork);
        await create.ExecuteAsync(tenant.Id, projectA.Id, "shared", "t", Classification.Public, RepresentationKind.Text, "type", "1", actor.Id);

        // Same tenant + same project -> conflict.
        await Assert.ThrowsAsync<ConflictException>(() =>
            create.ExecuteAsync(tenant.Id, projectA.Id, "shared", "t", Classification.Public, RepresentationKind.Text, "type", "1", actor.Id));

        // Same tenant + different project -> allowed.
        var inOtherProject = await create.ExecuteAsync(tenant.Id, projectB.Id, "shared", "t", Classification.Public, RepresentationKind.Text, "type", "1", actor.Id);
        Assert.Equal(projectB.Id, inOtherProject.ProjectId);

        // Different tenant + same project key -> allowed.
        var inOtherTenant = await create.ExecuteAsync(otherTenant.Id, projectOther.Id, "shared", "t", Classification.Public, RepresentationKind.Text, "type", "1", actorOther.Id);
        Assert.Equal(otherTenant.Id, inOtherTenant.TenantId);
    }

    [Fact]
    public async Task REV_001_add_revision_commits_and_advances_the_pointer()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);

        var revision = await new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, item.Id, 0, "1", "{\"v\":1}", actor.Id, "seed", "src-1", null);

        Assert.Equal(1, revision.RevisionNumber);
        Assert.Equal(item.Id, revision.KnowledgeItemId);
        Assert.Equal(actor.Id, revision.Actor);
        Assert.Equal("src-1", revision.SourceReference);

        var loaded = await new GetKnowledgeItem(fakes.Items).ExecuteAsync(tenant.Id, item.Id);
        Assert.Equal(revision.Id, loaded!.CurrentRevisionId);
        Assert.Equal(1, loaded.CurrentRevisionNumber);
    }

    [Fact]
    public async Task REV_004_stale_expected_revision_number_yields_conflict()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);

        var add = new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork);
        await add.ExecuteAsync(tenant.Id, item.Id, 0, "1", "a", actor.Id, "one", null, null);

        await Assert.ThrowsAsync<ConflictException>(() =>
            add.ExecuteAsync(tenant.Id, item.Id, 0, "1", "b", actor.Id, "two", null, null));
    }

    [Fact]
    public async Task add_revision_rejects_a_missing_item_and_a_cross_tenant_actor()
    {
        var fakes = CreateFakes();
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var projectA = await SeedProjectAsync(fakes, tenantA.Id, "shop-api");
        var actorA = await SeedPrincipalAsync(fakes, tenantA.Id, "a@alphacorp.com");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenantA.Id, projectA.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actorA.Id);

        var add = new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            add.ExecuteAsync(tenantA.Id, KnowledgeItemId.New(), 0, "1", "a", actorA.Id, "one", null, null));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            add.ExecuteAsync(tenantA.Id, item.Id, 0, "1", "a", actorB.Id, "one", null, null));
    }

    [Fact]
    public async Task KNW_002_create_rejects_an_unknown_or_inactive_schema()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");

        var create = new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork);

        // No schema registered at all -> unknown type, rejected at creation (KNW-002 / SRS 7.3).
        await Assert.ThrowsAsync<NotFoundException>(() =>
            create.ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id));

        // A draft schema is not active, so it must be rejected the same way.
        await fakes.Schemas.AddAsync(SchemaDefinition.Create("type", "1", "platform", "desc", "{}"));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            create.ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id));
    }

    [Fact]
    public async Task KNW_002_add_revision_rejects_a_schema_that_became_inactive()
    {
        var fakes = CreateFakes();
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");

        var schema = SchemaDefinition.Create("type", "1", "platform", "desc", "{\"type\":\"object\"}");
        schema.Publish();
        await fakes.Schemas.AddAsync(schema);

        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);

        // Deprecating the schema after the item was created makes the pinned version inactive; a later
        // revision must be rejected indistinguishably from an unknown schema (KNW-002).
        schema.Deprecate();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork)
                .ExecuteAsync(tenant.Id, item.Id, 0, "1", "{}", actor.Id, "one", null, null));
    }

    [Fact]
    public async Task REV_002_add_revision_rejects_a_schema_version_that_disagrees_with_the_item()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);

        var add = new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork);

        // The item pins schema version "1"; a revision claiming a different version must be rejected so
        // the recorded version can never diverge from the schema the payload was checked against.
        await Assert.ThrowsAsync<SchemaVersionMismatchException>(() =>
            add.ExecuteAsync(tenant.Id, item.Id, 0, "2", "{}", actor.Id, "one", null, null));
    }

    [Fact]
    public async Task KNW_004_add_revision_rejects_an_invalid_payload_and_accepts_a_valid_one()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);

        fakes.PayloadValidator.Impl = (content, _) =>
            content.Contains("bad", StringComparison.Ordinal)
                ? new List<SchemaValidationIssue> { new("/v", "must be a number") }
                : new List<SchemaValidationIssue>();

        var add = new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork);

        var ex = await Assert.ThrowsAsync<PayloadValidationException>(() =>
            add.ExecuteAsync(tenant.Id, item.Id, 0, "1", "{\"v\":\"bad\"}", actor.Id, "one", null, null));
        Assert.Single(ex.Issues);
        Assert.Equal("/v", ex.Issues[0].Path);

        var revision = await add.ExecuteAsync(tenant.Id, item.Id, 0, "1", "{}", actor.Id, "one", null, null);
        Assert.Equal(1, revision.RevisionNumber);
    }

    [Fact]
    public async Task ART_008_add_revision_rejects_prohibited_content_before_persistence()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);

        // The shared content policy (SRS 7.7, ART-008, AC-09) runs on every ingress path: a knowledge
        // revision carrying secret material is refused before anything is persisted.
        fakes.Inspector.Result = ContentInspectionResult.Reject(SecretRejectionReasons);

        var ex = await Assert.ThrowsAsync<ProhibitedContentException>(() =>
            new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork)
                .ExecuteAsync(tenant.Id, item.Id, 0, "1", "{\"key\":\"AKIAABCDEFGHIJKLMNOP\"}", actor.Id, "one", null, null));

        Assert.Contains("AKIA", Assert.Single(ex.Reasons));
        Assert.Empty(fakes.Revisions.Revisions);
        Assert.Equal(0, item.CurrentRevisionNumber);
    }

    [Fact]
    public async Task KNW_011_BR_009_approve_reject_and_verify_walk_the_lifecycle()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);
        var revision = await new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, item.Id, 0, "1", "{}", actor.Id, "seed", null, null);

        await new ApproveItem(fakes.Items, fakes.UnitOfWork).ExecuteAsync(tenant.Id, item.Id);
        Assert.Equal(InfoState.Approved, item.State);
        Assert.Equal(ReviewState.Reviewed, item.ReviewState);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);

        var verify = new VerifyItem(fakes.Items, fakes.Activities, fakes.Evidence, fakes.UnitOfWork);

        // Approval alone never verifies: without qualifying evidence the transition is refused (AC-08).
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.ExecuteAsync(tenant.Id, item.Id));

        await SeedAcceptedEvidenceAsync(fakes, tenant.Id, project.Id, actor.Id, item.Id, revision.Id);

        await verify.ExecuteAsync(tenant.Id, item.Id);
        Assert.Equal(InfoState.Verified, item.State);
        Assert.Equal(VerificationState.Verified, item.VerificationState);
    }

    [Fact]
    public async Task PRO_005_AC_08_verify_requires_accepted_evidence_pinned_to_the_current_revision()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);
        var revision = await new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, item.Id, 0, "1", "{}", actor.Id, "seed", null, null);
        await new ApproveItem(fakes.Items, fakes.UnitOfWork).ExecuteAsync(tenant.Id, item.Id);

        var verify = new VerifyItem(fakes.Items, fakes.Activities, fakes.Evidence, fakes.UnitOfWork);

        // No evidence at all: refused.
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.ExecuteAsync(tenant.Id, item.Id));

        // Unreviewed evidence is not qualifying evidence: a claim stays unverified (PRO-005).
        var claim = await SeedClaimActivityAsync(fakes, tenant.Id, project.Id, actor.Id, item.Id, revision.Id);
        var evidence = EvidenceRecord.Create(tenant.Id, project.Id, claim.Id, "screenshot", "sha256:aa", null, "AC-1");
        await fakes.Evidence.AddAsync(evidence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.ExecuteAsync(tenant.Id, item.Id));

        // A policy-reviewed, accepted evidence record is qualifying evidence.
        evidence.Review(EvidenceReviewState.Accepted, actor.Id);
        await verify.ExecuteAsync(tenant.Id, item.Id);

        Assert.Equal(InfoState.Verified, item.State);
        Assert.Equal(VerificationState.Verified, item.VerificationState);
        Assert.Equal(ReviewState.Reviewed, item.ReviewState);
    }

    [Fact]
    public async Task KNW_012_a_new_revision_invalidates_prior_verification_so_stale_evidence_cannot_re_verify()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var add = new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork);
        var item = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "api", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);
        var first = await add.ExecuteAsync(tenant.Id, item.Id, 0, "1", "{}", actor.Id, "seed", null, null);
        await new ApproveItem(fakes.Items, fakes.UnitOfWork).ExecuteAsync(tenant.Id, item.Id);
        await SeedAcceptedEvidenceAsync(fakes, tenant.Id, project.Id, actor.Id, item.Id, first.Id);
        await new VerifyItem(fakes.Items, fakes.Activities, fakes.Evidence, fakes.UnitOfWork).ExecuteAsync(tenant.Id, item.Id);
        Assert.Equal(InfoState.Verified, item.State);

        // New content is unreviewed, unverified content (KNW-012): approval and verification are reset.
        await add.ExecuteAsync(tenant.Id, item.Id, 1, "1", "{\"v\":2}", actor.Id, "change", null, null);
        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Equal(ReviewState.Pending, item.ReviewState);
        Assert.Equal(VerificationState.Unverified, item.VerificationState);

        // The accepted evidence is pinned to the superseded revision, so it cannot re-verify the new
        // content (revision-specific evidence invalidation, AC-08).
        await new ApproveItem(fakes.Items, fakes.UnitOfWork).ExecuteAsync(tenant.Id, item.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new VerifyItem(fakes.Items, fakes.Activities, fakes.Evidence, fakes.UnitOfWork).ExecuteAsync(tenant.Id, item.Id));
    }

    [Fact]
    public async Task BR_009_reject_is_terminal_and_verify_requires_prior_approval()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenant = Tenant.Create("Alpha Corp");
        var project = await SeedProjectAsync(fakes, tenant.Id, "shop-api");
        var actor = await SeedPrincipalAsync(fakes, tenant.Id, "a@alphacorp.com");
        var add = new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork);
        var verify = new VerifyItem(fakes.Items, fakes.Activities, fakes.Evidence, fakes.UnitOfWork);

        var rejected = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "r1", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);
        var rejectedRevision = await add.ExecuteAsync(tenant.Id, rejected.Id, 0, "1", "{}", actor.Id, "seed", null, null);
        await SeedAcceptedEvidenceAsync(fakes, tenant.Id, project.Id, actor.Id, rejected.Id, rejectedRevision.Id);
        await new RejectItem(fakes.Items, fakes.UnitOfWork).ExecuteAsync(tenant.Id, rejected.Id);
        Assert.Equal(InfoState.Rejected, rejected.State);

        // Even with qualifying evidence, a rejected item can never be verified.
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.ExecuteAsync(tenant.Id, rejected.Id));

        var proposed = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenant.Id, project.Id, "r2", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actor.Id);
        var proposedRevision = await add.ExecuteAsync(tenant.Id, proposed.Id, 0, "1", "{}", actor.Id, "seed", null, null);
        await SeedAcceptedEvidenceAsync(fakes, tenant.Id, project.Id, actor.Id, proposed.Id, proposedRevision.Id);

        // Even with qualifying evidence, verification requires an explicit approval first (BR-009).
        await Assert.ThrowsAsync<InvalidOperationException>(() => verify.ExecuteAsync(tenant.Id, proposed.Id));
    }

    [Fact]
    public async Task read_paths_never_disclose_cross_tenant_items_or_revisions()
    {
        var fakes = CreateFakes();
        await SeedActiveSchemaAsync(fakes, "type", "1");
        var tenantA = Tenant.Create("Alpha Corp");
        var tenantB = Tenant.Create("Beta Corp");
        var projectA = await SeedProjectAsync(fakes, tenantA.Id, "a");
        var projectB = await SeedProjectAsync(fakes, tenantB.Id, "b");
        var actorA = await SeedPrincipalAsync(fakes, tenantA.Id, "a@alphacorp.com");
        var actorB = await SeedPrincipalAsync(fakes, tenantB.Id, "b@betacorp.com");

        var itemA = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenantA.Id, projectA.Id, "k", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actorA.Id);
        var itemB = await new CreateKnowledgeItem(fakes.Items, fakes.Projects, fakes.Principals, fakes.Schemas, fakes.UnitOfWork)
            .ExecuteAsync(tenantB.Id, projectB.Id, "k", "t", Classification.Internal, RepresentationKind.Text, "type", "1", actorB.Id);
        var revisionB = await new AddRevision(fakes.Items, fakes.Revisions, fakes.Principals, fakes.Schemas, fakes.PayloadValidator, fakes.Inspector, fakes.UnitOfWork)
            .ExecuteAsync(tenantB.Id, itemB.Id, 0, "1", "{}", actorB.Id, "seed", null, null);

        // Items
        Assert.Null(await new GetKnowledgeItem(fakes.Items).ExecuteAsync(tenantA.Id, itemB.Id));
        var tenantItems = await new ListKnowledgeItemsForTenant(fakes.Items).ExecuteAsync(tenantA.Id);
        Assert.Single(tenantItems);
        Assert.Equal(itemA.Id, tenantItems[0].Id);

        var projectItems = await new ListKnowledgeItemsForProject(fakes.Items).ExecuteAsync(tenantA.Id, projectA.Id);
        Assert.Single(projectItems);
        Assert.Equal(itemA.Id, projectItems[0].Id);
        Assert.Empty(await new ListKnowledgeItemsForProject(fakes.Items).ExecuteAsync(tenantA.Id, projectB.Id));

        // Revisions
        Assert.Null(await new GetRevision(fakes.Revisions).ExecuteAsync(tenantA.Id, revisionB.Id));
        Assert.Empty(await fakes.Revisions.ListByItemAsync(tenantA.Id, itemB.Id));
        Assert.Single(await fakes.Revisions.ListByItemAsync(tenantB.Id, itemB.Id));
    }

    private static Fakes CreateFakes() => new();

    private static async Task<Project> SeedProjectAsync(Fakes fakes, TenantId tenantId, string key)
    {
        var project = Project.Create(tenantId, "Project", key, null, LifecycleProfileId.New(), PrincipalId.New());
        await fakes.Projects.AddAsync(project);
        return project;
    }

    private static async Task<Principal> SeedPrincipalAsync(Fakes fakes, TenantId tenantId, string email)
    {
        var principal = Principal.Create(tenantId, email, PrincipalType.Human);
        await fakes.Principals.AddAsync(principal);
        return principal;
    }

    private static async Task SeedActiveSchemaAsync(Fakes fakes, string @namespace, string version)
    {
        var schema = SchemaDefinition.Create(@namespace, version, "platform", "desc", "{\"type\":\"object\"}");
        schema.Publish();
        await fakes.Schemas.AddAsync(schema);
    }

    private static async Task<Activity> SeedClaimActivityAsync(
        Fakes fakes, TenantId tenantId, ProjectId projectId, PrincipalId actor,
        KnowledgeItemId itemId, KnowledgeRevisionId revisionId)
    {
        // The claim's output pins the exact revision it is about (PRO-004), which is what makes its
        // evidence revision-specific.
        var claim = Activity.Create(
            tenantId, projectId, ActivityType.Claim, actor, "meets AC-1", ActivityStatus.Completed,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null,
            Array.Empty<ActivityInput>(),
            new[] { new ActivityOutput(itemId, revisionId, 0) });
        await fakes.Activities.AddAsync(claim);
        return claim;
    }

    private static async Task SeedAcceptedEvidenceAsync(
        Fakes fakes, TenantId tenantId, ProjectId projectId, PrincipalId actor,
        KnowledgeItemId itemId, KnowledgeRevisionId revisionId)
    {
        var claim = await SeedClaimActivityAsync(fakes, tenantId, projectId, actor, itemId, revisionId);
        var evidence = EvidenceRecord.Create(tenantId, projectId, claim.Id, "screenshot", "sha256:aa", null, "AC-1");
        evidence.Review(EvidenceReviewState.Accepted, actor);
        await fakes.Evidence.AddAsync(evidence);
    }

    private sealed class Fakes
    {
        public InMemoryKnowledgeItemRepository Items { get; } = new();
        public InMemoryKnowledgeRevisionRepository Revisions { get; } = new();
        public InMemoryProjectRepository Projects { get; } = new();
        public InMemoryPrincipalRepository Principals { get; } = new();
        public InMemorySchemaDefinitionRepository Schemas { get; } = new();
        public InMemoryActivityRepository Activities { get; } = new();
        public InMemoryEvidenceRepository Evidence { get; } = new();
        public StubPayloadValidator PayloadValidator { get; } = new();
        public StubContentInspector Inspector { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
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
                Revisions.Where(r => r.TenantId == tenantId && r.KnowledgeItemId == itemId)
                    .OrderBy(r => r.RevisionNumber)
                    .ToList());
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
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && a.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<Activity>> ListForItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId
                    && (a.Inputs.Any(i => i.ItemId == itemId) || a.Outputs.Any(o => o.ItemId == itemId))).ToList());

        public Task<IReadOnlyList<Activity>> ListByActorAsync(TenantId tenantId, PrincipalId actorId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Activity>>(
                Items.Where(a => a.TenantId == tenantId && a.Actor == actorId).ToList());

        public Task<Activity?> FindByCorrelationIdAsync(TenantId tenantId, ProjectId projectId, PrincipalId actorId, string correlationId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(a =>
                a.TenantId == tenantId && a.ProjectId == projectId && a.Actor == actorId && a.CorrelationId == correlationId));
    }

    private sealed class InMemoryEvidenceRepository : IEvidenceRepository
    {
        public List<EvidenceRecord> Items { get; } = new();

        public Task AddAsync(EvidenceRecord evidence, CancellationToken ct = default)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }

        public Task<EvidenceRecord?> GetByIdAsync(TenantId tenantId, EvidenceId id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(e => e.TenantId == tenantId && e.Id == id));

        public Task<IReadOnlyList<EvidenceRecord>> ListForClaimAsync(TenantId tenantId, ActivityId originActivityId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EvidenceRecord>>(
                Items.Where(e => e.TenantId == tenantId && e.OriginActivityId == originActivityId)
                    .OrderBy(e => e.RecordedAt)
                    .ToList());

        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<EvidenceRecord>>(
                Items.Where(e => e.TenantId == tenantId && e.ProjectId == projectId)
                    .OrderBy(e => e.RecordedAt)
                    .ToList());
    }

    private sealed class StubPayloadValidator : IPayloadValidator
    {
        public Func<string, string, IReadOnlyList<SchemaValidationIssue>> Impl { get; set; } =
            static (_, _) => Array.Empty<SchemaValidationIssue>();

        public IReadOnlyList<SchemaValidationIssue> Validate(string contentJson, string schemaJson) =>
            Impl(contentJson, schemaJson);
    }

    private sealed class StubContentInspector : IContentInspector
    {
        public ContentInspectionResult Result { get; set; } = ContentInspectionResult.Allow();

        public Task<ContentInspectionResult> InspectAsync(
            string fileName,
            string mediaType,
            byte[] content,
            CancellationToken ct = default) =>
            Task.FromResult(Result);
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}