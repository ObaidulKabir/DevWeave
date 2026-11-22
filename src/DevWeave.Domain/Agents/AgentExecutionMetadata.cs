using DevWeave.Domain.Activities;
using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Agents;

/// <summary>
/// One-to-one provenance metadata for an agent <see cref="Activity"/> (SRS AGT-005, PROV). New agent
/// executions require this record; legacy activities without metadata remain valid. Captures provider,
/// model identity, configuration/tool fingerprints, external run reference, and authorization context
/// so a run is reproducibly attributable beyond the activity envelope alone.
/// </summary>
public sealed class AgentExecutionMetadata
{
    public ActivityId ActivityId { get; }
    public TenantId TenantId { get; }
    public string Provider { get; }
    public string Model { get; }
    public string? ModelVersion { get; }
    public string? ConfigurationFingerprint { get; }
    public string? ToolFingerprint { get; }
    public string? ExternalRunReference { get; }
    public string? AuthorizationContext { get; }

    private AgentExecutionMetadata(
        ActivityId activityId,
        TenantId tenantId,
        string provider,
        string model,
        string? modelVersion,
        string? configurationFingerprint,
        string? toolFingerprint,
        string? externalRunReference,
        string? authorizationContext)
    {
        ActivityId = activityId;
        TenantId = tenantId;
        Provider = provider;
        Model = model;
        ModelVersion = modelVersion;
        ConfigurationFingerprint = configurationFingerprint;
        ToolFingerprint = toolFingerprint;
        ExternalRunReference = externalRunReference;
        AuthorizationContext = authorizationContext;
    }

    public static AgentExecutionMetadata Create(
        ActivityId activityId,
        TenantId tenantId,
        string provider,
        string model,
        string? modelVersion = null,
        string? configurationFingerprint = null,
        string? toolFingerprint = null,
        string? externalRunReference = null,
        string? authorizationContext = null)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required.", nameof(model));

        return new AgentExecutionMetadata(
            activityId,
            tenantId,
            provider.Trim(),
            model.Trim(),
            NullIfWhiteSpace(modelVersion),
            NullIfWhiteSpace(configurationFingerprint),
            NullIfWhiteSpace(toolFingerprint),
            NullIfWhiteSpace(externalRunReference),
            NullIfWhiteSpace(authorizationContext));
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
