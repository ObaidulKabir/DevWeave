namespace DevWeave.Domain.Schema;

/// <summary>
/// A platform-global JSON Schema definition (SRS SCH-001, SCH-002, SCH-003). A definition is keyed
/// by the pair (Namespace, Version): the namespace identifies a content type (aligned with a
/// knowledge item's <c>TypeId</c>) and the version pinpoints one draft of its shape (aligned with the
/// item's <c>SchemaVersion</c>). The document itself is stored as a raw JSON Schema Draft 2020-12
/// string; parsing and evaluation are the responsibility of the infrastructure validation adapter.
///
/// Published definitions are immutable (SCH-003): there is no in-place mutation of the namespace,
/// version, or document once Published, so any change to a published type must be registered as a
/// new version.
/// </summary>
public sealed class SchemaDefinition
{
    public SchemaDefinitionId Id { get; }
    public string Namespace { get; }
    public string Version { get; }
    public string Owner { get; }
    public string Description { get; }
    public SchemaStatus Status { get; private set; }
    public string Document { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? PublishedAt { get; private set; }

    private SchemaDefinition(
        SchemaDefinitionId id,
        string @namespace,
        string version,
        string owner,
        string description,
        SchemaStatus status,
        string document,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt)
    {
        Id = id;
        Namespace = @namespace;
        Version = version;
        Owner = owner;
        Description = description;
        Status = status;
        Document = document;
        CreatedAt = createdAt;
        PublishedAt = publishedAt;
    }

    public static SchemaDefinition Create(
        string @namespace,
        string version,
        string owner,
        string description,
        string document)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
            throw new ArgumentException("Schema namespace is required.", nameof(@namespace));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Schema version is required.", nameof(version));

        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Schema owner is required.", nameof(owner));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Schema description is required.", nameof(description));

        if (string.IsNullOrWhiteSpace(document))
            throw new ArgumentException("Schema document is required.", nameof(document));

        return new SchemaDefinition(
            SchemaDefinitionId.New(),
            @namespace,
            version,
            owner,
            description,
            SchemaStatus.Draft,
            document,
            DateTimeOffset.UtcNow,
            publishedAt: null);
    }

    /// <summary>
    /// Makes a draft schema referencing-able (SRS SCH-003 / KNW-002). A deprecated schema can never
    /// be republished; publishing an already-published schema is rejected as a duplicate transition.
    /// </summary>
    public void Publish()
    {
        if (Status == SchemaStatus.Published)
            throw new InvalidOperationException("Schema is already published.");

        if (Status == SchemaStatus.Deprecated)
            throw new InvalidOperationException("A deprecated schema cannot be published.");

        Status = SchemaStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Deprecates a schema so new revisions cannot reference it (SRS SCH-003). Deprecation does not
    /// mutate the document, so already-committed revisions remain valid and interpretable.
    /// </summary>
    public void Deprecate()
    {
        if (Status == SchemaStatus.Deprecated)
            throw new InvalidOperationException("Schema is already deprecated.");

        Status = SchemaStatus.Deprecated;
    }

    /// <summary>
    /// Only Published (and therefore immutable) schemas are usable as the target of a knowledge
    /// revision (SRS KNW-002).
    /// </summary>
    public bool IsActive => Status == SchemaStatus.Published;
}