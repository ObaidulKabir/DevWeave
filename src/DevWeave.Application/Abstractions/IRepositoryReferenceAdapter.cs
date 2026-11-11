using DevWeave.Application.Abstractions.Planned;

namespace DevWeave.Application.Abstractions;

/// <summary>
/// Provider-neutral Git host adapter that returns and validates repository references only.
/// Source files, diffs and patches must never cross this boundary (SRS §1.3.2, ARCH AD-11).
/// </summary>
public interface IRepositoryReferenceAdapter
{
    string Provider { get; }

    /// <summary>
    /// Validates that the provider/repository/branch/commit identity is well-formed and returns
    /// a durable opaque reference string for coordination records.
    /// </summary>
    Task<RepositoryReference> ResolveAsync(
        string repositoryId,
        string targetBranch,
        string baseCommitSha,
        CancellationToken ct = default);

    /// <summary>
    /// Builds an isolated workspace/branch reference for a work package without materializing source.
    /// </summary>
    Task<string> PrepareWorkspaceReferenceAsync(
        AuthorizedScope scope,
        Guid workPackageId,
        string repositoryId,
        string baseCommitSha,
        CancellationToken ct = default);
}

public sealed record RepositoryReference(
    string Provider,
    string RepositoryId,
    string TargetBranch,
    string BaseCommitSha,
    string CanonicalRef);
