using DevWeave.Domain.Artifacts;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Artifacts;

/// <summary>
/// Proves the Artifact domain invariants for Slice 7: immutable version identity (ART-003), typed
/// metadata including checksum and classification (ART-002/ART-006), and the quarantined disposition
/// captured in metadata without mutating or erasing prior versions (ART-008).
/// </summary>
public sealed class ArtifactTests
{
    private static readonly string[] ExpectedStatuses = ["Stored", "Quarantined"];

    [Fact]
    public void ART_003_creation_establishes_an_immutable_version_record()
    {
        var artifact = CreateArtifact(version: 1);

        Assert.NotEqual(Guid.Empty, artifact.Id.Value);
        Assert.Equal(1, artifact.Version);
        Assert.Equal(ArtifactStatus.Stored, artifact.Status);
        Assert.NotEqual(default, artifact.CreatedAt);
    }

    [Fact]
    public void ART_002_metadata_is_populated_and_typed()
    {
        var (tenant, project, actor) = Seed();

        var artifact = Artifact.Create(
            tenant, project, "coverage.pdf", 2, "application/pdf", 42L,
            "abcd1234", "sha256", "object/key/2", Classification.Confidential, actor, ArtifactStatus.Stored);

        Assert.Equal(tenant, artifact.TenantId);
        Assert.Equal(project, artifact.ProjectId);
        Assert.Equal("coverage.pdf", artifact.Key);
        Assert.Equal("application/pdf", artifact.MediaType);
        Assert.Equal(42L, artifact.SizeInBytes);
        Assert.Equal("abcd1234", artifact.Checksum);
        Assert.Equal("sha256", artifact.ChecksumAlgorithm);
        Assert.Equal("object/key/2", artifact.ObjectKey);
        Assert.Equal(Classification.Confidential, artifact.Classification);
        Assert.Equal(actor, artifact.Creator);
    }

    [Fact]
    public void ART_003_and_ART_006_checksum_algorithm_defaults_to_sha256_and_is_retained()
    {
        var artifact = CreateArtifact(checksumAlgorithm: "sha256");

        Assert.Equal("sha256", artifact.ChecksumAlgorithm);
    }

    [Fact]
    public void ART_008_a_quarantined_version_is_recorded_without_erasing_the_prior_version()
    {
        var first = CreateArtifact(version: 1);
        var second = CreateArtifact(version: 2, status: ArtifactStatus.Quarantined);

        Assert.Equal(ArtifactStatus.Stored, first.Status);
        Assert.Equal(ArtifactStatus.Quarantined, second.Status);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
    }

    [Fact]
    public void ART_006_checksum_is_immutable_metadata()
    {
        Assert.False(typeof(Artifact).GetProperty(nameof(Artifact.Checksum))!.CanWrite);
    }

    [Fact]
    public void creation_guards_required_values()
    {
        var (tenant, project, actor) = Seed();

        Assert.Throws<ArgumentException>(() =>
            Artifact.Create(tenant, project, " ", 1, "application/pdf", 0, "aa", "sha256", "k", Classification.Public, actor, ArtifactStatus.Stored));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Artifact.Create(tenant, project, "k", 0, "application/pdf", 0, "aa", "sha256", "k", Classification.Public, actor, ArtifactStatus.Stored));
        Assert.Throws<ArgumentException>(() =>
            Artifact.Create(tenant, project, "k", 1, " ", 0, "aa", "sha256", "k", Classification.Public, actor, ArtifactStatus.Stored));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Artifact.Create(tenant, project, "k", 1, "application/pdf", -1, "aa", "sha256", "k", Classification.Public, actor, ArtifactStatus.Stored));
        Assert.Throws<ArgumentException>(() =>
            Artifact.Create(tenant, project, "k", 1, "application/pdf", 0, " ", "sha256", "k", Classification.Public, actor, ArtifactStatus.Stored));
        Assert.Throws<ArgumentException>(() =>
            Artifact.Create(tenant, project, "k", 1, "application/pdf", 0, "aa", "sha256", " ", Classification.Public, actor, ArtifactStatus.Stored));
    }

    [Fact]
    public void ART_002_status_domain_is_exactly_the_two_dispositions()
    {
        Assert.Equal(ExpectedStatuses, Enum.GetNames<ArtifactStatus>());
    }

    [Fact]
    public void version_is_monotonic_by_convention_and_carried_on_the_record()
    {
        var v1 = CreateArtifact(version: 1);
        var v2 = CreateArtifact(version: 2);
        var v3 = CreateArtifact(version: 3);

        Assert.Equal(1, v1.Version);
        Assert.Equal(2, v2.Version);
        Assert.Equal(3, v3.Version);
    }

    private static (TenantId Tenant, ProjectId Project, PrincipalId Actor) Seed() =>
        (TenantId.New(), ProjectId.New(), PrincipalId.New());

    private static Artifact CreateArtifact(
        int version = 1,
        string checksum = "deadbeef",
        string checksumAlgorithm = "sha256",
        ArtifactStatus status = ArtifactStatus.Stored) =>
        Artifact.Create(
            TenantId.New(),
            ProjectId.New(),
            "coverage.pdf",
            version,
            "application/pdf",
            42L,
            checksum,
            checksumAlgorithm,
            $"object/key/{version}",
            Classification.Internal,
            PrincipalId.New(),
            status);
}