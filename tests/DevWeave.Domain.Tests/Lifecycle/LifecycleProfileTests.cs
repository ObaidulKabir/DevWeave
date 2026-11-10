using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;

namespace DevWeave.Domain.Tests.Lifecycle;

/// <summary>
/// Proves LFC-001: lifecycle profiles are per-tenant configuration anchors (stable id, tenant-unique
/// key, name and version) rather than a single mandatory methodology.
/// </summary>
public sealed class LifecycleProfileTests
{
    [Fact]
    public void LFC_001_create_establishes_key_name_version_and_owning_tenant()
    {
        var tenant = Tenant.Create("Alpha Corp");

        var profile = LifecycleProfile.Create(tenant.Id, "agile-scrum", "Agile Scrum", "1.0");

        Assert.NotEqual(Guid.Empty, profile.Id.Value);
        Assert.Equal(tenant.Id, profile.TenantId);
        Assert.Equal("agile-scrum", profile.Key);
        Assert.Equal("Agile Scrum", profile.Name);
        Assert.Equal("1.0", profile.Version);
    }

    [Fact]
    public void LFC_001_key_name_and_version_are_required()
    {
        var tenant = Tenant.Create("Alpha Corp");

        Assert.Throws<ArgumentException>(() => LifecycleProfile.Create(tenant.Id, " ", "Agile", "1.0"));
        Assert.Throws<ArgumentException>(() => LifecycleProfile.Create(tenant.Id, "agile", " ", "1.0"));
        Assert.Throws<ArgumentException>(() => LifecycleProfile.Create(tenant.Id, "agile", "Agile", " "));
    }

    [Fact]
    public void LFC_001_key_is_immutable_after_creation()
    {
        var key = typeof(LifecycleProfile).GetProperty(nameof(LifecycleProfile.Key))!;

        Assert.False(key.CanWrite);
    }
}