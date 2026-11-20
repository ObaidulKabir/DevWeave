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
        var ownerRepo = NormalizeRepositoryId(repositoryId);

        if (string.IsNullOrWhiteSpace(targetBranch) || targetBranch.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Target branch is required and cannot contain path traversal.", nameof(targetBranch));

        if (!ShaRegex().IsMatch(baseCommitSha))
            throw new ArgumentException("Base commit must be a 7-40 character hex SHA.", nameof(baseCommitSha));

        var sha = baseCommitSha.ToLowerInvariant();
        var canonical = $"github:{ownerRepo}@{targetBranch.Trim()}#{sha}";
        return Task.FromResult(new RepositoryReference(
            Provider,
            ownerRepo,
            targetBranch.Trim(),
            sha,
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

    /// <summary>
    /// Accepts <c>owner/name</c>, HTTPS/SSH GitHub remotes, and <c>github:owner/name</c>.
    /// Rejects credentials, local paths, and file URLs.
    /// </summary>
    public static string NormalizeRepositoryId(string repositoryId)
    {
        if (string.IsNullOrWhiteSpace(repositoryId))
            throw new ArgumentException("Repository id is required.", nameof(repositoryId));

        var raw = repositoryId.Trim();
        if (raw.Contains('\\', StringComparison.Ordinal) ||
            raw.StartsWith('/') ||
            raw.StartsWith('.') ||
            raw.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Local paths are not accepted; send a git-host identity (owner/name or remote URL).",
                nameof(repositoryId));
        }

        if (raw.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("file: URLs are not accepted.", nameof(repositoryId));

        if (raw.StartsWith("github:", StringComparison.OrdinalIgnoreCase))
            raw = raw["github:".Length..];

        if (raw.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var colon = raw.IndexOf(':');
            if (colon < 0)
                throw new ArgumentException("GitHub SSH remotes must be git@github.com:owner/name.", nameof(repositoryId));
            var host = raw["git@".Length..colon];
            EnsureGitHubHost(host, repositoryId);
            raw = raw[(colon + 1)..];
        }
        else if (raw.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                throw new ArgumentException("Repository URL is not a valid absolute URI.", nameof(repositoryId));
            if (!string.IsNullOrEmpty(uri.UserInfo) &&
                !string.Equals(uri.UserInfo, "git", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Repository URLs must not include credentials. Send owner/name or a clean remote URL.",
                    nameof(repositoryId));
            }

            EnsureGitHubHost(uri.Host, repositoryId);
            raw = uri.AbsolutePath.Trim('/');
        }

        if (raw.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            raw = raw[..^4];

        var slash = raw.IndexOf('/');
        if (slash > 0)
        {
            var remainder = raw[(slash + 1)..];
            var nextSlash = remainder.IndexOf('/');
            if (nextSlash >= 0)
                raw = raw[..(slash + 1 + nextSlash)];
        }

        if (!OwnerRepoRegex().IsMatch(raw))
            throw new ArgumentException("GitHub repositoryId must be 'owner/name'.", nameof(repositoryId));

        return raw;
    }

    private static void EnsureGitHubHost(string host, string original)
    {
        if (!string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "www.github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"GitHub adapter only accepts github.com remotes, not '{host}'.",
                nameof(original));
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9](?:[A-Za-z0-9_.-]*[A-Za-z0-9])?/[A-Za-z0-9](?:[A-Za-z0-9_.-]*[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerRepoRegex();

    [GeneratedRegex(@"^[0-9a-fA-F]{7,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShaRegex();
}
