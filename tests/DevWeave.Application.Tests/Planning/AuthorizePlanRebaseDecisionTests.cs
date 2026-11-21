using DevWeave.Application.Abstractions;
using DevWeave.Application.Planning;
using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Application.Tests.Planning;

public sealed class AuthorizePlanRebaseDecisionTests
{
    [Fact]
    public async Task Authorize_continue_resumes_suspended_campaign()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Rebase Corp");
        var actor = Principal.Create(tenant.Id, "owner@rebase.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);
        fakes.Memberships.GrantOwner(tenant.Id, project.Id, actor.Id);

        var campaign = ExecutionCampaign.Create(tenant.Id, project.Id, Guid.NewGuid(), "ship", "Mediated");
        campaign.Activate();
        campaign.Suspend();
        fakes.Coordination.Campaigns.Add(campaign);

        var package = WorkPackage.Create(
            tenant.Id, project.Id, campaign.Id, campaign.BaselineId, "wp-1", "implement cart");
        fakes.Coordination.Packages.Add(package);

        var report = ImpactAnalysisReport.Create(
            tenant.Id,
            project.Id,
            RequirementChangeSetId.New(),
            actor.Id,
            [
                ImpactFinding.Create(
                    0,
                    KnowledgeItemId.New(),
                    KnowledgeRevisionId.New(),
                    "verification.test-case",
                    ImpactDisposition.ReverificationRequired,
                    "ThresholdTightened/validates",
                    "test"),
            ]);
        fakes.Reports.Items.Add(report);

        var decision = await new AuthorizePlanRebaseDecision(
                fakes.Reports,
                fakes.Decisions,
                fakes.Coordination,
                fakes.Memberships,
                fakes.UnitOfWork)
            .ExecuteAsync(
                tenant.Id,
                project.Id,
                report.Id,
                package.Id,
                PlanRebaseDecisionKind.ContinueAfterContextRefresh,
                actor.Id,
                "refresh after NFR tighten");

        Assert.True(decision.AllowsCampaignResume);
        Assert.Equal(CampaignStatus.Active, campaign.Status);
        Assert.Single(fakes.Decisions.Items);
    }

    [Fact]
    public async Task Authorize_block_leaves_campaign_suspended()
    {
        var fakes = new Fakes();
        var tenant = Tenant.Create("Rebase Corp");
        var actor = Principal.Create(tenant.Id, "owner@rebase.test", PrincipalType.Human);
        var project = Project.Create(tenant.Id, "Shop", "shop", null, LifecycleProfileId.New(), actor.Id);
        fakes.Memberships.GrantOwner(tenant.Id, project.Id, actor.Id);

        var campaign = ExecutionCampaign.Create(tenant.Id, project.Id, Guid.NewGuid(), "ship", "Mediated");
        campaign.Activate();
        campaign.Suspend();
        fakes.Coordination.Campaigns.Add(campaign);

        var package = WorkPackage.Create(
            tenant.Id, project.Id, campaign.Id, campaign.BaselineId, "wp-1", "implement cart");
        fakes.Coordination.Packages.Add(package);

        var report = ImpactAnalysisReport.Create(
            tenant.Id,
            project.Id,
            RequirementChangeSetId.New(),
            actor.Id,
            [
                ImpactFinding.Create(
                    0,
                    KnowledgeItemId.New(),
                    null,
                    "work.package",
                    ImpactDisposition.Blocked,
                    "Withdrawn",
                    "test"),
            ]);
        fakes.Reports.Items.Add(report);

        var decision = await new AuthorizePlanRebaseDecision(
                fakes.Reports,
                fakes.Decisions,
                fakes.Coordination,
                fakes.Memberships,
                fakes.UnitOfWork)
            .ExecuteAsync(
                tenant.Id,
                project.Id,
                report.Id,
                package.Id,
                PlanRebaseDecisionKind.Block,
                actor.Id,
                "blocked until replan");

        Assert.False(decision.AllowsCampaignResume);
        Assert.Equal(CampaignStatus.Suspended, campaign.Status);
    }

