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
    IReadOnlyList<string> UndecidedTopics,
    IReadOnlyList<SpecOverlayProblem> CatalogProblems);

public sealed record SpecOptionView(
    SpecChoice Choice,
    bool Recommended,
    bool Compatible,
    IReadOnlyList<string> Reasons);

public sealed record OpenTopicView(SpecTopic Topic, string? Default, int DependedOnBy);

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
    SpecCatalogService catalogs,
    CreateKnowledgeItem createItem,
    AddRevision addRevision,
    IProjectRepository projects,
    IPrincipalRepository principals,
    IProjectMembershipRepository memberships,
    IAgentGrantRepository grants,
    IAgentCredentialRepository credentials,
    IUnitOfWork uow)
{
    /// <summary>The effective catalog (packs plus approved overlays) for a project.</summary>
    public async Task<SpecCatalog> CatalogAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        return await catalogs.BuildAsync(tenant, project, ct);
    }

    public async Task<SpecResolution> ResolveAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, string topicId, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        var catalog = await catalogs.BuildAsync(tenant, project, ct);
        _ = catalog.Find(topicId) ?? throw new NotFoundException($"Topic '{topicId}' is not defined.");
        var candidates = await LoadCandidatesAsync(tenant, project, catalog, ct);
        return SpecResolver.Resolve(topicId, candidates);
    }

    public async Task<EffectiveSpecification> EffectiveAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        var catalog = await catalogs.BuildAsync(tenant, project, ct);
        var candidates = await LoadCandidatesAsync(tenant, project, catalog, ct);
        var resolutions = catalog.Topics.Select(t => SpecResolver.Resolve(t.Id, candidates)).ToList();
        var byTopic = resolutions.ToDictionary(r => r.Topic);
        return new EffectiveSpecification(
            resolutions,
            SpecResolver.CheckCompatibility(catalog.Topics, byTopic),
            resolutions.Where(r => r.Binding is null).Select(r => r.Topic).ToArray(),
            catalog.Problems);
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
        await AuthorizeAsync(tenant, project, actor, write: true, ct);
        var catalog = await catalogs.BuildAsync(tenant, project, ct);
        var topic = catalog.Find(topicId) ?? throw new NotFoundException($"Topic '{topicId}' is not defined.");

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
        if (catalog.IsLocked(topicId))
            throw new ConflictException($"Topic '{topicId}' is locked by the platform and cannot be overridden.");

        await EnsureSchemaAsync(SpecDecisionJson.TypeId, SpecDecisionJson.SchemaVersion,
            "A machine-readable specification decision for one topic.", SpecDecisionJson.SchemaDocument, ct);

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

    /// <summary>
    /// Ranked, compatibility-filtered options for a topic given the decisions that already bind. Agents
    /// choose from this list instead of inventing an answer.
    /// </summary>
    public async Task<(SpecTopic Topic, IReadOnlyList<SpecOptionView> Options)> OptionsAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, string topicId, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        var catalog = await catalogs.BuildAsync(tenant, project, ct);
        var topic = catalog.Find(topicId) ?? throw new NotFoundException($"Topic '{topicId}' is not defined.");
        var candidates = await LoadCandidatesAsync(tenant, project, catalog, ct);
        var bound = catalog.Topics
            .Select(t => SpecResolver.Resolve(t.Id, candidates))
            .Where(r => r.Binding is not null)
            .ToDictionary(r => r.Topic, r => r.Binding!.Decision.Choice);

        var views = new List<SpecOptionView>();
        foreach (var choice in topic.Choices)
        {
            var reasons = new List<string>();

            // This topic's own rules: choosing X requires another topic to be one of Y.
            foreach (var rule in topic.RuleList.Where(r => r.WhenChoice == choice.Id))
            {
                if (bound.TryGetValue(rule.OtherTopic, out var other) && !rule.AllowedOtherChoices.Contains(other))
                    reasons.Add($"Needs {rule.OtherTopic} to be {string.Join(" or ", rule.AllowedOtherChoices)}, but it is {other}.");
            }

            // Other topics' rules that constrain this one.
            foreach (var otherTopic in catalog.Topics.Where(t => t.Id != topic.Id))
            {
                foreach (var rule in otherTopic.RuleList.Where(r => r.OtherTopic == topic.Id))
                {
                    if (bound.TryGetValue(otherTopic.Id, out var otherChoice) && otherChoice == rule.WhenChoice &&
                        !rule.AllowedOtherChoices.Contains(choice.Id))
                        reasons.Add($"{otherTopic.Id} is {otherChoice}, which needs {topic.Id} to be {string.Join(" or ", rule.AllowedOtherChoices)}.");
                }
            }

            views.Add(new SpecOptionView(
                choice,
                choice.Support == SpecSupport.Recommended || topic.Default == choice.Id,
                reasons.Count == 0 && choice.Support != SpecSupport.Deprecated,
                reasons));
        }

        var ordered = views
            .OrderByDescending(v => v.Compatible)
            .ThenBy(v => v.Choice.Support)
            .ThenByDescending(v => v.Choice.Id == topic.Default)
            .ThenBy(v => v.Choice.Id, StringComparer.Ordinal)
            .ToArray();
        return (topic, ordered);
    }

    /// <summary>Topics that still have no binding decision, most depended-on first (language before framework).</summary>
    public async Task<IReadOnlyList<OpenTopicView>> OpenTopicsAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: false, ct);
        var catalog = await catalogs.BuildAsync(tenant, project, ct);
        var candidates = await LoadCandidatesAsync(tenant, project, catalog, ct);
        var undecided = catalog.Topics.Where(t => SpecResolver.Resolve(t.Id, candidates).Binding is null).ToList();
        return undecided
            .Select(t => new OpenTopicView(t, t.Default, catalog.Topics.Count(o => o.RuleList.Any(r => r.OtherTopic == t.Id))))
            .OrderByDescending(v => v.DependedOnBy)
            .ThenBy(v => v.Topic.Domain, StringComparer.Ordinal)
            .ThenBy(v => v.Topic.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Proposes a catalog overlay (custom topics, options, profiles, hidden or deprecated options). It is
    /// checked against the current effective catalog first and binds only after a person approves it.
    /// </summary>
    public async Task<KnowledgeItem> ProposeOverlayAsync(
        TenantId tenant, ProjectId project, PrincipalId actor, string content, string reason, CancellationToken ct = default)
    {
        await AuthorizeAsync(tenant, project, actor, write: true, ct);

        var overlay = SpecOverlayJson.TryParse(content, out var parseError)
            ?? throw new SpecDecisionRejectedException([new SpecIssue("/", parseError ?? "Invalid overlay.")]);

        var catalog = await catalogs.BuildAsync(tenant, project, ct);
        var issues = SpecCatalogBuilder.Validate(catalog, overlay, SpecCatalogService.BuiltInRoots, out _).ToList();
        issues.AddRange(overlay.EnablePacks
            .Where(id => catalog.Packs.All(p => p.Id != id))
            .Select(id => new SpecIssue("/enablePacks", $"Unknown package '{id}'.")));
        if (issues.Count > 0)
            throw new SpecDecisionRejectedException(issues);

        await EnsureSchemaAsync(SpecOverlayJson.TypeId, SpecOverlayJson.SchemaVersion,
            "A tenant or project extension of the built-in specification catalog.", SpecOverlayJson.SchemaDocument, ct);

        var level = overlay.Level.ToString().ToLowerInvariant();
        var item = await createItem.ExecuteAsync(
            tenant, project, $"spec.overlay.{level}.{Guid.NewGuid():N}", $"Catalog overlay ({level})",
            Classification.Internal, RepresentationKind.StructuredData,
            SpecOverlayJson.TypeId, SpecOverlayJson.SchemaVersion, actor, ct);

        await addRevision.ExecuteAsync(
            tenant, item.Id, 0, SpecOverlayJson.SchemaVersion, content, actor,
            string.IsNullOrWhiteSpace(reason) ? "Proposed catalog overlay." : reason,
            sourceReference: null, supersededRevisionId: null, ct);

        return item;
    }

    private async Task<List<SpecCandidate>> LoadCandidatesAsync(
        TenantId tenant, ProjectId project, SpecCatalog catalog, CancellationToken ct)
    {
        var candidates = new List<SpecCandidate>(catalog.PredefinedDecisions);
        var all = await items.ListByTenantAsync(tenant, ct);
        foreach (var item in all.Where(i => i.TypeId == SpecDecisionJson.TypeId && SpecItems.IsBinding(i)))
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

    private async Task EnsureSchemaAsync(string typeId, string version, string description, string document, CancellationToken ct)
    {
        if (await schemas.FindAsync(typeId, version, ct) is not null)
            return;

        var schema = SchemaDefinition.Create(typeId, version, "platform", description, document);
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
