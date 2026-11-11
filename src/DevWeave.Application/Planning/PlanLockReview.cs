using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Planning;

/// <summary>One plan item, pinned to the exact approved revision a baseline would lock.</summary>
public sealed record PlanLockItem(
    KnowledgeItemId ItemId,
    string Key,
    string Title,
    string TypeId,
    KnowledgeRevisionId RevisionId,
    int RevisionNumber,
    string? ParentKey);

/// <summary>A reason the plan cannot be locked yet.</summary>
public sealed record PlanLockBlocker(string Key, string Reason);

/// <summary>What locking the plan would capture, and what (if anything) prevents it.</summary>
public sealed record PlanLockPreview(IReadOnlyList<PlanLockItem> Items, IReadOnlyList<PlanLockBlocker> Blockers)
{
    public bool CanLock => Items.Count > 0 && Blockers.Count == 0;

    public string Describe() =>
        "The plan cannot be locked yet: " +
        string.Join("; ", Blockers.Select(b => string.IsNullOrEmpty(b.Key) ? b.Reason : $"{b.Key} — {b.Reason}"));
}

/// <summary>
/// Single source of truth for "what is the approved plan". A baseline may only lock the current
/// revision of every item in the project's plan outline, and only when each of those revisions has
/// been reviewed by a human. Both the Studio review screen and the baseline lock use this, so what a
/// person reviews is exactly what is locked (MCO-BR-01, MCO-BR-10, KNW-012).
/// </summary>
public sealed class PlanLockReview
{
    private readonly IPlanDesignRepository _plans;
    private readonly IKnowledgeItemRepository _items;

    public PlanLockReview(IPlanDesignRepository plans, IKnowledgeItemRepository items)
    {
        _plans = plans;
        _items = items;
    }

    public async Task<PlanLockPreview> PreviewAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default)
    {
        var nodes = await _plans.ListNodesAsync(tenantId, projectId, ct);
        if (nodes.Count == 0)
        {
            return new PlanLockPreview(
                Array.Empty<PlanLockItem>(),
                new[]
                {
                    new PlanLockBlocker(
                        string.Empty,
                        "The plan outline is empty. Attach the application idea as the outline root and get it approved before locking."),
                });
        }

        var loaded = new List<(PlanOutlineNodeRef Node, KnowledgeItem? Item)>();
        foreach (var node in nodes)
        {
            var item = await _items.GetByIdAsync(tenantId, node.ItemId, ct);
            loaded.Add((new PlanOutlineNodeRef(node.ItemId, node.ParentItemId),
                item is not null && item.ProjectId == projectId ? item : null));
        }

        var keyById = loaded
            .Where(entry => entry.Item is not null)
            .ToDictionary(entry => entry.Node.ItemId, entry => entry.Item!.Key);

        var included = new List<PlanLockItem>();
        var blockers = new List<PlanLockBlocker>();
        foreach (var (node, item) in loaded)
        {
            if (item is null)
            {
                blockers.Add(new PlanLockBlocker(node.ItemId.Value.ToString(), "the outline refers to an item that is not in this project"));
                continue;
            }

            if (item.CurrentRevisionId is not { } revisionId)
            {
                blockers.Add(new PlanLockBlocker(item.Key, "has no content revision yet"));
                continue;
            }

            if (item.ReviewState != ReviewState.Reviewed)
            {
                blockers.Add(new PlanLockBlocker(
                    item.Key,
                    $"revision {item.CurrentRevisionNumber} is {item.ReviewState} and has not been approved"));
                continue;
            }

            string? parentKey = null;
            if (node.ParentItemId is { } parentId)
                keyById.TryGetValue(parentId, out parentKey);

            included.Add(new PlanLockItem(
                item.Id, item.Key, item.Title, item.TypeId, revisionId, item.CurrentRevisionNumber, parentKey));
        }

        return new PlanLockPreview(
            included.OrderBy(i => i.Key, StringComparer.Ordinal).ToList(),
            blockers);
    }

    private readonly record struct PlanOutlineNodeRef(KnowledgeItemId ItemId, KnowledgeItemId? ParentItemId);
}
