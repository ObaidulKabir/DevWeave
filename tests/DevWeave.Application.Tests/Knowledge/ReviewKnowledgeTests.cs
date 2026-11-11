using DevWeave.Application.Abstractions;
using DevWeave.Application.Knowledge;
using DevWeave.Domain.Audit;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Knowledge;

/// <summary>
/// Review decisions are bound to one revision and leave a traceable record of who decided, on which
/// revision, when and why (KNW-012, PRO-005, GOV-007).
/// </summary>
public sealed class ReviewKnowledgeTests
{
    private static readonly TenantId Tenant = TenantId.New();
    private static readonly ProjectId Project = ProjectId.New();
    private static readonly PrincipalId Author = PrincipalId.New();
    private static readonly PrincipalId Reviewer = PrincipalId.New();

    [Fact]
    public async Task Approving_a_revision_records_reviewer_revision_and_comment()
    {
        var (items, audit, uow, item) = Arrange();
        var revision = item.AddRevision("1", "{}", Author, "draft", null, null, 0);
        var review = new ReviewKnowledge(items, audit, uow);

        var approved = await review.ApproveAsync(Tenant, item.Id, revision.Id, Reviewer, "  Looks right  ");

        Assert.Equal(InfoState.Approved, approved.State);
        Assert.Equal(ReviewState.Reviewed, approved.ReviewState);
        var decision = Assert.Single(await new ListReviewDecisions(items, audit).ExecuteAsync(Tenant, item.Id));
        Assert.Equal("Approved", decision.Decision);
        Assert.Equal(Reviewer, decision.Reviewer);
        Assert.Equal(revision.Id, decision.RevisionId);
        Assert.Equal(1, decision.RevisionNumber);
        Assert.Equal("Looks right", decision.Comment);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task Approving_a_superseded_revision_is_refused_and_records_nothing()
    {
        var (items, audit, uow, item) = Arrange();
        var first = item.AddRevision("1", "{}", Author, "draft", null, null, 0);
        item.AddRevision("1", "{\"changed\":true}", Author, "edited during review", null, null, 1);
        var review = new ReviewKnowledge(items, audit, uow);

        await Assert.ThrowsAsync<StaleReviewException>(() =>
            review.ApproveAsync(Tenant, item.Id, first.Id, Reviewer, null));

        Assert.Equal(InfoState.Proposed, item.State);
        Assert.Empty(audit.Records);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task Rejecting_requires_a_reason_and_records_it()
    {
        var (items, audit, uow, item) = Arrange();
        var revision = item.AddRevision("1", "{}", Author, "draft", null, null, 0);
        var review = new ReviewKnowledge(items, audit, uow);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            review.RejectAsync(Tenant, item.Id, revision.Id, Reviewer, "   "));
        Assert.Equal(InfoState.Proposed, item.State);

        await review.RejectAsync(Tenant, item.Id, revision.Id, Reviewer, "Missing acceptance criteria");

        Assert.Equal(InfoState.Rejected, item.State);
        var decision = Assert.Single(await new ListReviewDecisions(items, audit).ExecuteAsync(Tenant, item.Id));
        Assert.Equal("Rejected", decision.Decision);
        Assert.Equal("Missing acceptance criteria", decision.Comment);
    }

    [Fact]
    public async Task Overlong_comments_are_refused()
    {
        var (items, audit, uow, item) = Arrange();
        var revision = item.AddRevision("1", "{}", Author, "draft", null, null, 0);
        var review = new ReviewKnowledge(items, audit, uow);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            review.ApproveAsync(Tenant, item.Id, revision.Id, Reviewer, new string('x', KnowledgeReviewAudit.MaxCommentLength + 1)));
    }

    [Fact]
    public async Task Unknown_items_are_not_found()
    {
        var (items, audit, uow, _) = Arrange();
        var review = new ReviewKnowledge(items, audit, uow);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            review.ApproveAsync(Tenant, KnowledgeItemId.New(), KnowledgeRevisionId.New(), Reviewer, null));
    }

    private static (InMemoryItems Items, InMemoryAudit Audit, CountingUnitOfWork Uow, KnowledgeItem Item) Arrange()
    {
        var item = KnowledgeItem.Create(
            Tenant, Project, "req-1", "A requirement", Classification.Internal,
            RepresentationKind.Text, "doc.note", "1", Author);
        var items = new InMemoryItems();
        items.Store.Add(item);
        return (items, new InMemoryAudit(), new CountingUnitOfWork(), item);
    }

    private sealed class InMemoryItems : IKnowledgeItemRepository
    {
        public List<KnowledgeItem> Store { get; } = [];

        public Task AddAsync(KnowledgeItem item, CancellationToken ct = default)
        {
            Store.Add(item);
            return Task.CompletedTask;
        }

        public Task<KnowledgeItem?> GetByIdAsync(TenantId tenantId, KnowledgeItemId id, CancellationToken ct = default) =>
            Task.FromResult(Store.FirstOrDefault(i => i.TenantId == tenantId && i.Id == id));

        public Task<IReadOnlyList<KnowledgeItem>> ListByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(Store.Where(i => i.TenantId == tenantId && i.ProjectId == projectId).ToList());

        public Task<IReadOnlyList<KnowledgeItem>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeItem>>(Store.Where(i => i.TenantId == tenantId).ToList());

        public Task<bool> KeyExistsAsync(TenantId tenantId, ProjectId projectId, string key, CancellationToken ct = default) =>
            Task.FromResult(Store.Any(i => i.TenantId == tenantId && i.ProjectId == projectId && i.Key == key));
    }

    private sealed class InMemoryAudit : IAuditRepository
    {
        public List<AuditRecord> Records { get; } = [];

        public Task AddAsync(AuditRecord record, CancellationToken ct = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task<AuditRecord?> GetByIdAsync(TenantId tenantId, AuditRecordId id, CancellationToken ct = default) =>
            Task.FromResult(Records.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));

        public Task<List<AuditRecord>> ListByTenantAsync(TenantId tenantId, CancellationToken ct = default) =>
            Task.FromResult(Records.Where(r => r.TenantId == tenantId).OrderByDescending(r => r.OccurredAt).ToList());

        public Task<List<AuditRecord>> ListForResourceAsync(TenantId tenantId, string resourceType, string resourceId, CancellationToken ct = default) =>
            Task.FromResult(Records
                .Where(r => r.TenantId == tenantId && r.ResourceType == resourceType && r.ResourceId == resourceId)
                .OrderBy(r => r.OccurredAt)
                .ToList());
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(0);
        }
    }
}
