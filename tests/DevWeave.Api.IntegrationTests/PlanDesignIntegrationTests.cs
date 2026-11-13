using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Contracts.Planned.V1;
using DevWeave.Application.Planning;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Persistence;
using DevWeave.Mcp.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DevWeave.Api.IntegrationTests;

public sealed class PlanDesignIntegrationTests : IClassFixture<IdentityTestFactory>
{
    private readonly IdentityTestFactory _factory;

    public PlanDesignIntegrationTests(IdentityTestFactory factory)
    {
        _factory = factory;
        _factory.PrepareDatabase();
    }

    [Fact]
    public async Task Outline_requires_one_root_one_parent_and_same_project()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
        var root = await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        Assert.Equal(seed.Root.Id, root.RootItemId);
        var child = await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id, seed.Root.Id, 0);
        Assert.Equal(root.ItemId, child.ParentItemId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id, null, 0));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.OtherProjectItem.Id, root.ItemId, 0));

        using var client = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        using var response = await client.GetAsync($"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/plan/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var view = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(2, view!.RootElement.GetProperty("nodes").GetArrayLength());
        Assert.Equal(seed.Root.Id.Value.ToString(), view.RootElement.GetProperty("nodes")[0]
            .GetProperty("itemId").GetString());
    }

    [Fact]
    public async Task Outline_move_reparents_and_rejects_cycles_root_and_non_leaves_on_detach()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();

        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id, seed.Root.Id, 0);

        var sibling = NewItem(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, "sibling");
        db.Add(sibling);
        db.Add(NewRevision(sibling, seed.Owner.Id));
        await db.SaveChangesAsync();
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, sibling.Id, seed.Root.Id, 1);

        var moved = await design.MoveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id, sibling.Id, 0);
        Assert.Equal(sibling.Id, moved.ParentItemId);

        await Assert.ThrowsAsync<ConflictException>(() =>
            design.MoveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, sibling.Id, seed.Child.Id, 0));
        await Assert.ThrowsAsync<ConflictException>(() =>
            design.MoveAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, sibling.Id, 0));
        await Assert.ThrowsAsync<ConflictException>(() =>
            design.DetachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, sibling.Id));

        await design.DetachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id);
        var remaining = await design.ReadAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id);
        Assert.DoesNotContain(remaining.Nodes, n => n.ItemId == seed.Child.Id);
    }

    [Fact]
    public async Task Links_require_current_reviewed_revision_and_project_ownership()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        await Assert.ThrowsAsync<ConflictException>(() =>
            design.LinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Work.Id, seed.Root.Id, seed.Root.CurrentRevisionId!));

        using (var approval = _factory.Services.CreateScope())
        {
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var item = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id);
            item.Approve();
            await db.SaveChangesAsync();
        }
        using var refreshed = _factory.Services.CreateScope();
        var currentDesign = refreshed.ServiceProvider.GetRequiredService<PlanDesignService>();
        var link = await currentDesign.LinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id,
            seed.Work.Id, seed.Root.Id, seed.Root.CurrentRevisionId!);
        Assert.Equal(seed.Root.CurrentRevisionId, link.RevisionId);
        await Assert.ThrowsAsync<ConflictException>(() =>
            currentDesign.LinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id,
                seed.Work.Id, seed.Root.Id, seed.Root.CurrentRevisionId!));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            currentDesign.LinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id,
                seed.Work.Id, seed.OtherProjectItem.Id, seed.OtherProjectItem.CurrentRevisionId!));
    }

    [Fact]
    public async Task Proposal_needs_independent_human_reviewer_and_matching_reviewed_item()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        var proposal = await design.ProposeAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id,
            seed.Root.Id, seed.Child.Key, seed.Child.Title, seed.Child.TypeId, "Split the module");
        await Assert.ThrowsAsync<ConflictException>(() =>
            design.DecideAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, proposal.Id, seed.Child.Id));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            design.DecideAsync(seed.Tenant.Id, seed.Project.Id, seed.Editor.Id, proposal.Id, seed.Child.Id));

        using (var approval = _factory.Services.CreateScope())
        {
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Child.Id)).Approve();
            await db.SaveChangesAsync();
        }
        var accepted = await design.DecideAsync(seed.Tenant.Id, seed.Project.Id, seed.Reviewer.Id,
            proposal.Id, seed.Child.Id);
        Assert.Equal(PlanProposalStatus.Accepted, accepted.Status);
        Assert.Equal(seed.Root.Id, (await design.ReadAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id))
            .Nodes.Single(n => n.ItemId == seed.Child.Id).ParentItemId);
    }

    [Fact]
    public async Task Agent_without_grant_cannot_read_propose_or_attach_and_database_rows_are_tenant_scoped()
    {
        var seed = await SeedAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var agent = Principal.Create(seed.Tenant.Id, $"agent-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
            db.Add(agent);
            db.Add(ProjectMembership.Grant(seed.Tenant.Id, seed.Project.Id, agent.Id, ProjectRole.Member));
            await db.SaveChangesAsync();
            var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
            await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
            await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
                design.ReadAsync(seed.Tenant.Id, seed.Project.Id, agent.Id));
            await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
                design.ProposeAsync(seed.Tenant.Id, seed.Project.Id, agent.Id, seed.Root.Id,
                    "new", "New", "doc.note", "Expand idea"));
            await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
                design.AttachAsync(seed.Tenant.Id, seed.Project.Id, agent.Id, seed.Child.Id, seed.Root.Id, 0));
        }

        await using var connection = new NpgsqlConnection(_factory.ConnectionString);
        await connection.OpenAsync();
        await using (var role = new NpgsqlCommand("SET ROLE devweave_app", connection))
            await role.ExecuteNonQueryAsync();
        await using (var absent = new NpgsqlCommand("SELECT count(*) FROM plan_outline_nodes", connection))
            Assert.Equal(0L, (long)(await absent.ExecuteScalarAsync())!);
        await using (var tenant = new NpgsqlCommand($"SET app.tenant_id = '{seed.Tenant.Id.Value}'", connection))
            await tenant.ExecuteNonQueryAsync();
        await using (var visible = new NpgsqlCommand("SELECT count(*) FROM plan_outline_nodes", connection))
            Assert.Equal(1L, (long)(await visible.ExecuteScalarAsync())!);
        await using (var other = new NpgsqlCommand($"SET app.tenant_id = '{Guid.NewGuid()}'", connection))
            await other.ExecuteNonQueryAsync();
        await using (var hidden = new NpgsqlCommand("SELECT count(*) FROM plan_outline_nodes", connection))
            Assert.Equal(0L, (long)(await hidden.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Granted_agent_reads_outline_and_submits_proposal_but_cannot_review_it()
    {
        var seed = await SeedAsync();
        Principal agent;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            agent = Principal.Create(seed.Tenant.Id, $"planner-{Guid.NewGuid():N}@example.test", PrincipalType.AiAgent);
            db.Add(agent);
            db.Add(ProjectMembership.Grant(seed.Tenant.Id, seed.Project.Id, agent.Id, ProjectRole.Member));
            db.Add(AgentCredential.Issue(seed.Tenant.Id, agent.Id, "test", "hashed-test-secret", null));
            foreach (var capability in new[] { AgentCapability.ReadKnowledge, AgentCapability.WriteKnowledge })
            {
                var grant = AgentGrant.Propose(seed.Tenant.Id, seed.Project.Id, agent.Id, capability,
                    null, Classification.Internal);
                grant.Approve(seed.Owner.Id);
                db.Add(grant);
            }
            await db.SaveChangesAsync();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id)).Approve();
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<PlanDesignService>()
                .AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        }
        using var client = _factory.CreateAuthenticatedAgentClient(seed.Tenant.Id.Value, agent.Id.Value);
        var url = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/mcp/rpc";
        using var read = await client.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 1, McpMethods.CallTool,
            new { name = "read_plan_outline", arguments = new { } }));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using var outline = await read.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(JsonValueKind.Null, outline!.RootElement.GetProperty("error").ValueKind);
        Assert.Equal(1, outline.RootElement.GetProperty("result").GetProperty("nodes").GetArrayLength());

        using var propose = await client.PostAsJsonAsync(url, new JsonRpcRequest("2.0", 2, McpMethods.CallTool,
            new { name = "propose_plan_expansion", arguments = new {
                parentItemId = seed.Root.Id.Value, key = "agent-module", title = "Agent module",
                typeId = "doc.note", rationale = "Break down the user journey" } }));
        using var result = await propose.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(JsonValueKind.Null, result!.RootElement.GetProperty("error").ValueKind);
        var proposalId = result.RootElement.GetProperty("result").GetProperty("id").GetGuid();
        using var scopeAfter = _factory.Services.CreateScope();
        var design = scopeAfter.ServiceProvider.GetRequiredService<PlanDesignService>();
        await Assert.ThrowsAsync<UnauthorizedTenantException>(() =>
            design.DecideAsync(seed.Tenant.Id, seed.Project.Id, agent.Id, new(proposalId), null));
    }

    [Fact]
    public async Task Baseline_locks_only_the_current_approved_outline()
    {
        var seed = await SeedAsync();
        var scopeFor = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "test", "corr");
        var draft = new BaselineDraft("Goal", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 1);

        // An empty outline cannot be locked.
        using (var empty = _factory.Services.CreateScope())
        {
            var preview = await empty.ServiceProvider.GetRequiredService<PlanLockReview>()
                .PreviewAsync(seed.Tenant.Id, seed.Project.Id);
            Assert.False(preview.CanLock);
            await Assert.ThrowsAsync<ConflictException>(() =>
                empty.ServiceProvider.GetRequiredService<IPlanBaselines>().LockAsync(scopeFor, draft));
        }

        // An attached but unreviewed root blocks the lock and is named in the blockers.
        using (var unreviewed = _factory.Services.CreateScope())
        {
            await unreviewed.ServiceProvider.GetRequiredService<PlanDesignService>()
                .AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        }
        using (var blocked = _factory.Services.CreateScope())
        {
            var preview = await blocked.ServiceProvider.GetRequiredService<PlanLockReview>()
                .PreviewAsync(seed.Tenant.Id, seed.Project.Id);
            Assert.False(preview.CanLock);
            Assert.Equal(seed.Root.Key, Assert.Single(preview.Blockers).Key);
            await Assert.ThrowsAsync<ConflictException>(() =>
                blocked.ServiceProvider.GetRequiredService<IPlanBaselines>().LockAsync(scopeFor, draft));
        }

        using (var approval = _factory.Services.CreateScope())
        {
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var item = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id);
            item.Approve(item.CurrentRevisionId!);
            await db.SaveChangesAsync();
        }

        // Once approved, a baseline built from the server's view locks; a caller naming a different
        // revision (the plan moved on since they reviewed it) is refused.
        using var ready = _factory.Services.CreateScope();
        var lockReview = ready.ServiceProvider.GetRequiredService<PlanLockReview>();
        var approved = await lockReview.PreviewAsync(seed.Tenant.Id, seed.Project.Id);
        Assert.True(approved.CanLock);
        Assert.Equal(seed.Root.CurrentRevisionId, Assert.Single(approved.Items).RevisionId);

        var baselines = ready.ServiceProvider.GetRequiredService<IPlanBaselines>();
        await Assert.ThrowsAsync<ConflictException>(() => baselines.LockAsync(scopeFor,
            draft with { ApprovedRevisions = new[] { new ResourceRef(seed.Root.Id.Value, Guid.NewGuid()) } }));

        var baselineId = await baselines.LockAsync(scopeFor,
            draft with { ApprovedRevisions = new[] { new ResourceRef(seed.Root.Id.Value, seed.Root.CurrentRevisionId!.Value) } });
        Assert.NotEqual(Guid.Empty, baselineId);
    }

    [Fact]
    public async Task Only_a_project_owner_or_admin_can_lock_a_baseline_or_suspend_drifted_campaigns()
    {
        var seed = await SeedAsync();
        using (var approval = _factory.Services.CreateScope())
        {
            await approval.ServiceProvider.GetRequiredService<PlanDesignService>()
                .AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
            var db = approval.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id)).Approve(seed.Root.CurrentRevisionId!);
            await db.SaveChangesAsync();
        }

        using var scope = _factory.Services.CreateScope();
        var baselines = scope.ServiceProvider.GetRequiredService<IPlanBaselines>();
        var editorScope = new AuthorizedScope(seed.Tenant.Id, seed.Editor.Id, seed.Project.Id, "test", "corr");
        var draft = new BaselineDraft("Goal", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 1);

        // A Member (not Owner/Admin) cannot lock the plan or suspend campaigns on a prior baseline.
        await Assert.ThrowsAsync<NotFoundException>(() => baselines.LockAsync(editorScope, draft));
        await Assert.ThrowsAsync<NotFoundException>(() => baselines.SuspendDriftedCampaignsAsync(editorScope));

        var ownerScope = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "test", "corr");
        var baselineId = await baselines.LockAsync(ownerScope, draft);
        Assert.NotEqual(Guid.Empty, baselineId);
    }

    [Fact]
    public async Task Linking_a_newer_revision_repins_the_work_item_and_clears_the_stale_link_and_unlink_removes_it()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        var item = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id);
        item.Approve(item.CurrentRevisionId!);
        await db.SaveChangesAsync();

        var firstLink = await design.LinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id,
            seed.Work.Id, seed.Root.Id, item.CurrentRevisionId!);

        // A new revision makes the link stale (reported via BuildDriftAsync, exercised through CoordinationService
        // in PlanDesignIntegrationTests' baseline test); here we verify re-pinning clears the old link outright.
        var nextRevision = item.AddRevision("1.0", "{\"summary\":\"Plan v2\"}", seed.Owner.Id, "revised", null, null, item.CurrentRevisionNumber);
        db.Add(nextRevision);
        await db.SaveChangesAsync();
        item.Approve(nextRevision.Id);
        await db.SaveChangesAsync();

        var repinned = await design.LinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id,
            seed.Work.Id, seed.Root.Id, nextRevision.Id);
        Assert.NotEqual(firstLink.Id, repinned.Id);

        var linksAfterRepin = await scope.ServiceProvider.GetRequiredService<IPlanDesignRepository>()
            .ListLinksAsync(seed.Tenant.Id, seed.Project.Id, default);
        var onlyLink = Assert.Single(linksAfterRepin, l => l.WorkItemId == seed.Work.Id && l.KnowledgeItemId == seed.Root.Id);
        Assert.Equal(nextRevision.Id, onlyLink.RevisionId);

        var removed = await design.UnlinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Work.Id, seed.Root.Id);
        Assert.Equal(1, removed);
        var linksAfterUnlink = await scope.ServiceProvider.GetRequiredService<IPlanDesignRepository>()
            .ListLinksAsync(seed.Tenant.Id, seed.Project.Id, default);
        Assert.DoesNotContain(linksAfterUnlink, l => l.WorkItemId == seed.Work.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            design.UnlinkAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Work.Id, seed.Root.Id));
    }

    [Fact]
    public async Task Drift_report_distinguishes_added_changed_and_removed_items_without_cross_contamination()
    {
        var seed = await SeedAsync();
        using var scope = _factory.Services.CreateScope();
        var design = scope.ServiceProvider.GetRequiredService<PlanDesignService>();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var baselines = scope.ServiceProvider.GetRequiredService<IPlanBaselines>();
        var ownerScope = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "test", "corr");

        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id, seed.Root.Id, 0);
        var root = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id);
        var child = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Child.Id);
        root.Approve(root.CurrentRevisionId!);
        child.Approve(child.CurrentRevisionId!);
        await db.SaveChangesAsync();

        var baselineId = await baselines.LockAsync(ownerScope,
            new BaselineDraft("Baseline 1", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 1));

        // Removed: child is taken out of the outline entirely after being baselined.
        await design.DetachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Child.Id);

        // Changed: root gets a new, approved revision.
        var nextRoot = root.AddRevision("1.0", "{\"summary\":\"v2\"}", seed.Owner.Id, "revised", null, null, root.CurrentRevisionNumber);
        db.Add(nextRoot);
        await db.SaveChangesAsync();
        root.Approve(nextRoot.Id);
        await db.SaveChangesAsync();

        // Added: a brand-new item, approved and attached to the outline for the first time.
        var extra = KnowledgeItem.Create(seed.Tenant.Id, seed.Project.Id, $"extra-{Guid.NewGuid():N}", "extra",
            Classification.Internal, RepresentationKind.Text, "doc.note", "1.0", seed.Owner.Id);
        var extraRevision = extra.AddRevision("1.0", "{\"summary\":\"new\"}", seed.Owner.Id, "new idea", null, null, 0);
        db.Add(extra);
        db.Add(extraRevision);
        await db.SaveChangesAsync();
        await design.AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, extra.Id, root.Id, 0);
        extra.Approve(extraRevision.Id);
        await db.SaveChangesAsync();

        var preview = await baselines.PreviewLockAsync(ownerScope);

        Assert.True(preview.Drift.HasDrift);
        Assert.Equal(baselineId, preview.Drift.LastBaselineId);
        Assert.Equal(1, preview.Drift.LastPlanVersion);
        Assert.Equal(new[] { extra.Key }, preview.Drift.AddedKeys);
        Assert.Equal(new[] { root.Key }, preview.Drift.ChangedKeys);
        Assert.Equal(new[] { child.Key }, preview.Drift.RemovedKeys);

        // Each item appears in exactly one list; the original bug conflated added/changed and cleared removed.
        Assert.DoesNotContain(root.Key, preview.Drift.AddedKeys);
        Assert.DoesNotContain(extra.Key, preview.Drift.ChangedKeys);
        Assert.DoesNotContain(child.Key, preview.Drift.ChangedKeys);
    }

    [Fact]
    public async Task Work_package_scope_comes_from_its_plan_version_and_nothing_progresses_after_a_newer_version_locks()
    {
        var seed = await SeedAsync();
        var owner = new AuthorizedScope(seed.Tenant.Id, seed.Owner.Id, seed.Project.Id, "test", "corr");
        var runtimeId = Guid.NewGuid();

        // Plan version 1: the approved root.
        using (var setup = _factory.Services.CreateScope())
        {
            await setup.ServiceProvider.GetRequiredService<PlanDesignService>()
                .AttachAsync(seed.Tenant.Id, seed.Project.Id, seed.Owner.Id, seed.Root.Id, null, 0);
            var db = setup.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            (await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id)).Approve(seed.Root.CurrentRevisionId!);
            await db.SaveChangesAsync();
        }

        Guid campaignId, packageId, leaseId, targetId, queueItemId;
        long fence;
        using (var scope = _factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var baselineId = await services.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 1", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 1));
            campaignId = await services.GetRequiredService<ICampaignCoordinator>().CreateAsync(owner,
                new CampaignDraft(baselineId, Array.Empty<Guid>(), "Build version 1", "Mediated", "camp-v1"));
            var scheduler = services.GetRequiredService<IParticipationScheduler>();

            // Scope must be content of this plan version, real work items and known specification topics.
            await Assert.ThrowsAsync<ConflictException>(() => scheduler.CreatePackageAsync(owner,
                new PackageDraft(campaignId, baselineId, "Outside the plan", [], [], [], [Guid.NewGuid()])));
            await Assert.ThrowsAsync<NotFoundException>(() => scheduler.CreatePackageAsync(owner,
                new PackageDraft(campaignId, baselineId, "Unknown topic", [], [], [], SpecTopics: ["no.such.topic"])));

            packageId = await scheduler.CreatePackageAsync(owner, new PackageDraft(
                campaignId, baselineId, "Implement the idea", [], [], [],
                [seed.Root.CurrentRevisionId!.Value], [seed.Work.Id.Value], ["backend.language"]));
            var package = await services.GetRequiredService<ICoordinationRepository>()
                .GetWorkPackageByIdAsync(seed.Tenant.Id, new WorkPackageId(packageId));
            Assert.Equal([seed.Root.CurrentRevisionId!.Value], package!.ScopeRevisionIds);
            Assert.False(package.IsUnscoped);

            var leases = services.GetRequiredService<ILeaseAuthority>();
            var lease = await leases.AcquireAsync(owner, new LeaseRequest(
                packageId, runtimeId, "src/app", DateTimeOffset.UtcNow.AddMinutes(10), 0, "lease-1"));
            leaseId = lease.Id;
            fence = lease.Fence;

            var registry = services.GetRequiredService<ICodebaseRegistry>();
            targetId = await registry.RegisterAsync(owner, "github", "acme/app", "main", "abc1234");
            var changeSetId = await registry.RecordChangeSetAsync(owner,
                new ChangeSetDraft(packageId, targetId, "github:pr/1", "def5678", ["src/app/Program.cs"]));
            queueItemId = await services.GetRequiredService<IIntegrationQueue>()
                .EnqueueAsync(owner, changeSetId, $"queue-{Guid.NewGuid():N}");
        }

        // The Owner changes the plan and locks version 2, which suspends the version-1 campaign.
        using (var change = _factory.Services.CreateScope())
        {
            var db = change.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var root = await db.KnowledgeItems.SingleAsync(i => i.Id == seed.Root.Id);
            var next = root.AddRevision("1.0", "{\"summary\":\"Changed idea\"}", seed.Owner.Id, "changed", null, null, root.CurrentRevisionNumber);
            db.Add(next);
            await db.SaveChangesAsync();
            root.Approve(next.Id);
            await db.SaveChangesAsync();
            await change.ServiceProvider.GetRequiredService<IPlanBaselines>().LockAsync(owner,
                new BaselineDraft("Version 2", Array.Empty<ResourceRef>(), Array.Empty<Guid>(), new[] { "criterion" }, 2));
        }

        // Nothing on the old version can progress any more, and work already queued is refused, not merged.
        using (var after = _factory.Services.CreateScope())
        {
            var services = after.ServiceProvider;
            var leases = services.GetRequiredService<ILeaseAuthority>();
            await Assert.ThrowsAsync<ConflictException>(() => leases.AcquireAsync(owner, new LeaseRequest(
                packageId, runtimeId, "src/other", DateTimeOffset.UtcNow.AddMinutes(10), 0, "lease-2")));
            await Assert.ThrowsAsync<ConflictException>(() =>
                leases.RenewAsync(owner, leaseId, fence, DateTimeOffset.UtcNow.AddMinutes(20)));
            await Assert.ThrowsAsync<ConflictException>(() => services.GetRequiredService<ICodebaseRegistry>()
                .RecordChangeSetAsync(owner, new ChangeSetDraft(packageId, targetId, "github:pr/2", "aaa1111", ["src/app/x.cs"])));

            var decision = await services.GetRequiredService<IIntegrationQueue>().DecideAsync(owner, queueItemId, fence);
            Assert.Equal("Rejected", decision.Outcome);
            Assert.Contains("suspended", decision.Reason);

            // Releasing a lease is always allowed, so agents can let go cleanly.
            await leases.ReleaseAsync(owner, leaseId, fence);
        }

        // The REST surface Studio reads from reports the same status.
        using var client = _factory.CreateAuthenticatedHumanClient(seed.Tenant.Id.Value, seed.Owner.Id.Value);
        var base_ = $"/tenants/{seed.Tenant.Id.Value}/projects/{seed.Project.Id.Value}/coordination";

        using var planStatus = await client.GetAsync($"{base_}/plan-status");
        Assert.Equal(HttpStatusCode.OK, planStatus.StatusCode);
        using var planDoc = await planStatus.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(2, planDoc!.RootElement.GetProperty("currentPlanVersion").GetInt32());
        var campaignStatus = Assert.Single(planDoc.RootElement.GetProperty("campaigns").EnumerateArray());
        Assert.False(campaignStatus.GetProperty("onCurrentPlan").GetBoolean());
        Assert.Equal("Suspended", campaignStatus.GetProperty("status").GetString());

        using var workStatus = await client.GetAsync($"{base_}/packages/{packageId}/status");
        Assert.Equal(HttpStatusCode.OK, workStatus.StatusCode);
        using var workDoc = await workStatus.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.False(workDoc!.RootElement.GetProperty("canProgress").GetBoolean());
        Assert.Contains("suspended", workDoc.RootElement.GetProperty("blockedReason").GetString());
        Assert.False(workDoc.RootElement.GetProperty("unscoped").GetBoolean());

        using var missing = await client.GetAsync($"{base_}/packages/{Guid.NewGuid()}/status");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        using var listed = await client.GetAsync($"{base_}/campaigns/{campaignId}/packages");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listedDoc = await listed.Content.ReadFromJsonAsync<JsonDocument>();
        var listedPackage = Assert.Single(listedDoc!.RootElement.EnumerateArray());
        Assert.False(listedPackage.GetProperty("canProgress").GetBoolean());
    }

    private async Task<Seed> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var tenant = Tenant.Create($"Plan {Guid.NewGuid():N}");
        var owner = Principal.Create(tenant.Id, $"owner-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var editor = Principal.Create(tenant.Id, $"editor-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var reviewer = Principal.Create(tenant.Id, $"reviewer-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
        var profile = LifecycleProfile.Create(tenant.Id, Guid.NewGuid().ToString("N"), "Plan", "1");
        var project = Project.Create(tenant.Id, "App", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        var other = Project.Create(tenant.Id, "Other", Guid.NewGuid().ToString("N"), null, profile.Id, owner.Id);
        db.AddRange(tenant, owner, editor, reviewer, profile, project, other);
        db.AddRange(ProjectMembership.Grant(tenant.Id, project.Id, owner.Id, ProjectRole.Owner),
            ProjectMembership.Grant(tenant.Id, project.Id, editor.Id, ProjectRole.Member),
            ProjectMembership.Grant(tenant.Id, project.Id, reviewer.Id, ProjectRole.Admin));
        var root = NewItem(tenant.Id, project.Id, owner.Id, "idea");
        var child = NewItem(tenant.Id, project.Id, owner.Id, "module");
        var outside = NewItem(tenant.Id, other.Id, owner.Id, "outside");
        var revisions = new[] { NewRevision(root, owner.Id), NewRevision(child, owner.Id), NewRevision(outside, owner.Id) };
        var work = WorkItem.Create(tenant.Id, project.Id, "work", "Deliver module", "Build it", null, null, null,
            WorkPriority.Normal, owner.Id);
        db.AddRange(root, child, outside, work);
        db.AddRange(revisions);
        await db.SaveChangesAsync();
        return new(tenant, project, owner, editor, reviewer, root, child, outside, work);
    }

    private static KnowledgeItem NewItem(TenantId tenant, ProjectId project, PrincipalId actor, string key) =>
        KnowledgeItem.Create(tenant, project, $"{key}-{Guid.NewGuid():N}", key, Classification.Internal,
            RepresentationKind.Text, "doc.note", "1.0", actor);

    private static KnowledgeRevision NewRevision(KnowledgeItem item, PrincipalId actor) =>
        item.AddRevision("1.0", "{\"summary\":\"Plan\"}", actor, "Initial idea", null, null, 0);

    private sealed record Seed(Tenant Tenant, Project Project, Principal Owner, Principal Editor,
        Principal Reviewer, KnowledgeItem Root, KnowledgeItem Child, KnowledgeItem OtherProjectItem, WorkItem Work);
}
