using DevWeave.Application.ProjectGraph;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Relations;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Domain = DevWeave.Domain;

namespace DevWeave.Infrastructure.ProjectGraph;

/// <summary>
/// Derives a payload-free graph from canonical aggregate tables. It does not persist graph nodes or
/// edges, so the projection cannot diverge into a second source of truth.
/// </summary>
internal sealed class EfProjectGraphSource(DevWeaveDbContext db) : IProjectGraphSource
{
    public async Task<ProjectGraphSnapshot> LoadAsync(
        Domain.Identity.TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset asOf,
        CancellationToken ct = default,
        DateTimeOffset? asOfValid = null)
    {
        var nodes = new Dictionary<string, ProjectGraphNode>(StringComparer.Ordinal);
        var edges = new List<ProjectGraphEdge>();
        var projectRef = Local(ProjectResourceKind.Project, projectId.Value);
        AddNode(nodes, projectRef, "Project", Classification.Internal, asOf);

        var items = await db.KnowledgeItems
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.ProjectId == projectId &&
                item.CreatedAt <= asOf)
            .ToListAsync(ct);
        var itemById = items.ToDictionary(item => item.Id);
        var itemIds = itemById.Keys.ToArray();
        foreach (var item in items)
        {
            var itemRef = Local(ProjectResourceKind.KnowledgeItem, item.Id.Value);
            AddNode(nodes, itemRef, item.Title, item.Classification, item.CreatedAt);
            edges.Add(new ProjectGraphEdge(projectRef, itemRef, "contains"));
        }

        var revisions = itemIds.Length == 0
            ? []
            : await db.KnowledgeRevisions
                .AsNoTracking()
                .Where(revision =>
                    revision.TenantId == tenantId &&
                    revision.RecordedAt <= asOf &&
                    itemIds.Contains(revision.KnowledgeItemId))
                .ToListAsync(ct);
        if (asOfValid is { } validInstant)
            revisions = revisions.Where(revision => revision.IsValidAt(validInstant)).ToList();
        var revisionById = revisions.ToDictionary(revision => revision.Id);
        foreach (var revision in revisions)
        {
            var item = itemById[revision.KnowledgeItemId];
            var itemRef = Local(ProjectResourceKind.KnowledgeItem, item.Id.Value);
            var revisionRef = Local(ProjectResourceKind.KnowledgeRevision, revision.Id.Value);
            AddNode(
                nodes,
                revisionRef,
                $"{item.Key}@{revision.RevisionNumber}",
                item.Classification,
                revision.RecordedAt);
            edges.Add(new ProjectGraphEdge(itemRef, revisionRef, "has_revision"));
            if (revision.SupersededRevisionId is { } superseded &&
                revisionById.ContainsKey(superseded))
            {
                edges.Add(new ProjectGraphEdge(
                    revisionRef,
                    Local(ProjectResourceKind.KnowledgeRevision, superseded.Value),
                    "supersedes"));
            }
        }

        var relationships = await db.Relationships
            .AsNoTracking()
            .Where(relationship =>
                relationship.TenantId == tenantId &&
                relationship.ProjectId == projectId &&
                relationship.CreatedAt <= asOf &&
                (relationship.RevokedAt == null || relationship.RevokedAt > asOf))
            .ToListAsync(ct);
        if (asOfValid is { } validForEdge)
        {
            relationships = relationships
                .Where(relationship =>
                    (relationship.ValidFrom == null || relationship.ValidFrom <= validForEdge) &&
                    (relationship.ValidTo == null || relationship.ValidTo >= validForEdge))
                .ToList();
        }

        foreach (var relationship in relationships)
        {
            edges.Add(new ProjectGraphEdge(
                Local(ProjectResourceKind.KnowledgeItem, relationship.SourceItemId.Value),
                Local(ProjectResourceKind.KnowledgeItem, relationship.TargetItemId.Value),
                relationship.RelationTypeCode,
                Local(ProjectResourceKind.Relationship, relationship.Id.Value)));
        }

