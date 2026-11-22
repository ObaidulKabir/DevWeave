using System.Security.Cryptography;
using System.Text.Json;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Coordination;

public enum BaselineHistoricalPrecision
{
    Exact = 1,
    ReducedLegacy = 2,
}

public sealed record BaselineRevisionRef(
    Guid ItemId,
    Guid RevisionId,
    string TypeId,
    string SchemaVersion,
    int Position);

public sealed record BaselineSemanticEdge(
    Guid RelationshipId,
    Guid SourceItemId,
    Guid TargetItemId,
    string RelationTypeCode,
    int Position);

public sealed record BaselineSpecificationBinding(
    string Topic,
    Guid? ItemId,
    Guid? RevisionId,
    string BindingHash,
    int Position);

public sealed record BaselineCriterionRef(
    Guid ItemId,
    Guid RevisionId,
    int Position);

public sealed record BaselineDependencyEdge(
    ProjectResourceKind FromKind,
    string FromIdentifier,
    ProjectResourceKind ToKind,
    string ToIdentifier,
    string RelationType,
    int Position);

/// <summary>
/// Immutable graph-complete companion to a v1 plan baseline. The v1 row remains for wire and
/// execution compatibility; this manifest preserves the exact semantic inputs used by new locks.
/// </summary>
public sealed class PlanBaselineV2
{
    public const string CurrentManifestSchemaVersion = "2.0";

    private readonly List<BaselineRevisionRef> _approvedRevisions;
    private readonly List<BaselineSemanticEdge> _semanticEdges;
    private readonly List<BaselineSpecificationBinding> _specificationBindings;
    private readonly List<BaselineCriterionRef> _criterionRefs;
    private readonly List<BaselineDependencyEdge> _dependencyGraph;

    public BaselineId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string ManifestSchemaVersion { get; }
    public BaselineHistoricalPrecision HistoricalPrecision { get; }
    public IReadOnlyList<BaselineRevisionRef> ApprovedRevisions => _approvedRevisions;
    public IReadOnlyList<BaselineSemanticEdge> SemanticEdges => _semanticEdges;
    public IReadOnlyList<BaselineSpecificationBinding> SpecificationBindings => _specificationBindings;
    public IReadOnlyList<BaselineCriterionRef> CriterionRefs => _criterionRefs;
    public IReadOnlyList<BaselineDependencyEdge> DependencyGraph => _dependencyGraph;
    public IReadOnlyList<string> LegacyAcceptanceCriteria { get; }
    public Guid LifecycleProfileId { get; }
    public string LifecycleProfileVersion { get; }
    public string PolicyVersion { get; }
    public BaselineId? PredecessorBaselineId { get; }
    public string ManifestChecksum { get; }
    public DateTimeOffset CreatedAt { get; }

    private PlanBaselineV2(
        BaselineId id,
        TenantId tenantId,
        ProjectId projectId,
        string manifestSchemaVersion,
        BaselineHistoricalPrecision historicalPrecision,
        IReadOnlyList<string> legacyAcceptanceCriteria,
        Guid lifecycleProfileId,
        string lifecycleProfileVersion,
        string policyVersion,
        BaselineId? predecessorBaselineId,
        string manifestChecksum,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        ManifestSchemaVersion = manifestSchemaVersion;
        HistoricalPrecision = historicalPrecision;
        LegacyAcceptanceCriteria = legacyAcceptanceCriteria;
        LifecycleProfileId = lifecycleProfileId;
        LifecycleProfileVersion = lifecycleProfileVersion;
        PolicyVersion = policyVersion;
        PredecessorBaselineId = predecessorBaselineId;
        ManifestChecksum = manifestChecksum;
        CreatedAt = createdAt;
        _approvedRevisions = [];
        _semanticEdges = [];
        _specificationBindings = [];
        _criterionRefs = [];
        _dependencyGraph = [];
    }

