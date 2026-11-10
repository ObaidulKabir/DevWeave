using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Agents;

/// <summary>
/// A task-scoped, revision-pinned snapshot of knowledge assembled for an AI agent (SRS AGT-004,
/// AGT-005). The package is immutable once assembled: it records exactly which item revisions were
/// included, their representation at the time, and the idempotency key that scopes retry deduplication
/// (AGT-010). Source-code-bearing content is excluded from context packages by construction (AGT-004).
/// </summary>
public sealed class ContextPackage
{
    private readonly List<ContextPackageEntry> _entries;

    public ContextPackageId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }
    public string TaskKey { get; }
    public PrincipalId AgentId { get; }
    public string? IdempotencyKey { get; }
    public DateTimeOffset CreatedAt { get; }

    /// <summary>The revision-pinned entries, renumbered in stable position order (AGT-004, AGT-005).</summary>
    public IReadOnlyList<ContextPackageEntry> Entries => _entries;

    private ContextPackage(
        ContextPackageId id,
        TenantId tenantId,
        ProjectId projectId,
        string taskKey,
        PrincipalId agentId,
        string? idempotencyKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        TaskKey = taskKey;
        AgentId = agentId;
        IdempotencyKey = idempotencyKey;
        CreatedAt = createdAt;
        _entries = new List<ContextPackageEntry>();
    }

    public static ContextPackage Assemble(
        TenantId tenantId,
        ProjectId projectId,
        string taskKey,
        PrincipalId agentId,
        string? idempotencyKey,
        IReadOnlyList<ContextPackageEntry> candidateEntries)
    {
        if (string.IsNullOrWhiteSpace(taskKey))
            throw new ArgumentException("Context package task key is required.", nameof(taskKey));

        var entries = (candidateEntries ?? Array.Empty<ContextPackageEntry>())
            .Where(entry => !IsSourceCode(entry.Representation))
            .Select((entry, index) => entry with { Position = index })
            .ToList();

        var package = new ContextPackage(
            ContextPackageId.New(),
            tenantId,
            projectId,
            taskKey,
            agentId,
            idempotencyKey,
            DateTimeOffset.UtcNow);

        package._entries.AddRange(entries);

        return package;
    }

    /// <summary>
    /// The source-code exclusion guard (AGT-004): source-code-bearing representations never enter a
    /// context package.
    /// </summary>
    public static bool IsSourceCode(RepresentationKind representation) =>
        representation == RepresentationKind.SourceCode;
}