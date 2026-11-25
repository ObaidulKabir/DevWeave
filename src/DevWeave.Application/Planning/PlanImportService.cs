using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;
using DevWeave.Application.Knowledge;
using DevWeave.Application.Lifecycle;
using DevWeave.Application.Relations;
using DevWeave.Contracts.Planning.V1;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

public sealed class PlanImportService(
    IInitialPlanProposalRepository proposals,
    IPlanDesignRepository plans,
    IProjectRepository projects,
    IPrincipalRepository principals,
    IProjectMembershipRepository memberships,
    IMembershipRepository tenantMemberships,
    IAgentGrantRepository grants,
    IAgentCredentialRepository credentials,
    ISchemaDefinitionRepository schemas,
    IPayloadValidator payloads,
    IRelationTypeRepository relationTypes,
    IContentInspector inspector,
    CreateKnowledgeItem createItem,
    AddRevision addRevision,
    ReviewKnowledge review,
    CreateRelationship createRelationship,
    CreateMilestone createMilestone,
    CreateWorkItem createWorkItem,
    PlanDesignService planDesign,
    ICodebaseRegistry codebases,
    IRepositoryReferenceAdapter git,
    IUnitOfWork uow)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const int MaxKnowledge = 100;
    private const int MaxOutline = 100;
    private const int MaxRelations = 200;
    private const int MaxMilestones = 50;
    private const int MaxWorkItems = 200;
    private const int MaxCodebaseTargets = 10;
    private const int MaxContentChars = 100_000;

    public async Task<InitialPlanProposal> ProposeAsync(
        TenantId tenant,
        ProjectId project,
        PrincipalId actor,
        InitialPlanProposalV1 payload,
        CancellationToken ct = default)
    {
        var (principal, ceiling) = await AuthorizeAgentWriteAsync(tenant, project, actor, ct);
        _ = principal;

        var existing = await proposals.FindByIdempotencyAsync(tenant, project, actor, payload.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.Status != InitialPlanProposalStatus.Proposed)
                throw new ConflictException("Idempotency key was already used for a decided plan import.");
            var existingPayload = Deserialize(existing.PayloadJson);
            if (!PayloadEquals(existingPayload, payload))
                throw new ConflictException("Idempotency key was reused with a different plan payload.");
            return existing;
        }

        await ValidatePayloadAsync(tenant, project, payload, ceiling, ct);

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var inspection = await inspector.InspectAsync("", "application/json", bytes, ct);
        if (inspection.Verdict != ContentVerdict.Allow)
            throw new ProhibitedContentException("Plan import payload was rejected by content policy.", inspection.Reasons);

        var proposal = InitialPlanProposal.Propose(
            tenant, project, payload.IdempotencyKey, json, payload.RequireEmptyProject, actor);
        await proposals.AddAsync(proposal, ct);
        await uow.SaveChangesAsync(ct);
        return proposal;
    }

    public async Task<IReadOnlyList<InitialPlanProposal>> ListAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct = default)
    {
        await AuthorizeHumanReaderAsync(tenant, project, actor, ct);
        return await proposals.ListAsync(tenant, project, ct);
    }

    public async Task<InitialPlanProposal> GetAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, InitialPlanProposalId id, CancellationToken ct = default)
    {
        await AuthorizeHumanReaderAsync(tenant, project, actor, ct);
        return await proposals.GetAsync(tenant, project, id, ct)
            ?? throw new NotFoundException("Plan import proposal was not found.");
    }

    public async Task<(InitialPlanProposal Proposal, IReadOnlyDictionary<string, Guid> Mapping)> ApproveAsync(
        TenantId tenant,
        ProjectId project,
        PrincipalId actor,
        InitialPlanProposalId id,
        string? note,
        CancellationToken ct = default)
    {
        await AuthorizeHumanReviewerAsync(tenant, project, actor, ct);
        var proposal = await proposals.GetAsync(tenant, project, id, ct)
            ?? throw new NotFoundException("Plan import proposal was not found.");
        if (proposal.Status != InitialPlanProposalStatus.Proposed)
            throw new ConflictException("Plan import proposal has already been decided.");
        if (proposal.ProposedBy == actor)
            throw new ConflictException("A proposer cannot approve their own plan import.");

        var payload = Deserialize(proposal.PayloadJson);
        await ValidatePayloadAsync(tenant, project, payload, Classification.Restricted, ct);

        var mapping = await MaterializeAsync(tenant, project, actor, payload, ct);
        proposal.Approve(actor, JsonSerializer.Serialize(mapping, JsonOptions), note);
        await uow.SaveChangesAsync(ct);
        return (proposal, mapping);
    }

    public async Task<InitialPlanProposal> RejectAsync(
        TenantId tenant,
        ProjectId project,
        PrincipalId actor,
        InitialPlanProposalId id,
        string? note,
        CancellationToken ct = default)
    {
        await AuthorizeHumanReviewerAsync(tenant, project, actor, ct);
        var proposal = await proposals.GetAsync(tenant, project, id, ct)
            ?? throw new NotFoundException("Plan import proposal was not found.");
        if (proposal.Status != InitialPlanProposalStatus.Proposed)
            throw new ConflictException("Plan import proposal has already been decided.");
        if (proposal.ProposedBy == actor)
            throw new ConflictException("A proposer cannot reject their own plan import.");
        proposal.Reject(actor, note);
        await uow.SaveChangesAsync(ct);
        return proposal;
    }

    public InitialPlanProposalV1 Deserialize(string payloadJson) =>
        JsonSerializer.Deserialize<InitialPlanProposalV1>(payloadJson, JsonOptions)
        ?? throw new ArgumentException("Plan import payload is not valid JSON.");

    public InitialPlanProposalSummaryResponse ToSummary(InitialPlanProposal proposal)
    {
        var payload = Deserialize(proposal.PayloadJson);
        return new InitialPlanProposalSummaryResponse(
            proposal.Id.Value,
            proposal.IdempotencyKey,
            proposal.PayloadHash,
            proposal.RequireEmptyProject,
            proposal.Status.ToString(),
            proposal.ProposedBy.Value,
            proposal.ProposedAt,
            proposal.ReviewedBy?.Value,
            proposal.ReviewedAt,
            proposal.DecisionNote,
            payload.Knowledge.Count,
            payload.Outline.Count,
            payload.Relations.Count,
            payload.Milestones.Count,
            payload.WorkItems.Count,
            payload.CodebaseTargets?.Count ?? 0);
    }

    public InitialPlanProposalDetailResponse ToDetail(InitialPlanProposal proposal)
    {
        var payload = Deserialize(proposal.PayloadJson);
        IReadOnlyDictionary<string, Guid>? mapping = null;
        if (!string.IsNullOrWhiteSpace(proposal.ResultMappingJson))
            mapping = JsonSerializer.Deserialize<Dictionary<string, Guid>>(proposal.ResultMappingJson, JsonOptions);
        return new InitialPlanProposalDetailResponse(
            proposal.Id.Value,
            proposal.IdempotencyKey,
            proposal.PayloadHash,
            proposal.RequireEmptyProject,
            proposal.Status.ToString(),
            proposal.ProposedBy.Value,
            proposal.ProposedAt,
            proposal.ReviewedBy?.Value,
            proposal.ReviewedAt,
            proposal.DecisionNote,
            payload,
            mapping);
    }

    private async Task<Dictionary<string, Guid>> MaterializeAsync(
        TenantId tenant,
        ProjectId project,
        PrincipalId actor,
        InitialPlanProposalV1 payload,
        CancellationToken ct)
    {
        var mapping = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var knowledgeIds = new Dictionary<string, KnowledgeItemId>(StringComparer.Ordinal);
        var revisionIds = new Dictionary<string, KnowledgeRevisionId>(StringComparer.Ordinal);

        foreach (var draft in payload.Knowledge)
        {
            if (!Enum.TryParse<Classification>(draft.Classification, ignoreCase: true, out var classification))
                throw new ArgumentException($"Unknown classification '{draft.Classification}'.");
            if (!Enum.TryParse<RepresentationKind>(draft.RepresentationKind, ignoreCase: true, out var representation))
                throw new ArgumentException($"Unknown representation '{draft.RepresentationKind}'.");

            var item = await createItem.ExecuteAsync(
                tenant, project, draft.Key, draft.Title, classification, representation,
                draft.TypeId, draft.SchemaVersion, actor, ct, draft.Description);
            var revision = await addRevision.ExecuteAsync(
                tenant, item.Id, 0, draft.SchemaVersion, draft.Content, actor,
                draft.Reason, null, null, ct);
            await review.ApproveAsync(tenant, item.Id, revision.Id, actor, "Approved via plan import.", ct);
            knowledgeIds[draft.Ref] = item.Id;
            revisionIds[draft.Ref] = revision.Id;
            mapping[$"knowledge:{draft.Ref}"] = item.Id.Value;
            mapping[$"revision:{draft.Ref}"] = revision.Id.Value;
        }

        foreach (var node in payload.Outline.OrderBy(n => n.ParentRef is null ? 0 : 1).ThenBy(n => n.Position))
        {
            var itemId = knowledgeIds[node.ItemRef];
            KnowledgeItemId? parentId = node.ParentRef is null ? null : knowledgeIds[node.ParentRef];
            await planDesign.AttachAsync(tenant, project, actor, itemId, parentId, node.Position, ct);
        }

        foreach (var edge in payload.Relations)
        {
            var relationship = await createRelationship.ExecuteAsync(
                tenant, project, knowledgeIds[edge.SourceRef], knowledgeIds[edge.TargetRef],
                edge.RelationTypeCode, actor, ct);
            mapping[$"relation:{edge.SourceRef}->{edge.TargetRef}:{edge.RelationTypeCode}"] = relationship.Id.Value;
        }

        var milestoneIds = new Dictionary<string, MilestoneId>(StringComparer.Ordinal);
        foreach (var milestone in payload.Milestones)
        {
            var created = await createMilestone.ExecuteAsync(
                tenant, project, milestone.Key, milestone.Title, milestone.DueAt, actor, ct);
            milestoneIds[milestone.Ref] = created.Id;
            mapping[$"milestone:{milestone.Ref}"] = created.Id.Value;
        }

        var workIds = new Dictionary<string, WorkItemId>(StringComparer.Ordinal);
        foreach (var work in OrderWorkItems(payload.WorkItems))
        {
            if (!Enum.TryParse<WorkPriority>(work.Priority, ignoreCase: true, out var priority))
                throw new ArgumentException($"Unknown work priority '{work.Priority}'.");
            MilestoneId? milestoneId = work.MilestoneRef is null ? null : milestoneIds[work.MilestoneRef];
            var dependsOn = (work.DependsOnRefs ?? Array.Empty<string>())
                .Select(r => workIds[r])
                .ToArray();
            var created = await createWorkItem.ExecuteAsync(
                tenant, project, work.Key, work.Title, work.Description, milestoneId, null,
                null, priority, actor, dependsOn, ct);
            workIds[work.Ref] = created.Id;
            mapping[$"work:{work.Ref}"] = created.Id.Value;
        }

        foreach (var link in payload.WorkLinks ?? Array.Empty<PlanImportWorkLinkDraftV1>())
        {
            await planDesign.LinkAsync(
                tenant, project, actor, workIds[link.WorkItemRef], knowledgeIds[link.KnowledgeRef],
                revisionIds[link.KnowledgeRef], ct);
        }

        var scope = new AuthorizedScope(tenant, actor, project, "Plan import codebase target", "plan-import");
        foreach (var target in payload.CodebaseTargets ?? Array.Empty<PlanImportCodebaseTargetDraftV1>())
        {
            var id = await codebases.RegisterAsync(
                scope, target.Provider, target.RepositoryId, target.TargetBranch, target.BaseCommitSha, ct);
            mapping[$"codebase:{target.Ref}"] = id;
        }

        return mapping;
    }

    private async Task ValidatePayloadAsync(
        TenantId tenant,
        ProjectId project,
        InitialPlanProposalV1 payload,
        Classification ceiling,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload.IdempotencyKey))
            throw new ArgumentException("idempotencyKey is required.");
        if (payload.Knowledge is null || payload.Knowledge.Count == 0)
            throw new ArgumentException("At least one knowledge item is required.");
        if (payload.Knowledge.Count > MaxKnowledge)
            throw new ArgumentException($"At most {MaxKnowledge} knowledge items are allowed.");
        if ((payload.Outline?.Count ?? 0) > MaxOutline)
            throw new ArgumentException($"At most {MaxOutline} outline nodes are allowed.");
        if ((payload.Relations?.Count ?? 0) > MaxRelations)
            throw new ArgumentException($"At most {MaxRelations} relations are allowed.");
        if ((payload.Milestones?.Count ?? 0) > MaxMilestones)
            throw new ArgumentException($"At most {MaxMilestones} milestones are allowed.");
        if ((payload.WorkItems?.Count ?? 0) > MaxWorkItems)
            throw new ArgumentException($"At most {MaxWorkItems} work items are allowed.");
        if ((payload.CodebaseTargets?.Count ?? 0) > MaxCodebaseTargets)
            throw new ArgumentException($"At most {MaxCodebaseTargets} codebase targets are allowed.");

        if (payload.RequireEmptyProject)
        {
            var nodes = await plans.ListNodesAsync(tenant, project, ct);
            if (nodes.Count > 0)
                throw new ConflictException("Project outline is not empty; set requireEmptyProject to false to append.");
        }

        var knowledgeRefs = new HashSet<string>(StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var draft in payload.Knowledge)
        {
            RequireRef(draft.Ref, "knowledge");
            if (!knowledgeRefs.Add(draft.Ref))
                throw new ArgumentException($"Duplicate knowledge ref '{draft.Ref}'.");
            if (string.IsNullOrWhiteSpace(draft.Key) || !keys.Add(draft.Key.Trim()))
                throw new ArgumentException($"Knowledge key '{draft.Key}' is missing or duplicated.");
            if (string.IsNullOrWhiteSpace(draft.Title) || string.IsNullOrWhiteSpace(draft.TypeId) ||
                string.IsNullOrWhiteSpace(draft.SchemaVersion) || string.IsNullOrWhiteSpace(draft.Content) ||
                string.IsNullOrWhiteSpace(draft.Reason))
                throw new ArgumentException($"Knowledge '{draft.Ref}' is missing required fields.");
            if (draft.Content.Length > MaxContentChars)
                throw new ArgumentException($"Knowledge '{draft.Ref}' content exceeds {MaxContentChars} characters.");
            if (!Enum.TryParse<Classification>(draft.Classification, ignoreCase: true, out var classification))
                throw new ArgumentException($"Unknown classification '{draft.Classification}'.");
            if (classification > ceiling)
                throw new UnauthorizedTenantException($"Knowledge '{draft.Ref}' exceeds the agent classification ceiling.");
            if (!Enum.TryParse<RepresentationKind>(draft.RepresentationKind, ignoreCase: true, out var representation))
                throw new ArgumentException($"Unknown representation '{draft.RepresentationKind}'.");
            if (representation == RepresentationKind.SourceCode)
                throw new ArgumentException("SourceCode representation is not allowed in plan import.");

            var schema = await schemas.FindAsync(draft.TypeId, draft.SchemaVersion, ct);
            if (schema is null || !schema.IsActive)
                throw new NotFoundException($"No active schema '{draft.TypeId}' version '{draft.SchemaVersion}' is registered.");
            var issues = payloads.Validate(draft.Content, schema.Document);
            if (issues.Count > 0)
                throw new PayloadValidationException(issues);
        }

        if (payload.Outline is null || payload.Outline.Count == 0)
            throw new ArgumentException("Outline must include at least a root node.");

        var outlineRefs = new HashSet<string>(StringComparer.Ordinal);
        var roots = 0;
        foreach (var node in payload.Outline)
        {
            RequireRef(node.ItemRef, "outline");
            if (!knowledgeRefs.Contains(node.ItemRef))
                throw new ArgumentException($"Outline itemRef '{node.ItemRef}' is not in knowledge.");
            if (!outlineRefs.Add(node.ItemRef))
                throw new ArgumentException($"Outline lists '{node.ItemRef}' more than once.");
            if (node.ParentRef is null)
            {
                roots++;
            }
            else
            {
                if (!knowledgeRefs.Contains(node.ParentRef))
                    throw new ArgumentException($"Outline parentRef '{node.ParentRef}' is not in knowledge.");
                if (node.ParentRef == node.ItemRef)
                    throw new ArgumentException("Outline node cannot parent itself.");
            }
            if (node.Position < 0)
                throw new ArgumentException("Outline position must be nonnegative.");
        }
        if (roots != 1)
            throw new ArgumentException("Outline must have exactly one root (null parentRef).");
        EnsureOutlineConnected(payload.Outline);

        foreach (var edge in payload.Relations ?? Array.Empty<PlanImportRelationDraftV1>())
        {
            if (!knowledgeRefs.Contains(edge.SourceRef) || !knowledgeRefs.Contains(edge.TargetRef))
                throw new ArgumentException($"Relation endpoints must reference knowledge refs.");
            var type = await relationTypes.GetByCodeAsync(edge.RelationTypeCode, ct)
                ?? throw new NotFoundException($"Relation type '{edge.RelationTypeCode}' is not registered.");
            if (!type.IsActive)
                throw new ConflictException($"Relation type '{edge.RelationTypeCode}' is not active.");
            var sourceTypeId = payload.Knowledge.First(item => item.Ref == edge.SourceRef).TypeId;
            var targetTypeId = payload.Knowledge.First(item => item.Ref == edge.TargetRef).TypeId;
            if (!type.Accepts(sourceTypeId, targetTypeId))
                throw new ArgumentException(
                    $"Relation type '{edge.RelationTypeCode}' does not allow '{sourceTypeId}' to '{targetTypeId}'.");
        }

        var milestoneRefs = new HashSet<string>(StringComparer.Ordinal);
        var milestoneKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var milestone in payload.Milestones ?? Array.Empty<PlanImportMilestoneDraftV1>())
        {
            RequireRef(milestone.Ref, "milestone");
            if (!milestoneRefs.Add(milestone.Ref))
                throw new ArgumentException($"Duplicate milestone ref '{milestone.Ref}'.");
            if (string.IsNullOrWhiteSpace(milestone.Key) || !milestoneKeys.Add(milestone.Key.Trim()))
                throw new ArgumentException($"Milestone key '{milestone.Key}' is missing or duplicated.");
        }

        var workRefs = new HashSet<string>(StringComparer.Ordinal);
        var workKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var work in payload.WorkItems ?? Array.Empty<PlanImportWorkItemDraftV1>())
        {
            RequireRef(work.Ref, "work");
            if (!workRefs.Add(work.Ref))
                throw new ArgumentException($"Duplicate work ref '{work.Ref}'.");
            if (string.IsNullOrWhiteSpace(work.Key) || !workKeys.Add(work.Key.Trim()))
                throw new ArgumentException($"Work key '{work.Key}' is missing or duplicated.");
            if (work.MilestoneRef is not null && !milestoneRefs.Contains(work.MilestoneRef))
                throw new ArgumentException($"Work '{work.Ref}' milestoneRef is unknown.");
            if (!Enum.TryParse<WorkPriority>(work.Priority, ignoreCase: true, out _))
                throw new ArgumentException($"Unknown work priority '{work.Priority}'.");
        }
        EnsureWorkAcyclic(payload.WorkItems ?? Array.Empty<PlanImportWorkItemDraftV1>());

        foreach (var link in payload.WorkLinks ?? Array.Empty<PlanImportWorkLinkDraftV1>())
        {
            if (!workRefs.Contains(link.WorkItemRef))
                throw new ArgumentException($"Work link workItemRef '{link.WorkItemRef}' is unknown.");
            if (!knowledgeRefs.Contains(link.KnowledgeRef))
                throw new ArgumentException($"Work link knowledgeRef '{link.KnowledgeRef}' is unknown.");
            if (!(payload.Outline?.Any(o => o.ItemRef == link.KnowledgeRef) ?? false))
                throw new ArgumentException($"Work link knowledgeRef '{link.KnowledgeRef}' must be in the outline.");
        }

        var codebaseRefs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in payload.CodebaseTargets ?? Array.Empty<PlanImportCodebaseTargetDraftV1>())
        {
            RequireRef(target.Ref, "codebase");
            if (!codebaseRefs.Add(target.Ref))
                throw new ArgumentException($"Duplicate codebase ref '{target.Ref}'.");
            if (string.IsNullOrWhiteSpace(target.Provider) ||
                string.IsNullOrWhiteSpace(target.RepositoryId) ||
                string.IsNullOrWhiteSpace(target.TargetBranch) ||
                string.IsNullOrWhiteSpace(target.BaseCommitSha))
            {
                throw new ArgumentException($"Codebase '{target.Ref}' is missing provider, repositoryId, targetBranch, or baseCommitSha.");
            }

            if (string.Equals(target.Provider, git.Provider, StringComparison.OrdinalIgnoreCase))
                await git.ResolveAsync(target.RepositoryId, target.TargetBranch, target.BaseCommitSha, ct);
        }
    }

    private static void EnsureOutlineConnected(IReadOnlyList<PlanImportOutlineDraftV1> outline)
    {
        var byRef = outline.ToDictionary(n => n.ItemRef, StringComparer.Ordinal);
        var root = outline.Single(n => n.ParentRef is null).ItemRef;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Walk(string itemRef)
        {
            if (!visited.Add(itemRef))
                throw new ArgumentException("Outline contains a cycle.");
            foreach (var child in outline.Where(n => n.ParentRef == itemRef))
                Walk(child.ItemRef);
        }
        Walk(root);
        if (visited.Count != byRef.Count)
            throw new ArgumentException("Outline must be a single connected tree from the root.");
    }

    private static void EnsureWorkAcyclic(IReadOnlyList<PlanImportWorkItemDraftV1> workItems)
    {
        var byRef = workItems.ToDictionary(w => w.Ref, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Dfs(string itemRef)
        {
            if (visited.Contains(itemRef)) return;
            if (!visiting.Add(itemRef))
                throw new ArgumentException("Work item dependencies contain a cycle.");
            foreach (var dep in byRef[itemRef].DependsOnRefs ?? Array.Empty<string>())
            {
                if (!byRef.ContainsKey(dep))
                    throw new ArgumentException($"Work dependency '{dep}' is unknown.");
                Dfs(dep);
            }
            visiting.Remove(itemRef);
            visited.Add(itemRef);
        }
        foreach (var work in workItems)
            Dfs(work.Ref);
    }

    private static IEnumerable<PlanImportWorkItemDraftV1> OrderWorkItems(IReadOnlyList<PlanImportWorkItemDraftV1> workItems)
    {
        var remaining = workItems.ToDictionary(w => w.Ref, StringComparer.Ordinal);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var ready = new Queue<PlanImportWorkItemDraftV1>(
            remaining.Values.Where(w => (w.DependsOnRefs ?? Array.Empty<string>()).Count == 0));
        while (ready.Count > 0)
        {
            var next = ready.Dequeue();
            if (!emitted.Add(next.Ref))
                continue;
            yield return next;
            foreach (var candidate in remaining.Values)
            {
                if (emitted.Contains(candidate.Ref))
                    continue;
                var deps = candidate.DependsOnRefs ?? Array.Empty<string>();
                if (deps.All(emitted.Contains))
                    ready.Enqueue(candidate);
            }
        }
        if (emitted.Count != remaining.Count)
            throw new ArgumentException("Work item dependencies could not be ordered.");
    }

    private static void RequireRef(string value, string kind)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100)
            throw new ArgumentException($"{kind} ref is required (max 100 characters).");
    }

    private static bool PayloadEquals(InitialPlanProposalV1 left, InitialPlanProposalV1 right) =>
        string.Equals(
            JsonSerializer.Serialize(left, JsonOptions),
            JsonSerializer.Serialize(right, JsonOptions),
            StringComparison.Ordinal);

    private async Task<(Principal Principal, Classification Ceiling)> AuthorizeAgentWriteAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct)
    {
        _ = await projects.GetByIdAsync(tenant, project, ct)
            ?? throw new NotFoundException("Project was not found.");
        var principal = await principals.GetByIdAsync(tenant, actor, ct);
        if (principal is null || !principal.IsActive)
            throw new NotFoundException("Actor was not found.");
        var member = (await memberships.ListByPrincipalAsync(tenant, actor, ct))
            .FirstOrDefault(m => m.ProjectId == project && m.Status == ProjectMembershipStatus.Active)
            ?? throw new NotFoundException("Project was not found.");
        if (principal.Type != PrincipalType.AiAgent)
            throw new UnauthorizedTenantException("Only planning agents may submit plan import proposals via MCP.");
        _ = member;
        if (!(await credentials.ListForAgentAsync(tenant, actor, ct)).Any(c => c.IsActive(DateTimeOffset.UtcNow)))
            throw new UnauthorizedTenantException("Agent has no active credential.");
        var authorized = (await grants.ListForAgentAsync(tenant, actor, ct))
            .Where(g => g.IsActive(DateTimeOffset.UtcNow) && g.CoversProject(project) &&
                        g.Capability == AgentCapability.WriteKnowledge)
            .ToArray();
        if (authorized.Length == 0)
            throw new UnauthorizedTenantException("Agent lacks WriteKnowledge for plan import.");
        return (principal, authorized.Max(g => g.ClassificationCeiling));
    }

    private async Task AuthorizeHumanReaderAsync(TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct)
    {
        _ = await projects.GetByIdAsync(tenant, project, ct)
            ?? throw new NotFoundException("Project was not found.");
        var principal = await principals.GetByIdAsync(tenant, actor, ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException("Only humans can read plan import proposals.");
        await ProjectWorkAuthorization.EnsureMemberAsync(
            memberships, tenant, project, actor, reviewerOnly: false, ct, tenantMemberships);
    }

    private async Task AuthorizeHumanReviewerAsync(TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct)
    {
        _ = await projects.GetByIdAsync(tenant, project, ct)
            ?? throw new NotFoundException("Project was not found.");
        var principal = await principals.GetByIdAsync(tenant, actor, ct);
        if (principal is null || !principal.IsActive || principal.Type != PrincipalType.Human)
            throw new UnauthorizedTenantException("Only humans can decide plan import proposals.");
        await ProjectWorkAuthorization.EnsureMemberAsync(
            memberships, tenant, project, actor, reviewerOnly: true, ct, tenantMemberships);
    }
}
