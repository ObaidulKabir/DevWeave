using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;
using DevWeave.Domain.Specifications;

namespace DevWeave.Application.Specifications;

internal static class SpecItems
{
    /// <summary>Only a human-approved, current revision is binding (KNW-003).</summary>
    public static bool IsBinding(KnowledgeItem item) =>
        item.CurrentRevisionId is not null &&
        item.ReviewState == ReviewState.Reviewed &&
        item.State is InfoState.Approved or InfoState.Verified;
}

/// <summary>
/// Builds the effective catalog for a project: the built-in packs plus approved tenant-level and
/// project-level overlays. Overlays that no longer validate are skipped and reported, never applied.
/// </summary>
public sealed class SpecCatalogService(IKnowledgeItemRepository items, IKnowledgeRevisionRepository revisions)
{
    public static IReadOnlyList<SpecPack> Packs { get; } = SpecPackLoader.LoadBuiltIn();

    public static IReadOnlySet<string> BuiltInRoots { get; } =
        Packs.SelectMany(p => p.Topics).Select(t => SpecCatalogBuilder.Root(t.Id)).ToHashSet(StringComparer.Ordinal);

    public async Task<SpecCatalog> BuildAsync(TenantId tenant, ProjectId project, CancellationToken ct = default)
    {
        var overlays = new List<(SpecOverlay Overlay, string Source, DateTimeOffset At)>();
        var all = await items.ListByTenantAsync(tenant, ct);
        foreach (var item in all.Where(i => i.TypeId == SpecOverlayJson.TypeId && SpecItems.IsBinding(i)))
        {
            var revision = await revisions.GetByIdAsync(tenant, item.CurrentRevisionId!, ct);
            if (revision is null)
                continue;

            var overlay = SpecOverlayJson.TryParse(revision.Content, out _);
            if (overlay is null)
                continue;

            // A project overlay only applies to its own project; a tenant overlay applies to every project.
            if (overlay.Level == SpecScopeLevel.Project && item.ProjectId != project)
                continue;

            overlays.Add((overlay, $"{overlay.Level.ToString().ToLowerInvariant()}:{item.Key}", item.CreatedAt));
        }

        var ordered = overlays
            .OrderBy(o => o.Overlay.Level)
            .ThenBy(o => o.At)
            .Select(o => (o.Overlay, o.Source))
            .ToList();
        return SpecCatalogBuilder.Build(Packs, ordered);
    }
}
