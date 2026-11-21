using System.Text.Json;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Projects;

public sealed record ProjectEventId(Guid Value)
{
    public static ProjectEventId New() => new(Guid.NewGuid());
}

/// <summary>An ordered, typed resource reference owned by a project-event envelope.</summary>
public sealed record ProjectEventResourceRef(
    ProjectResourceKind Kind,
    string Identifier,
    string? RevisionIdentifier,
    int Position)
{
    public ProjectResourceRef ToResourceRef() => new(Kind, Identifier, RevisionIdentifier);
}

/// <summary>
/// An immutable fact in the append-only project journal. Current-state tables remain canonical
/// write projections; this envelope preserves why, when, by whom and against which exact inputs a
/// meaningful transition occurred.
/// </summary>
public sealed class ProjectEvent
{
    private static readonly IReadOnlySet<string> AllowedMetadataKeys =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "changeKind",
            "classification",
            "definitionVersion",
            "manifestChecksum",
            "planVersion",
            "resourceKind",
            "result",
            "schemaVersion",
            "state",
            "status",
        };

    private readonly List<ProjectEventResourceRef> _inputs;
    private readonly List<ProjectEventResourceRef> _outputs;

    public ProjectEventId Id { get; }
    public TenantId TenantId { get; }
    public ProjectId ProjectId { get; }

    /// <summary>
    /// Database-generated global sequence. A global sequence is also strictly monotonic for every
    /// individual project's subsequence.
    /// </summary>
    public long Sequence { get; private set; }

    public string EventType { get; }
    public string EventSchemaVersion { get; }
    public PrincipalId ActorId { get; }
    public string Purpose { get; }
    public DateTimeOffset ValidAt { get; }
    public DateTimeOffset RecordedAt { get; }
    public BaselineId? BaselineId { get; }
    public IReadOnlyList<ProjectEventResourceRef> Inputs => _inputs;
    public IReadOnlyList<ProjectEventResourceRef> Outputs => _outputs;
    public string MetadataJson { get; }
    public string CorrelationId { get; }
    public string IdempotencyKey { get; }
    public string Reason { get; }
    public string AuthorizationContext { get; }

    private ProjectEvent(
        ProjectEventId id,
        TenantId tenantId,
        ProjectId projectId,
        long sequence,
        string eventType,
        string eventSchemaVersion,
        PrincipalId actorId,
        string purpose,
        DateTimeOffset validAt,
        DateTimeOffset recordedAt,
        BaselineId? baselineId,
        string metadataJson,
        string correlationId,
        string idempotencyKey,
        string reason,
        string authorizationContext)
    {
        Id = id;
        TenantId = tenantId;
        ProjectId = projectId;
        Sequence = sequence;
        EventType = eventType;
        EventSchemaVersion = eventSchemaVersion;
        ActorId = actorId;
        Purpose = purpose;
        ValidAt = validAt;
        RecordedAt = recordedAt;
        BaselineId = baselineId;
        MetadataJson = metadataJson;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        Reason = reason;
        AuthorizationContext = authorizationContext;
        _inputs = [];
        _outputs = [];
    }

    public static ProjectEvent Record(
        TenantId tenantId,
        ProjectId projectId,
        string eventType,
        string eventSchemaVersion,
        PrincipalId actorId,
        string purpose,
        DateTimeOffset validAt,
        BaselineId? baselineId,
        IReadOnlyList<ProjectResourceRef>? inputs,
        IReadOnlyList<ProjectResourceRef>? outputs,
        IReadOnlyDictionary<string, string>? metadata,
        string correlationId,
        string idempotencyKey,
        string reason,
        string authorizationContext)
    {
        RequireText(eventType, nameof(eventType), 200);
        RequireText(eventSchemaVersion, nameof(eventSchemaVersion), 50);
        RequireText(purpose, nameof(purpose), 2000);
        RequireText(correlationId, nameof(correlationId), 200);
        RequireText(idempotencyKey, nameof(idempotencyKey), 200);
        RequireText(reason, nameof(reason), 2000);
        RequireText(authorizationContext, nameof(authorizationContext), 2000);

        var inputRefs = NormalizeRefs(inputs, nameof(inputs));
        var outputRefs = NormalizeRefs(outputs, nameof(outputs));
        var metadataJson = SerializeMetadata(metadata);
        var projectEvent = new ProjectEvent(
            ProjectEventId.New(),
            tenantId,
            projectId,
            sequence: 0,
            eventType.Trim(),
            eventSchemaVersion.Trim(),
            actorId,
            purpose.Trim(),
            validAt,
            DateTimeOffset.UtcNow,
            baselineId,
            metadataJson,
            correlationId.Trim(),
            idempotencyKey.Trim(),
            reason.Trim(),
            authorizationContext.Trim());

        projectEvent._inputs.AddRange(inputRefs.Select((resource, index) =>
            new ProjectEventResourceRef(
                resource.Kind,
                resource.Identifier,
                resource.RevisionIdentifier,
                index)));
        projectEvent._outputs.AddRange(outputRefs.Select((resource, index) =>
            new ProjectEventResourceRef(
                resource.Kind,
                resource.Identifier,
                resource.RevisionIdentifier,
                index)));

        return projectEvent;
    }

    private static IReadOnlyList<ProjectResourceRef> NormalizeRefs(
        IReadOnlyList<ProjectResourceRef>? resources,
        string parameterName)
    {
        var normalized = resources?.ToArray() ?? [];
        if (normalized.Select(resource => resource.CanonicalKey).Distinct(StringComparer.Ordinal).Count() !=
            normalized.Length)
            throw new ArgumentException("An event cannot contain the same resource reference twice.", parameterName);
        return normalized;
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var values = metadata ?? new Dictionary<string, string>();
        var unknown = values.Keys
            .Where(key => !AllowedMetadataKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
            throw new ArgumentException(
                $"Project event metadata contains non-allowlisted keys: {string.Join(", ", unknown)}.",
                nameof(metadata));

        var canonical = values
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return JsonSerializer.Serialize(canonical);
    }

    private static void RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (value.Length > maxLength)
            throw new ArgumentException(
                $"{parameterName} cannot exceed {maxLength} characters.",
                parameterName);
    }
}