    public static PlanBaselineV2 CreateExact(
        BaselineId id,
        TenantId tenantId,
        ProjectId projectId,
        IReadOnlyList<BaselineRevisionRef> approvedRevisions,
        IReadOnlyList<BaselineSemanticEdge> semanticEdges,
        IReadOnlyList<BaselineSpecificationBinding> specificationBindings,
        IReadOnlyList<BaselineCriterionRef> criterionRefs,
        IReadOnlyList<BaselineDependencyEdge> dependencyGraph,
        IReadOnlyList<string> legacyAcceptanceCriteria,
        Guid lifecycleProfileId,
        string lifecycleProfileVersion,
        string policyVersion,
        BaselineId? predecessorBaselineId)
    {
        if (approvedRevisions.Count == 0)
            throw new ArgumentException("An exact baseline requires at least one approved revision.", nameof(approvedRevisions));
        if (lifecycleProfileId == Guid.Empty)
            throw new ArgumentException("Lifecycle profile ID is required.", nameof(lifecycleProfileId));
        RequireText(lifecycleProfileVersion, nameof(lifecycleProfileVersion));
        RequireText(policyVersion, nameof(policyVersion));

        var revisions = Reposition(
            approvedRevisions
                .OrderBy(reference => reference.ItemId)
                .ThenBy(reference => reference.RevisionId),
            (reference, position) => reference with { Position = position });
        EnsureUnique(revisions.Select(reference => reference.ItemId), nameof(approvedRevisions));
        EnsureUnique(revisions.Select(reference => reference.RevisionId), nameof(approvedRevisions));

        var edges = Reposition(
            semanticEdges
                .OrderBy(edge => edge.RelationTypeCode, StringComparer.Ordinal)
                .ThenBy(edge => edge.SourceItemId)
                .ThenBy(edge => edge.TargetItemId)
                .ThenBy(edge => edge.RelationshipId),
            (edge, position) => edge with { Position = position });
        EnsureUnique(edges.Select(edge => edge.RelationshipId), nameof(semanticEdges));

        var bindings = Reposition(
            specificationBindings.OrderBy(binding => binding.Topic, StringComparer.Ordinal),
            (binding, position) => binding with { Position = position });
        EnsureUnique(bindings.Select(binding => binding.Topic), nameof(specificationBindings));

        var criteria = Reposition(
            criterionRefs.OrderBy(reference => reference.ItemId).ThenBy(reference => reference.RevisionId),
            (reference, position) => reference with { Position = position });
        EnsureUnique(
            criteria.Select(reference => $"{reference.ItemId:D}:{reference.RevisionId:D}"),
            nameof(criterionRefs));

        var dependencies = Reposition(
            dependencyGraph
                .OrderBy(edge => edge.FromKind)
                .ThenBy(edge => edge.FromIdentifier, StringComparer.Ordinal)
                .ThenBy(edge => edge.ToKind)
                .ThenBy(edge => edge.ToIdentifier, StringComparer.Ordinal)
                .ThenBy(edge => edge.RelationType, StringComparer.Ordinal),
            (edge, position) => edge with { Position = position });
        EnsureUnique(
            dependencies.Select(edge =>
                $"{edge.FromKind}:{edge.FromIdentifier}:{edge.RelationType}:{edge.ToKind}:{edge.ToIdentifier}"),
            nameof(dependencyGraph));

        var legacyCriteria = legacyAcceptanceCriteria
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var checksum = ComputeChecksum(
            id,
            projectId,
            BaselineHistoricalPrecision.Exact,
            revisions,
            edges,
            bindings,
            criteria,
            dependencies,
            legacyCriteria,
            lifecycleProfileId,
            lifecycleProfileVersion,
            policyVersion,
            predecessorBaselineId);

        var baseline = new PlanBaselineV2(
            id,
            tenantId,
            projectId,
            CurrentManifestSchemaVersion,
            BaselineHistoricalPrecision.Exact,
            legacyCriteria,
            lifecycleProfileId,
            lifecycleProfileVersion.Trim(),
            policyVersion.Trim(),
            predecessorBaselineId,
            checksum,
            DateTimeOffset.UtcNow);
        baseline._approvedRevisions.AddRange(revisions);
        baseline._semanticEdges.AddRange(edges);
        baseline._specificationBindings.AddRange(bindings);
        baseline._criterionRefs.AddRange(criteria);
        baseline._dependencyGraph.AddRange(dependencies);
        return baseline;
    }

