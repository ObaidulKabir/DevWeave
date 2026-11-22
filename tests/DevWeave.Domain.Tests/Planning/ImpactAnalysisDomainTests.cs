using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Knowledge;
using DevWeave.Domain.Planning;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Planning;

public sealed class ImpactAnalysisDomainTests
{
    [Fact]
    public void RequirementChangeSet_requires_unique_idempotency_keys()
    {
        var actor = PrincipalId.New();
        var change = new RequirementChangeDescriptor(
            RequirementChangeKind.Clarified,
            [KnowledgeRevisionId.New()],
            [KnowledgeRevisionId.New()],
            actor,
            "clarify AC",
            DateTimeOffset.UtcNow,
            "idem-1");

        Assert.Throws<ArgumentException>(() =>
            RequirementChangeSet.Create(
                TenantId.New(),
                ProjectId.New(),
                actor,
                [change, change]));
    }

    [Fact]
    public void ImpactAnalysisReport_rejects_unaffected_findings()
    {
        Assert.Throws<ArgumentException>(() =>
            ImpactAnalysisReport.Create(
                TenantId.New(),
                ProjectId.New(),
                RequirementChangeSetId.New(),
                PrincipalId.New(),
                [
                    ImpactFinding.Create(
                        0,
                        KnowledgeItemId.New(),
                        KnowledgeRevisionId.New(),
                        "architecture.decision",
                        ImpactDisposition.Unaffected,
                        "Clarified/satisfies",
                        "bad"),
                ]));
    }

    [Fact]
    public void PlanRebaseDecision_records_authorized_kind_and_reason()
    {
        var decision = PlanRebaseDecision.Authorize(
            TenantId.New(),
            ProjectId.New(),
            WorkPackageId.New(),
            ImpactAnalysisReportId.New(),
            PlanRebaseDecisionKind.ContinueAfterContextRefresh,
            PrincipalId.New(),
            "refresh package context");

        Assert.Equal(PlanRebaseDecisionKind.ContinueAfterContextRefresh, decision.Kind);
        Assert.Equal("refresh package context", decision.Reason);
        Assert.True(decision.AllowsCampaignResume);
    }

    [Theory]
    [InlineData(PlanRebaseDecisionKind.ContinueUnchanged, true)]
    [InlineData(PlanRebaseDecisionKind.ContinueAfterContextRefresh, true)]
    [InlineData(PlanRebaseDecisionKind.Supersede, true)]
    [InlineData(PlanRebaseDecisionKind.Replan, false)]
    [InlineData(PlanRebaseDecisionKind.Block, false)]
    [InlineData(PlanRebaseDecisionKind.Cancel, false)]
    public void PlanRebaseDecision_resume_gate_matches_kind(PlanRebaseDecisionKind kind, bool allowsResume)
    {
        var decision = PlanRebaseDecision.Authorize(
            TenantId.New(),
            ProjectId.New(),
            WorkPackageId.New(),
            ImpactAnalysisReportId.New(),
            kind,
            PrincipalId.New(),
            "decision");

        Assert.Equal(allowsResume, decision.AllowsCampaignResume);
    }

    [Fact]
    public void ExecutionCampaign_Resume_only_from_suspended()
    {
        var campaign = ExecutionCampaign.Create(
            TenantId.New(),
            ProjectId.New(),
            Guid.NewGuid(),
            "ship",
            "Mediated");
        campaign.Activate();
        campaign.Suspend();
        campaign.Resume();
        Assert.Equal(CampaignStatus.Active, campaign.Status);
        Assert.Throws<InvalidOperationException>(campaign.Resume);
    }
}
