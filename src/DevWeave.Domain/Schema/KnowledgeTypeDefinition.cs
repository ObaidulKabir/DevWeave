using DevWeave.Domain.Knowledge;

namespace DevWeave.Domain.Schema;

/// <summary>
/// How catalogue content is stored. Knowledge-backed types use the universal envelope; a few
/// catalogue concepts map to existing aggregates so they are not duplicated as Knowledge rows.
/// </summary>
public enum KnowledgeStorageKind
{
    KnowledgeEnvelope = 1,
    WorkItemAggregate = 2,
    ActivityAggregate = 3,
    EvidenceAggregate = 4,
}

/// <summary>
/// Governed metadata for one knowledge type id (SRS Appendix A / PRJ-007). Schema versions remain
/// in <see cref="SchemaDefinition"/>; this catalogue describes ownership, storage, lifecycle and
/// classification defaults above those versions.
/// </summary>
public sealed class KnowledgeTypeDefinition
{
    public string TypeId { get; }
    public string Owner { get; }
    public string Description { get; }
    public KnowledgeStorageKind StorageKind { get; }
    public string LifecyclePolicy { get; }
    public Classification DefaultClassification { get; }
    public bool IsCanonical { get; }
    public string MigrationStrategy { get; }
    public DateTimeOffset CreatedAt { get; }

    private KnowledgeTypeDefinition(
        string typeId,
        string owner,
        string description,
        KnowledgeStorageKind storageKind,
        string lifecyclePolicy,
        Classification defaultClassification,
        bool isCanonical,
        string migrationStrategy,
        DateTimeOffset createdAt)
    {
        TypeId = typeId;
        Owner = owner;
        Description = description;
        StorageKind = storageKind;
        LifecyclePolicy = lifecyclePolicy;
        DefaultClassification = defaultClassification;
        IsCanonical = isCanonical;
        MigrationStrategy = migrationStrategy;
        CreatedAt = createdAt;
    }

    public static KnowledgeTypeDefinition Create(
        string typeId,
        string owner,
        string description,
        KnowledgeStorageKind storageKind,
        string lifecyclePolicy,
        Classification defaultClassification,
        bool isCanonical,
        string migrationStrategy,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            throw new ArgumentException("Type id is required.", nameof(typeId));
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Owner is required.", nameof(owner));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (!Enum.IsDefined(storageKind))
            throw new ArgumentOutOfRangeException(nameof(storageKind));
        if (string.IsNullOrWhiteSpace(lifecyclePolicy))
            throw new ArgumentException("Lifecycle policy is required.", nameof(lifecyclePolicy));
        if (!Enum.IsDefined(defaultClassification))
            throw new ArgumentOutOfRangeException(nameof(defaultClassification));
        if (string.IsNullOrWhiteSpace(migrationStrategy))
            throw new ArgumentException("Migration strategy is required.", nameof(migrationStrategy));

        return new KnowledgeTypeDefinition(
            typeId.Trim(),
            owner.Trim(),
            description.Trim(),
            storageKind,
            lifecyclePolicy.Trim(),
            defaultClassification,
            isCanonical,
            migrationStrategy.Trim(),
            createdAt ?? DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Knowledge create/revise paths may only target envelope-backed types.
    /// </summary>
    public bool AllowsKnowledgeEnvelope => StorageKind == KnowledgeStorageKind.KnowledgeEnvelope;
}
