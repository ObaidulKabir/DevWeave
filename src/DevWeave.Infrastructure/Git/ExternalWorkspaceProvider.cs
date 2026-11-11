using System.Text.RegularExpressions;
using DevWeave.Application.Abstractions.Planned;

namespace DevWeave.Infrastructure.Git;

/// <summary>
/// Emits isolated external workspace references. Source content is never returned or stored.
/// </summary>
public sealed partial class ExternalWorkspaceProvider : IExternalWorkspaceProvider
{
    public Task<string> PrepareReferenceAsync(
        AuthorizedScope scope,
        Guid workPackageId,
        string baseCommitSha,
        CancellationToken ct = default)
    {
        if (!ShaRegex().IsMatch(baseCommitSha))
            throw new ArgumentException("Base commit must be a 7-40 character hex SHA.", nameof(baseCommitSha));

        // Opaque workspace identity for an isolated branch/worktree — references only.
        var reference =
            $"workspace:tenant/{scope.TenantId.Value:N}/project/{scope.ProjectId!.Value:N}/work/{workPackageId:N}/base/{baseCommitSha.ToLowerInvariant()}";
        return Task.FromResult(reference);
    }

    [GeneratedRegex(@"^[0-9a-fA-F]{7,40}$", RegexOptions.CultureInvariant)]
    private static partial Regex ShaRegex();
}