    public static PlanBaselineV2 ProjectLegacy(
        PlanBaseline legacy,
        Guid lifecycleProfileId,
        string lifecycleProfileVersion,
        string policyVersion,
        BaselineId? predecessorBaselineId)
    {
        var revisions = legacy.ApprovedRevisionIds
            .Distinct()
            .OrderBy(id => id)
            .Select((revisionId, position) =>
                new BaselineRevisionRef(Guid.Empty, revisionId, "legacy.unknown", "unknown", position))
            .ToArray();
        var criteria = legacy.AcceptanceCriteria
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var checksum = ComputeChecksum(
            legacy.Id,
            legacy.ProjectId,
            BaselineHistoricalPrecision.ReducedLegacy,
            revisions,
            [],
            [],
            [],
            [],
            criteria,
            lifecycleProfileId,
            lifecycleProfileVersion,
            policyVersion,
            predecessorBaselineId);

        var projection = new PlanBaselineV2(
            legacy.Id,
            legacy.TenantId,
            legacy.ProjectId,
            CurrentManifestSchemaVersion,
            BaselineHistoricalPrecision.ReducedLegacy,
            criteria,
            lifecycleProfileId,
            lifecycleProfileVersion,
            policyVersion,
            predecessorBaselineId,
            checksum,
            legacy.CreatedAt);
        projection._approvedRevisions.AddRange(revisions);
        return projection;
    }

    public bool HasValidChecksum() =>
        string.Equals(
            ManifestChecksum,
            ComputeChecksum(
                Id,
                ProjectId,
                HistoricalPrecision,
                _approvedRevisions,
                _semanticEdges,
                _specificationBindings,
                _criterionRefs,
                _dependencyGraph,
                LegacyAcceptanceCriteria,
                LifecycleProfileId,
                LifecycleProfileVersion,
                PolicyVersion,
                PredecessorBaselineId),
            StringComparison.Ordinal);

    private static string ComputeChecksum(
        BaselineId id,
        ProjectId projectId,
        BaselineHistoricalPrecision precision,
        IReadOnlyList<BaselineRevisionRef> revisions,
        IReadOnlyList<BaselineSemanticEdge> edges,
        IReadOnlyList<BaselineSpecificationBinding> bindings,
        IReadOnlyList<BaselineCriterionRef> criteria,
        IReadOnlyList<BaselineDependencyEdge> dependencies,
        IReadOnlyList<string> legacyCriteria,
        Guid lifecycleProfileId,
        string lifecycleProfileVersion,
        string policyVersion,
        BaselineId? predecessorBaselineId)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            manifestSchemaVersion = CurrentManifestSchemaVersion,
            baselineId = id.Value,
            projectId = projectId.Value,
            historicalPrecision = precision.ToString(),
            approvedRevisions = revisions,
            semanticEdges = edges,
            specificationBindings = bindings,
            criterionRefs = criteria,
            dependencyGraph = dependencies,
            legacyAcceptanceCriteria = legacyCriteria,
            lifecycleProfileId,
            lifecycleProfileVersion,
            policyVersion,
            predecessorBaselineId = predecessorBaselineId?.Value,
        });
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    private static T[] Reposition<T>(
        IEnumerable<T> values,
        Func<T, int, T> withPosition) =>
        values.Select(withPosition).ToArray();

    private static void EnsureUnique<T>(IEnumerable<T> values, string parameterName)
        where T : notnull
    {
        var materialized = values.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
            throw new ArgumentException("Baseline manifest references must be unique.", parameterName);
    }

    private static void RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
    }
}