    private sealed class Fakes
    {
        public InMemoryImpactAnalysisReportRepository Reports { get; } = new();
        public InMemoryPlanRebaseDecisionRepository Decisions { get; } = new();
        public StubCoordinationRepository Coordination { get; } = new();
        public OwnerMembershipRepository Memberships { get; } = new();
        public InMemoryUnitOfWork UnitOfWork { get; } = new();
    }

    private sealed class InMemoryImpactAnalysisReportRepository : IImpactAnalysisReportRepository
    {
        public List<ImpactAnalysisReport> Items { get; } = new();

        public Task AddAsync(ImpactAnalysisReport report, CancellationToken ct = default)
        {
            Items.Add(report);
            return Task.CompletedTask;
        }

        public Task<ImpactAnalysisReport?> GetByIdAsync(
            TenantId tenantId,
            ImpactAnalysisReportId id,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(report => report.TenantId == tenantId && report.Id == id));

        public Task<IReadOnlyList<ImpactAnalysisReport>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ImpactAnalysisReport>>(
                Items.Where(report => report.TenantId == tenantId && report.ProjectId == projectId).ToList());
    }

    private sealed class InMemoryPlanRebaseDecisionRepository : IPlanRebaseDecisionRepository
    {
        public List<PlanRebaseDecision> Items { get; } = new();

        public Task AddAsync(PlanRebaseDecision decision, CancellationToken ct = default)
        {
            Items.Add(decision);
            return Task.CompletedTask;
        }

        public Task<PlanRebaseDecision?> GetByIdAsync(
            TenantId tenantId,
            PlanRebaseDecisionId id,
            CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(decision => decision.TenantId == tenantId && decision.Id == id));

        public Task<PlanRebaseDecision?> GetLatestForWorkPackageAsync(
            TenantId tenantId,
            WorkPackageId workPackageId,
            CancellationToken ct = default) =>
            Task.FromResult(Items
                .Where(decision => decision.TenantId == tenantId && decision.WorkPackageId == workPackageId)
                .OrderByDescending(decision => decision.AuthorizedAt)
                .FirstOrDefault());

        public Task<IReadOnlyList<PlanRebaseDecision>> ListByImpactReportAsync(
            TenantId tenantId,
            ImpactAnalysisReportId impactReportId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PlanRebaseDecision>>(
                Items.Where(decision => decision.TenantId == tenantId && decision.ImpactReportId == impactReportId)
                    .ToList());
    }

    private sealed class OwnerMembershipRepository : IProjectMembershipRepository
    {
        private readonly List<ProjectMembership> _rows = new();

        public void GrantOwner(TenantId tenantId, ProjectId projectId, PrincipalId principalId) =>
            _rows.Add(ProjectMembership.Grant(tenantId, projectId, principalId, ProjectRole.Owner));

        public Task AddAsync(ProjectMembership membership, CancellationToken ct = default)
        {
            _rows.Add(membership);
            return Task.CompletedTask;
        }

        public Task<ProjectMembership?> GetByIdAsync(
            TenantId tenantId,
            ProjectMembershipId id,
            CancellationToken ct = default) =>
            Task.FromResult(_rows.FirstOrDefault(row => row.TenantId == tenantId && row.Id == id));

        public Task<IReadOnlyList<ProjectMembership>> ListByPrincipalAsync(
            TenantId tenantId,
            PrincipalId principalId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                _rows.Where(row => row.TenantId == tenantId && row.PrincipalId == principalId).ToList());

        public Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(
            TenantId tenantId,
            ProjectId projectId,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProjectMembership>>(
                _rows.Where(row => row.TenantId == tenantId && row.ProjectId == projectId).ToList());
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }

    private sealed class StubCoordinationRepository : ICoordinationRepository
    {
        public List<ExecutionCampaign> Campaigns { get; } = new();
        public List<WorkPackage> Packages { get; } = new();

