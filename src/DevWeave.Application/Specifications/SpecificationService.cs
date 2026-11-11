using DevWeave.Application.Abstractions;
using DevWeave.Application.Knowledge;
using DevWeave.Domain.Agents;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Schema;
using DevWeave.Domain.Specifications;

namespace DevWeave.Application.Specifications;

/// <summary>The effective specification of a project: one binding decision per topic, plus problems.</summary>
public sealed record EffectiveSpecification(
    IReadOnlyList<SpecResolution> Resolutions,
    IReadOnlyList<SpecIssue> Incompatibilities,
    IReadOnlyList<string> UndecidedTopics);

public sealed class SpecDecisionRejectedException(IReadOnlyList<SpecIssue> issues)
    : Exception("The decision does not satisfy its topic definition.")
{
    public IReadOnlyList<SpecIssue> Issues { get; } = issues;
}

/// <summary>
/// Resolves and proposes specification decisions. Decisions are ordinary knowledge items of type
/// <c>spec.decision</c>, so they reuse revisioning, human review and audit; only a human-approved,
/// current revision is binding. Agents may propose but never approve (KNW-003).
/// </summary>
public sealed class SpecificationService(
    IKnowledgeItemRepository items,
    IKnowledgeRevisionRepository revisions,
    ISchemaDefinitionRepository schemas,
    CreateKnowledgeItem createItem,
    AddRevision addRevision,
    IProjectRepository projects,
    IPrincipalRepository principals,
    IProjectMembershipRepository memberships,
    IAgentGrantRepository grants,
    IAgentCredentialRepository credentials,
    IUnitOfWork uow)
{
    public IReadOnlyList<SpecTopic> Topics => SpecTopicCatalog.Topics;

    public async Task<SpecResolution> ResolveAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, string topicId, CancellationToken ct = default)
    {
        _ = SpecTopicCatalog.Find(topicId) ?? throw new NotFoundException($"Topic '{topicId}' is not defined.");
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        var candidates = await LoadCandidatesAsync(tenant, project, ct);
        return SpecResolver.Resolve(topicId, candidates);
    }

    public async Task<EffectiveSpecification> EffectiveAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        var candidates = await LoadCandidatesAsync(tenant, project, ct);
        var resolutions = SpecTopicCatalog.Topics.Select(t => SpecResolver.Resolve(t.Id, candidates)).ToList();
        var byTopic = resolutions.ToDictionary(r => r.Topic);
        return new EffectiveSpecification(
            resolutions,
            SpecResolver.CheckCompatibility(SpecTopicCatalog.Topics, byTopic),
            resolutions.Where(r => r.Binding is null).Select(r => r.Topic).ToArray());
    }

    /// <summary>
    /// Records a proposed decision as a knowledge item awaiting human approval. The content is checked
    /// against the topic definition first, so an illegal choice or parameter never enters the store.
    /// </summary>
    public async Task<KnowledgeItem> ProposeAsync(
        TenantId tenant,
        ProjectId project,
        PrincipalId actor,
        string topicId,
        SpecScopeLevel level,
        string content,
        string reason,
        CancellationToken ct = default)
    {
        var topic = SpecTopicCatalog.Find(topicId) ?? throw new NotFoundException($"Topic '{topicId}' is not defined.");
        await AuthorizeAsync(tenant, project, actor, write: true, ct);

        if (level == SpecScopeLevel.Platform)
            throw new SpecDecisionRejectedException([new SpecIssue("/scope/level", "Platform decisions are predefined and cannot be proposed.")]);

        var decision = SpecDecisionJson.TryParse(content, out var parseError)
            ?? throw new SpecDecisionRejectedException([new SpecIssue("/", parseError ?? "Invalid decision.")]);
        if (decision.Level != level)
            throw new SpecDecisionRejectedException([new SpecIssue("/scope/level", "Content scope level does not match the requested level.")]);

        var issues = SpecDecisionValidator.Validate(topic, decision);
        if (issues.Count > 0)
            throw new SpecDecisionRejectedException(issues);

        // A locked platform decision cannot be narrowed by a proposal; fail fast instead of at resolve time.
        var predefined = SpecTopicCatalog.PredefinedDecisions.FirstOrDefault(c => c.Decision.Topic == topicId);
        if (predefined is { Decision.Locked: true })
            throw new ConflictException($"Topic '{topicId}' is locked by the platform and cannot be overridden.");

        await EnsureSchemaAsync(ct);

        var key = $"spec.{topicId}.{SpecDecisionJson.ToLevelString(level)}.{Guid.NewGuid():N}";
        var item = await createItem.ExecuteAsync(
            tenant, project, key, $"{topic.Title} ({SpecDecisionJson.ToLevelString(level)}): {decision.Choice}",
            Classification.Internal, RepresentationKind.StructuredData,
            SpecDecisionJson.TypeId, SpecDecisionJson.SchemaVersion, actor, ct);

        await addRevision.ExecuteAsync(
            tenant, item.Id, 0, SpecDecisionJson.SchemaVersion, content, actor,
            string.IsNullOrWhiteSpace(reason) ? "Proposed specification decision." : reason,
            sourceReference: null, supersededRevisionId: null, ct);

        return item;
    }

    private async Task<List<SpecCandidate>> LoadCandidatesAsync(TenantId tenant, ProjectId project, CancellationToken ct)
    {
        var candidates = new List<SpecCandidate>(SpecTopicCatalog.PredefinedDecisions);
        var all = await items.ListByTenantAsync(tenant, ct);
        foreach (var item in all.Where(i => i.TypeId == SpecDecisionJson.TypeId && IsBinding(i)))
        {
            var revision = await revisions.GetByIdAsync(tenant, item.CurrentRevisionId!, ct);
            if (revision is null)
                continue;

            var decision = SpecDecisionJson.TryParse(revision.Content, out _);
            if (decision is null || decision.Level == SpecScopeLevel.Platform)
                continue;

            // A project decision only binds its own project; a tenant decision binds every project.
            if (decision.Level == SpecScopeLevel.Project && item.ProjectId != project)
                continue;

            candidates.Add(new SpecCandidate(
                decision, item.Id.Value, revision.Id.Value, revision.RevisionNumber, item.ProjectId.Value, item.CreatedAt));
        }

        return candidates;
    }

    private static bool IsBinding(KnowledgeItem item) =>
        item.CurrentRevisionId is not null &&
        item.ReviewState == ReviewState.Reviewed &&
        item.State is InfoState.Approved or InfoState.Verified;

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        if (await schemas.FindAsync(SpecDecisionJson.TypeId, SpecDecisionJson.SchemaVersion, ct) is not null)
            return;

        var schema = SchemaDefinition.Create(
            SpecDecisionJson.TypeId, SpecDecisionJson.SchemaVersion, "platform",
            "A machine-readable specification decision for one topic.", SpecDecisionJson.SchemaDocument);
        schema.Publish();
        await schemas.AddAsync(schema, ct);
        await uow.SaveChangesAsync(ct);
    }

    private async Task AuthorizeAsync(TenantId tenant, ProjectId project, PrincipalId actor, bool write, CancellationToken ct)
    {
        _ = await projects.GetByIdAsync(tenant, project, ct) ?? throw new NotFoundException("Project was not found.");
        var principal = await principals.GetByIdAsync(tenant, actor, ct);
        if (principal is null || !principal.IsActive)
            throw new NotFoundException("Actor was not found.");

        var member = (await memberships.ListByPrincipalAsync(tenant, actor, ct))
            .FirstOrDefault(m => m.ProjectId == project && m.Status == ProjectMembershipStatus.Active)
            ?? throw new NotFoundException("Project was not found.");

        if (principal.Type == PrincipalType.Human)
        {
            if (write && member.Role == ProjectRole.Viewer)
                throw new NotFoundException("Project was not found.");
            return;
        }

        if (principal.Type != PrincipalType.AiAgent)
            throw new UnauthorizedTenantException("Actor cannot access specifications.");
        if (!(await credentials.ListForAgentAsync(tenant, actor, ct)).Any(c => c.IsActive(DateTimeOffset.UtcNow)))
            throw new UnauthorizedTenantException("Agent has no active credential.");

        var capability = write ? AgentCapability.WriteKnowledge : AgentCapability.ReadKnowledge;
        if (!(await grants.ListForAgentAsync(tenant, actor, ct))
                .Any(g => g.IsActive(DateTimeOffset.UtcNow) && g.CoversProject(project) && g.Capability == capability))
            throw new UnauthorizedTenantException("Agent lacks the required specification capability.");
    }
}
