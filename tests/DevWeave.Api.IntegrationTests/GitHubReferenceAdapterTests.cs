using DevWeave.Application.Abstractions.Planned;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;
using DevWeave.Infrastructure.Git;

namespace DevWeave.Api.IntegrationTests;

public sealed class GitHubReferenceAdapterTests
{
    private readonly GitHubReferenceAdapter _adapter = new();

    [Fact]
    public async Task ResolveAsync_accepts_owner_repo_and_sha()
    {
        var resolved = await _adapter.ResolveAsync("acme/checkout", "main", "Abc1234");

        Assert.Equal("github", resolved.Provider);
        Assert.Equal("acme/checkout", resolved.RepositoryId);
        Assert.Equal("abc1234", resolved.BaseCommitSha);
        Assert.Equal("github:acme/checkout@main#abc1234", resolved.CanonicalRef);
    }

    [Fact]
    public async Task ResolveAsync_rejects_invalid_repository_ids()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _adapter.ResolveAsync("not-a-repo", "main", "abc1234"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _adapter.ResolveAsync("../secrets", "main", "abc1234"));
    }

    [Fact]
    public async Task PrepareWorkspaceReferenceAsync_returns_isolated_ref_without_source()
    {
        var scope = new AuthorizedScope(
            new TenantId(Guid.NewGuid()),
            new PrincipalId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            "test",
            "trace");

        var reference = await _adapter.PrepareWorkspaceReferenceAsync(
            scope, Guid.NewGuid(), "acme/checkout", "deadbeef");

        Assert.Contains("github:acme/checkout", reference, StringComparison.Ordinal);
        Assert.DoesNotContain("class ", reference, StringComparison.Ordinal);
    }
}