        public Task<WorkPackage?> GetWorkPackageByIdAsync(
            TenantId tenantId,
            WorkPackageId id,
            CancellationToken ct = default) =>
            Task.FromResult(Packages.FirstOrDefault(package => package.TenantId == tenantId && package.Id == id));

        public Task<ExecutionCampaign?> GetCampaignByIdAsync(
            TenantId tenantId,
            CampaignId id,
            CancellationToken ct = default) =>
            Task.FromResult(Campaigns.FirstOrDefault(campaign => campaign.TenantId == tenantId && campaign.Id == id));

        public Task AddChannelAsync(CoordinationChannel channel, CancellationToken ct = default) => Unsupported();
        public Task<CoordinationChannel?> GetChannelByIdAsync(TenantId tenantId, ChannelId id, CancellationToken ct = default) => Unsupported<CoordinationChannel?>();
        public Task<List<CoordinationChannel>> ListChannelsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) => Unsupported<List<CoordinationChannel>>();
        public Task AddMessageAsync(CoordinationMessage message, CancellationToken ct = default) => Unsupported();
        public Task<List<CoordinationMessage>> ListMessagesAsync(TenantId tenantId, ChannelId channelId, long sinceSequence, int limit, CancellationToken ct = default) => Unsupported<List<CoordinationMessage>>();
        public Task<CoordinationMessage?> GetMessageByIdAsync(TenantId tenantId, MessageId id, CancellationToken ct = default) => Unsupported<CoordinationMessage?>();
        public Task<CoordinationMessage?> GetMessageByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default) => Unsupported<CoordinationMessage?>();
        public Task UpsertCursorAsync(CoordinationCursor cursor, CancellationToken ct = default) => Unsupported();
        public Task<CoordinationCursor?> GetCursorAsync(TenantId tenantId, ChannelId channelId, PrincipalId principalId, CancellationToken ct = default) => Unsupported<CoordinationCursor?>();
        public Task AddWorkPackageAsync(WorkPackage workPackage, CancellationToken ct = default) => Unsupported();
        public Task<WorkPackage?> GetWorkPackageByKeyAsync(TenantId tenantId, CampaignId campaignId, string key, CancellationToken ct = default) => Unsupported<WorkPackage?>();
        public Task AddLeaseAsync(TaskLease lease, CancellationToken ct = default) => Unsupported();
        public Task<TaskLease?> GetLeaseByIdAsync(TenantId tenantId, LeaseId id, CancellationToken ct = default) => Unsupported<TaskLease?>();
        public Task<TaskLease?> GetActiveLeaseAsync(TenantId tenantId, WorkPackageId workPackageId, string resourceScope, CancellationToken ct = default) => Unsupported<TaskLease?>();
        public Task<List<TaskLease>> ListLeasesByWorkPackageAsync(TenantId tenantId, WorkPackageId workPackageId, CancellationToken ct = default) => Unsupported<List<TaskLease>>();
        public Task<List<TaskLease>> ListActiveLeasesByRuntimeAsync(TenantId tenantId, RuntimeId runtimeId, CancellationToken ct = default) => Unsupported<List<TaskLease>>();
        public Task<List<TaskLease>> ListActiveLeasesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) => Unsupported<List<TaskLease>>();
        public Task AddRuntimeAsync(AgentRuntimeInstance runtime, CancellationToken ct = default) => Unsupported();
        public Task<AgentRuntimeInstance?> GetRuntimeByIdAsync(TenantId tenantId, RuntimeId id, CancellationToken ct = default) => Unsupported<AgentRuntimeInstance?>();
        public Task<AgentRuntimeInstance?> GetRuntimeBySessionKeyAsync(string sessionKey, CancellationToken ct = default) => Unsupported<AgentRuntimeInstance?>();
        public Task AddBaselineAsync(PlanBaseline baseline, CancellationToken ct = default) => Unsupported();
        public Task<PlanBaseline?> GetBaselineByIdAsync(TenantId tenantId, BaselineId id, CancellationToken ct = default) => Unsupported<PlanBaseline?>();
        public Task<List<PlanBaseline>> ListBaselinesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) => Unsupported<List<PlanBaseline>>();
        public Task AddCodebaseTargetAsync(CodebaseTarget target, CancellationToken ct = default) => Unsupported();
        public Task<CodebaseTarget?> GetCodebaseTargetByIdAsync(TenantId tenantId, CodebaseTargetId id, CancellationToken ct = default) => Unsupported<CodebaseTarget?>();
        public Task<List<CodebaseTarget>> ListCodebaseTargetsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) => Unsupported<List<CodebaseTarget>>();
        public Task<CodebaseTarget?> FindCodebaseTargetAsync(TenantId tenantId, ProjectId projectId, string provider, string repositoryId, string targetBranch, string baseCommitSha, CancellationToken ct = default) => Unsupported<CodebaseTarget?>();
        public Task AddChangeSetAsync(ChangeSet changeSet, CancellationToken ct = default) => Unsupported();
        public Task<ChangeSet?> GetChangeSetByIdAsync(TenantId tenantId, ChangeSetId id, CancellationToken ct = default) => Unsupported<ChangeSet?>();
        public Task<ChangeSet?> GetChangeSetByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default) => Unsupported<ChangeSet?>();
        public Task AddOfferAsync(ParticipationOffer offer, CancellationToken ct = default) => Unsupported();
        public Task<ParticipationOffer?> GetOfferByIdAsync(TenantId tenantId, ParticipationOfferId id, CancellationToken ct = default) => Unsupported<ParticipationOffer?>();
        public Task AddQueueItemAsync(IntegrationQueueItem item, CancellationToken ct = default) => Unsupported();
        public Task<IntegrationQueueItem?> GetQueueItemByIdAsync(TenantId tenantId, IntegrationQueueItemId id, CancellationToken ct = default) => Unsupported<IntegrationQueueItem?>();
        public Task<IntegrationQueueItem?> GetQueueItemByIdempotencyKeyAsync(TenantId tenantId, string idempotencyKey, CancellationToken ct = default) => Unsupported<IntegrationQueueItem?>();
        public Task AddKillSwitchAsync(CoordinationKillSwitch killSwitch, CancellationToken ct = default) => Unsupported();
        public Task<bool> IsTargetKilledAsync(TenantId tenantId, KillSwitchTargetType type, Guid targetId, CancellationToken ct = default) => Unsupported<bool>();
        public Task<List<ExecutionCampaign>> ListCampaignsByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) => Unsupported<List<ExecutionCampaign>>();
        public Task AddCampaignAsync(ExecutionCampaign campaign, CancellationToken ct = default) => Unsupported();
        public Task<List<WorkPackage>> ListWorkPackagesByCampaignAsync(TenantId tenantId, CampaignId campaignId, CancellationToken ct = default) => Unsupported<List<WorkPackage>>();
        public Task AddWorkthroughAsync(WorkPackageWorkthrough workthrough, CancellationToken ct = default) => Unsupported();
        public Task<WorkPackageWorkthrough?> GetLatestWorkthroughAsync(TenantId tenantId, WorkPackageId workPackageId, CancellationToken ct = default) => Unsupported<WorkPackageWorkthrough?>();
        public Task<List<WorkPackageWorkthrough>> ListWorkthroughsAsync(TenantId tenantId, WorkPackageId workPackageId, CancellationToken ct = default) => Unsupported<List<WorkPackageWorkthrough>>();
        public Task<List<AgentRuntimeInstance>> ListRuntimesByProjectAsync(TenantId tenantId, ProjectId projectId, CancellationToken ct = default) => Unsupported<List<AgentRuntimeInstance>>();

        private static Task Unsupported() => throw new NotSupportedException();
        private static Task<T> Unsupported<T>() => throw new NotSupportedException();
    }
}
