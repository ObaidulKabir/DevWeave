using DevWeave.Application.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Artifacts;

public sealed class ArtifactObjectKeyTests
{
    [Fact]
    public void BuildObjectKey_embeds_tenant_project_key_hash_version_and_unique_suffix()
    {
        var tenant = new TenantId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        var project = new ProjectId(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        var first = UploadArtifact.BuildObjectKey(tenant, project, "Design/Spec", 1);
        var second = UploadArtifact.BuildObjectKey(tenant, project, "Design/Spec", 1);
        var otherLogical = UploadArtifact.BuildObjectKey(tenant, project, "design-spec", 1);

        Assert.StartsWith("aaaaaaaabbbbccccddddeeeeeeeeeeee/11111111222233334444555555555555/artifacts/", first);
        Assert.Contains("/v1/", first);
        Assert.NotEqual(first, second);
        Assert.NotEqual(first.Split('/')[3], otherLogical.Split('/')[3]);
    }
}