        var workLinks = await db.PlanWorkLinks
            .AsNoTracking()
            .Where(link =>
                link.TenantId == tenantId &&
                link.ProjectId == projectId &&
                link.CreatedAt <= asOf)
            .ToListAsync(ct);
        var linkedClassification = workLinks
            .Where(link => itemById.ContainsKey(link.KnowledgeItemId))
            .GroupBy(link => link.WorkItemId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(link => itemById[link.KnowledgeItemId].Classification));
        var workItems = await db.WorkItems
            .AsNoTracking()
            .Where(work =>
                work.TenantId == tenantId &&
                work.ProjectId == projectId &&
                work.CreatedAt <= asOf)
            .ToListAsync(ct);
        foreach (var work in workItems)
        {
            var workRef = Local(ProjectResourceKind.WorkItem, work.Id.Value);
            var classification = linkedClassification.GetValueOrDefault(work.Id, Classification.Internal);
            AddNode(nodes, workRef, work.Title, classification, work.CreatedAt);
            edges.Add(new ProjectGraphEdge(projectRef, workRef, "contains"));
        }
        foreach (var link in workLinks.Where(link => revisionById.ContainsKey(link.RevisionId)))
        {
            edges.Add(new ProjectGraphEdge(
                Local(ProjectResourceKind.WorkItem, link.WorkItemId.Value),
                Local(ProjectResourceKind.KnowledgeRevision, link.RevisionId.Value),
                "implements"));
        }

        var dependencies = await db.WorkItemDependencies
            .AsNoTracking()
            .Where(dependency =>
                dependency.TenantId == tenantId &&
                dependency.ProjectId == projectId &&
                dependency.CreatedAt <= asOf)
            .ToListAsync(ct);
        foreach (var dependency in dependencies)
        {
            edges.Add(new ProjectGraphEdge(
                Local(ProjectResourceKind.WorkItem, dependency.WorkItemId.Value),
                Local(ProjectResourceKind.WorkItem, dependency.DependsOnId.Value),
                "depends_on"));
        }

