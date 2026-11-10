using DevWeave.Domain.Identity;

namespace DevWeave.Domain.Lifecycle;

/// <summary>
/// A tenant-owned lifecycle configuration anchor (SRS LFC-001). A profile is selected by a project rather
/// than imposed as a single mandatory methodology. It carries a stable id, tenant-unique key, name and
/// version; the workflow engine (stages/transitions/activities) is a later slice.
/// </summary>
public sealed class LifecycleProfile
{
    public LifecycleProfileId Id { get; }
    public TenantId TenantId { get; }
    public string Key { get; }
    public string Name { get; private set; }
    public string Version { get; }
    public DateTimeOffset CreatedAt { get; }

    private LifecycleProfile(
        LifecycleProfileId id,
        TenantId tenantId,
        string key,
        string name,
        string version,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        Key = key;
        Name = name;
        Version = version;
        CreatedAt = createdAt;
    }

    public static LifecycleProfile Create(TenantId tenantId, string key, string name, string version)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Lifecycle profile key is required.", nameof(key));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Lifecycle profile name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Lifecycle profile version is required.", nameof(version));

        return new LifecycleProfile(
            LifecycleProfileId.New(),
            tenantId,
            key,
            name,
            version,
            DateTimeOffset.UtcNow);
    }
}