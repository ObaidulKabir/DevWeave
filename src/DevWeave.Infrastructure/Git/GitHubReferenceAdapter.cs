using System.Text.RegularExpressions;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Abstractions.Planned;

namespace DevWeave.Infrastructure.Git;

/// <summary>
/// First concrete Git reference adapter. Records GitHub repository/branch/commit identities only;
/// never fetches blobs, trees, diffs or patches into DevWeave storage.
/// </summary>
public sealed partial class GitHubReferenceAdapter : IRepositoryReferenceAdapter
{
    public string Provider => "github";

    public Task<RepositoryReference> ResolveAsync(
        string repositoryId,
        string targetBranch,
        string baseCommitSha,
        CancellationToken ct = default)
    {
        if (!OwnerRepoRegex().IsMatch(repositoryId))
            throw new ArgumentException("GitHub repositoryId must be 'owner/name'.", nameof(repositoryId));

        if (string.IsNullOrWhiteSpace(targetBranch))
            throw new ArgumentException("Target branch is required.", nameof(targetBranch));

        if (!ShaRegex().IsMatch(baseCommitSha))
            throw new ArgumentException("Base commit must be a 7-40 character hex SHA.", nameof(baseCommitSha));

        var canonical = $"github:{repositoryId}@{targetBranch}#{baseCommitSha.ToLowerInvariant()}";
        return Task.FromResult(new RepositoryReference(
            Provider,
            repositoryId,
            targetBranch,
            baseCommitSha.ToLowerInvariant(),
            canonical));
    }

    public async Task<string> PrepareWorkspaceReferenceAsync(
        AuthorizedScope scope,
        Guid workPackageId,
        string repositoryId,
        string baseCommitSha,
        CancellationToken ct = default)
    {
        var resolved = await ResolveAsync(repositoryId, "worktree", baseCommitSha, ct);
        // Isolated branch/worktree identity — metadata only, no clone or materialization.
        return $"{resolved.CanonicalRef}/work/{scope.ProjectId!.Value:N}/{workPackageId:N}";
    }

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9_.-]*[A-Za-z0-9])?/[A-Za-z0-9](?:[A-Za-z0-9_.-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerRepoRegex();

    [GeneratedRegex(@"^[0-9a-fA-F]{7,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShaRegex();
}