        var activities = await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Inputs)
            .Include(activity => activity.Outputs)
            .Where(activity =>
                activity.TenantId == tenantId &&
                activity.ProjectId == projectId &&
                activity.StartedAt <= asOf)
            .ToListAsync(ct);
        var activityClassification = new Dictionary<Domain.Activities.ActivityId, Classification>();
        foreach (var activity in activities)
        {
            var referencedItems = activity.Inputs.Select(input => input.ItemId)
                .Concat(activity.Outputs.Select(output => output.ItemId))
                .Where(itemById.ContainsKey)
                .ToArray();
            var classification = referencedItems.Length == 0
                ? Classification.Internal
                : referencedItems.Max(itemId => itemById[itemId].Classification);
            activityClassification[activity.Id] = classification;
            var activityRef = Local(ProjectResourceKind.Activity, activity.Id.Value);
            var actorRef = Local(ProjectResourceKind.Principal, activity.Actor.Value);
            AddNode(nodes, activityRef, activity.Purpose, classification, activity.StartedAt);
            AddNode(nodes, actorRef, activity.Actor.Value.ToString("D"), Classification.Internal, activity.StartedAt);
            edges.Add(new ProjectGraphEdge(projectRef, activityRef, "contains"));
            edges.Add(new ProjectGraphEdge(activityRef, actorRef, "associated_with"));
            foreach (var input in activity.Inputs.Where(input => revisionById.ContainsKey(input.RevisionId)))
            {
                edges.Add(new ProjectGraphEdge(
                    Local(ProjectResourceKind.KnowledgeRevision, input.RevisionId.Value),
                    activityRef,
                    "used_by"));
            }
            foreach (var output in activity.Outputs.Where(output => revisionById.ContainsKey(output.RevisionId)))
            {
                edges.Add(new ProjectGraphEdge(
                    activityRef,
                    Local(ProjectResourceKind.KnowledgeRevision, output.RevisionId.Value),
                    "generated"));
            }
        }

        var artifacts = await db.Artifacts
            .AsNoTracking()
            .Where(artifact =>
                artifact.TenantId == tenantId &&
                artifact.ProjectId == projectId &&
                artifact.CreatedAt <= asOf)
            .ToListAsync(ct);
        var artifactById = artifacts.ToDictionary(artifact => artifact.Id);
        foreach (var artifact in artifacts)
        {
            var artifactRef = Local(ProjectResourceKind.Artifact, artifact.Id.Value);
            AddNode(nodes, artifactRef, $"{artifact.Key}@{artifact.Version}", artifact.Classification, artifact.CreatedAt);
            edges.Add(new ProjectGraphEdge(projectRef, artifactRef, "contains"));
        }

        var evidenceRecords = await db.Evidence
            .AsNoTracking()
            .Where(evidence =>
                evidence.TenantId == tenantId &&
                evidence.ProjectId == projectId &&
                evidence.RecordedAt <= asOf)
            .ToListAsync(ct);
        foreach (var evidence in evidenceRecords)
        {
            var classification = activityClassification.GetValueOrDefault(
                evidence.OriginActivityId,
                Classification.Internal);
            if (evidence.RelatedArtifactId is { } artifactId &&
                artifactById.TryGetValue(artifactId, out var artifact) &&
                artifact.Classification > classification)
                classification = artifact.Classification;

            var evidenceRef = Local(ProjectResourceKind.Evidence, evidence.Id.Value);
            AddNode(nodes, evidenceRef, evidence.Type, classification, evidence.RecordedAt);
            edges.Add(new ProjectGraphEdge(
                evidenceRef,
                Local(ProjectResourceKind.Activity, evidence.OriginActivityId.Value),
                "derived_from"));
            if (evidence.RelatedArtifactId is { } relatedArtifact)
            {
                edges.Add(new ProjectGraphEdge(
                    evidenceRef,
                    Local(ProjectResourceKind.Artifact, relatedArtifact.Value),
                    "supported_by"));
            }
        }

        await AddCoordinationGraphAsync(
            tenantId,
            projectId,
            asOf,
            nodes,
            edges,
            revisionById,
            itemById,
            projectRef,
            ct);
        await AddExternalReferencesAsync(tenantId, projectId, asOf, nodes, edges, projectRef, ct);

        var nodeKeys = nodes.Keys.ToHashSet(StringComparer.Ordinal);
        var completeEdges = edges
            .Where(edge =>
                nodeKeys.Contains(edge.From.CanonicalKey) &&
                nodeKeys.Contains(edge.To.CanonicalKey))
            .Distinct()
            .ToArray();
        return new ProjectGraphSnapshot(asOf, nodes.Values.ToArray(), completeEdges);
    }

    private async Task AddCoordinationGraphAsync(
        Domain.Identity.TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset asOf,
        IDictionary<string, ProjectGraphNode> nodes,
        ICollection<ProjectGraphEdge> edges,
        IReadOnlyDictionary<KnowledgeRevisionId, KnowledgeRevision> revisionById,
        IReadOnlyDictionary<KnowledgeItemId, KnowledgeItem> itemById,
        ProjectResourceRef projectRef,
        CancellationToken ct)
    {
        var manifests = await db.PlanBaselineManifests
            .AsNoTracking()
            .Include(manifest => manifest.ApprovedRevisions)
            .Where(manifest =>
                manifest.TenantId == tenantId &&
                manifest.ProjectId == projectId &&
                manifest.CreatedAt <= asOf)
            .ToListAsync(ct);
        var manifestsById = manifests.ToDictionary(manifest => manifest.Id);
        var baselines = await db.PlanBaselines
            .AsNoTracking()
            .Where(baseline =>
                baseline.TenantId == tenantId &&
                baseline.ProjectId == projectId &&
                baseline.CreatedAt <= asOf)
            .ToListAsync(ct);
        var baselineClassification = new Dictionary<Domain.Coordination.BaselineId, Classification>();
        foreach (var baseline in baselines)
        {
            var revisionIds = manifestsById.TryGetValue(baseline.Id, out var manifest)
                ? manifest.ApprovedRevisions.Select(reference => new KnowledgeRevisionId(reference.RevisionId))
                : baseline.ApprovedRevisionIds.Select(id => new KnowledgeRevisionId(id));
            var classification = revisionIds
                .Where(revisionById.ContainsKey)
                .Select(revisionId => itemById[revisionById[revisionId].KnowledgeItemId].Classification)
                .DefaultIfEmpty(Classification.Internal)
                .Max();
            baselineClassification[baseline.Id] = classification;
            var baselineRef = Local(ProjectResourceKind.PlanBaseline, baseline.Id.Value);
            AddNode(nodes, baselineRef, baseline.Goal, classification, baseline.CreatedAt);
            edges.Add(new ProjectGraphEdge(projectRef, baselineRef, "contains"));
            foreach (var revisionId in revisionIds.Where(revisionById.ContainsKey))
            {
                edges.Add(new ProjectGraphEdge(
                    baselineRef,
                    Local(ProjectResourceKind.KnowledgeRevision, revisionId.Value),
                    "pins"));
            }
        }

        var packages = await db.WorkPackages
            .AsNoTracking()
            .Where(package =>
                package.TenantId == tenantId &&
                package.ProjectId == projectId &&
                package.CreatedAt <= asOf)
            .ToListAsync(ct);
        var packageClassification = new Dictionary<Domain.Coordination.WorkPackageId, Classification>();
        foreach (var package in packages)
        {
            var baselineId = new Domain.Coordination.BaselineId(package.BaselineId);
            var classification = baselineClassification.GetValueOrDefault(
                baselineId,
                Classification.Internal);
            packageClassification[package.Id] = classification;
            var packageRef = Local(ProjectResourceKind.WorkPackage, package.Id.Value);
            AddNode(nodes, packageRef, package.Objective, classification, package.CreatedAt);
            edges.Add(new ProjectGraphEdge(
                packageRef,
                Local(ProjectResourceKind.PlanBaseline, package.BaselineId),
                "executes_against"));
            foreach (var revisionId in package.ScopeRevisionIds)
            {
                edges.Add(new ProjectGraphEdge(
                    packageRef,
                    Local(ProjectResourceKind.KnowledgeRevision, revisionId),
                    "scoped_to"));
            }
            foreach (var workItemId in package.ScopeWorkItemIds)
            {
                edges.Add(new ProjectGraphEdge(
                    packageRef,
                    Local(ProjectResourceKind.WorkItem, workItemId),
                    "delivers"));
            }
        }

        var targets = await db.CodebaseTargets
            .AsNoTracking()
            .Where(target =>
                target.TenantId == tenantId &&
                target.ProjectId == projectId &&
                target.CreatedAt <= asOf)
            .ToListAsync(ct);
        foreach (var target in targets)
        {
            var targetRef = Local(ProjectResourceKind.CodebaseTarget, target.Id.Value);
            AddNode(
                nodes,
                targetRef,
                $"{target.Provider}:{target.RepositoryId}@{target.TargetBranch}",
                Classification.Internal,
                target.CreatedAt);
            edges.Add(new ProjectGraphEdge(projectRef, targetRef, "references"));
        }

        var changeSets = await db.ChangeSets
            .AsNoTracking()
            .Where(changeSet =>
                changeSet.TenantId == tenantId &&
                changeSet.ProjectId == projectId &&
                changeSet.RecordedAt <= asOf)
            .ToListAsync(ct);
        foreach (var changeSet in changeSets)
        {
            var classification = packageClassification.GetValueOrDefault(
                changeSet.WorkPackageId,
                Classification.Internal);
            var changeSetRef = Local(ProjectResourceKind.ChangeSet, changeSet.Id.Value);
            AddNode(nodes, changeSetRef, changeSet.ProviderRef, classification, changeSet.RecordedAt);
            edges.Add(new ProjectGraphEdge(
                changeSetRef,
                Local(ProjectResourceKind.WorkPackage, changeSet.WorkPackageId.Value),
                "produced_for"));
            edges.Add(new ProjectGraphEdge(
                changeSetRef,
                Local(ProjectResourceKind.CodebaseTarget, changeSet.CodebaseTargetId.Value),
                "targets"));
        }
    }

    private async Task AddExternalReferencesAsync(
        Domain.Identity.TenantId tenantId,
        ProjectId projectId,
        DateTimeOffset asOf,
        IDictionary<string, ProjectGraphNode> nodes,
        ICollection<ProjectGraphEdge> edges,
        ProjectResourceRef projectRef,
        CancellationToken ct)
    {
        var references = await db.ProjectReferences
            .AsNoTracking()
            .Where(reference =>
                reference.TenantId == tenantId &&
                reference.ProjectId == projectId &&
                reference.CreatedAt <= asOf)
            .ToListAsync(ct);
        foreach (var reference in references)
        {
            var externalRef = new ProjectResourceRef(
                ProjectResourceKind.ExternalReference,
                $"{reference.Type}:{reference.ExternalId}");
            AddNode(
                nodes,
                externalRef,
                reference.ExternalId,
                Classification.Internal,
                reference.CreatedAt);
            edges.Add(new ProjectGraphEdge(projectRef, externalRef, "references"));
        }
    }

    private static ProjectResourceRef Local(ProjectResourceKind kind, Guid id) =>
        ProjectResourceRef.Local(kind, id);

    private static void AddNode(
        IDictionary<string, ProjectGraphNode> nodes,
        ProjectResourceRef resource,
        string label,
        Classification classification,
        DateTimeOffset recordedAt)
    {
        nodes.TryAdd(
            resource.CanonicalKey,
            new ProjectGraphNode(resource, label, classification, recordedAt));
    }
}
