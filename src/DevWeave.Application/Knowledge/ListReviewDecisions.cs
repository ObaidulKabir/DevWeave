using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

public sealed record ReviewDecision(
    PrincipalId Reviewer,
    string Decision,
    KnowledgeRevisionId RevisionId,
    int RevisionNumber,
    string? Comment,
    DateTimeOffset DecidedAt);

/// <summary>Lists the recorded human review decisions for an item, oldest first.</summary>
public sealed class ListReviewDecisions
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IAuditRepository _audit;

    public ListReviewDecisions(IKnowledgeItemRepository items, IAuditRepository audit)
    {
        _items = items;
        _audit = audit;
    }

    public async Task<IReadOnlyList<ReviewDecision>> ExecuteAsync(
        TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct = default)
    {
        _ = await _items.GetByIdAsync(tenantId, itemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{itemId}' was not found within tenant '{tenantId}'.");

        var records = await _audit.ListForResourceAsync(
            tenantId, KnowledgeReviewAudit.ResourceType, itemId.Value.ToString(), ct);

        var decisions = new List<ReviewDecision>();
        foreach (var record in records)
        {
            if (record.Action != KnowledgeReviewAudit.ApprovedAction &&
                record.Action != KnowledgeReviewAudit.RejectedAction)
                continue;

            if (TryParse(record.MetadataJson, out var revisionId, out var revisionNumber, out var comment))
            {
                decisions.Add(new ReviewDecision(
                    record.ActorId,
                    record.Action == KnowledgeReviewAudit.ApprovedAction ? "Approved" : "Rejected",
                    new KnowledgeRevisionId(revisionId),
                    revisionNumber,
                    comment,
                    record.OccurredAt));
            }
        }

        return decisions;
    }

    private static bool TryParse(string? json, out Guid revisionId, out int revisionNumber, out string? comment)
    {
        revisionId = Guid.Empty;
        revisionNumber = 0;
        comment = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("revisionId", out var id) || !id.TryGetGuid(out revisionId))
                return false;

            if (root.TryGetProperty("revisionNumber", out var number) && number.TryGetInt32(out var parsed))
                revisionNumber = parsed;

            if (root.TryGetProperty("comment", out var text) && text.ValueKind == JsonValueKind.String)
                comment = text.GetString();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
