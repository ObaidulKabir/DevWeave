using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Lifecycle;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;

namespace DevWeave.Application.Knowledge;

/// <summary>Audit vocabulary for knowledge review decisions (GOV-007, KNW-012).</summary>
public static class KnowledgeReviewAudit
{
    public const string ResourceType = "KnowledgeItem";
    public const string ApprovedAction = "knowledge.revision.approved";
    public const string RejectedAction = "knowledge.revision.rejected";
    public const int MaxCommentLength = 2000;
}

/// <summary>
/// Records a human review decision on one specific revision. The decision is refused when that
/// revision is no longer current, and the reviewer, time, revision and comment are written to the
/// audit trail in the same transaction as the state change, so an approval can always be traced to
/// who approved exactly which content (KNW-012, PRO-005, GOV-007).
/// </summary>
public sealed class ReviewKnowledge
{
    private readonly IKnowledgeItemRepository _items;
    private readonly IProjectMembershipRepository _memberships;
    private readonly IAuditRepository _audit;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewKnowledge(
        IKnowledgeItemRepository items,
        IProjectMembershipRepository memberships,
        IAuditRepository audit,
        IUnitOfWork unitOfWork)
    {
        _items = items;
        _memberships = memberships;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<KnowledgeItem> ApproveAsync(
        TenantId tenantId,
        KnowledgeItemId itemId,
        KnowledgeRevisionId reviewedRevisionId,
        PrincipalId reviewer,
        string? comment,
        CancellationToken ct = default)
    {
        var cleaned = Clean(comment);
        var item = await RequireItemAsync(tenantId, itemId, ct);
        await EnsureReviewerAsync(item, reviewer, ct);
        item.Approve(reviewedRevisionId);
        await RecordAsync(item, reviewedRevisionId, reviewer, KnowledgeReviewAudit.ApprovedAction, "approved", cleaned, ct);
        return item;
    }

    public async Task<KnowledgeItem> RejectAsync(
        TenantId tenantId,
        KnowledgeItemId itemId,
        KnowledgeRevisionId reviewedRevisionId,
        PrincipalId reviewer,
        string reason,
        CancellationToken ct = default)
    {
        var cleaned = Clean(reason)
            ?? throw new ArgumentException("A reason is required when rejecting a revision.", nameof(reason));

        var item = await RequireItemAsync(tenantId, itemId, ct);
        await EnsureReviewerAsync(item, reviewer, ct);
        item.Reject(reviewedRevisionId);
        await RecordAsync(item, reviewedRevisionId, reviewer, KnowledgeReviewAudit.RejectedAction, "rejected", cleaned, ct);
        return item;
    }

    /// <summary>Only a project Owner or Admin may decide a review; others cannot even see the item.</summary>
    private Task EnsureReviewerAsync(KnowledgeItem item, PrincipalId reviewer, CancellationToken ct) =>
        ProjectWorkAuthorization.EnsureMemberAsync(_memberships, item.TenantId, item.ProjectId, reviewer, reviewerOnly: true, ct);

    private async Task<KnowledgeItem> RequireItemAsync(TenantId tenantId, KnowledgeItemId itemId, CancellationToken ct) =>
        await _items.GetByIdAsync(tenantId, itemId, ct)
            ?? throw new NotFoundException($"Knowledge item '{itemId}' was not found within tenant '{tenantId}'.");

    private async Task RecordAsync(
        KnowledgeItem item,
        KnowledgeRevisionId reviewedRevisionId,
        PrincipalId reviewer,
        string action,
        string decision,
        string? comment,
        CancellationToken ct)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            decision,
            projectId = item.ProjectId.Value,
            revisionId = reviewedRevisionId.Value,
            revisionNumber = item.CurrentRevisionNumber,
            comment,
        });

        await _audit.AddAsync(
            AuditRecord.Record(
                item.TenantId,
                reviewer,
                action,
                KnowledgeReviewAudit.ResourceType,
                item.Id.Value.ToString(),
                metadata),
            ct);

        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static string? Clean(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;

        if (trimmed.Length > KnowledgeReviewAudit.MaxCommentLength)
            throw new ArgumentException(
                $"Review comments are limited to {KnowledgeReviewAudit.MaxCommentLength} characters.", nameof(text));

        return trimmed;
    }
}
