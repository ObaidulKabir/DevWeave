using DevWeave.Domain.Identity;
using DevWeave.Domain.Lifecycle;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Lifecycle;

public sealed class WorkItemTests
{
    [Fact]
    public void LFC_005_creation_separates_planned_reported_verified_and_approved_states()
    {
        var item = Create();

        Assert.Equal(PlannedWorkState.Planned, item.PlannedState);
        Assert.Equal(ReportedExecutionState.NotStarted, item.ReportedState);
        Assert.Equal(WorkVerificationState.Unverified, item.VerificationState);
        Assert.Equal(WorkApprovalState.Pending, item.ApprovalState);
        Assert.Equal(1, item.Version);
    }

    [Fact]
    public void LFC_005_report_does_not_advance_planned_state_or_verification()
    {
        var actor = PrincipalId.New();
        var item = Create();

        var events = item.Report(ReportedExecutionState.Completed, actor, "done", 1);

        Assert.Equal(PlannedWorkState.Planned, item.PlannedState);
        Assert.Equal(ReportedExecutionState.Completed, item.ReportedState);
        Assert.Equal(WorkVerificationState.Unverified, item.VerificationState);
        Assert.Equal(WorkApprovalState.Pending, item.ApprovalState);
        Assert.Single(events);
        Assert.Equal(WorkItemTransitionDimension.Reported, events[0].Dimension);
    }

    [Fact]
    public void LFC_005_verification_and_approval_require_distinct_reviewers()
    {
        var reporter = PrincipalId.New();
        var verifier = PrincipalId.New();
        var approver = PrincipalId.New();
        var item = Create();

        item.Report(ReportedExecutionState.Completed, reporter, "done", 1);
        Assert.Throws<InvalidOperationException>(() =>
            item.Approve(approver, "approve", item.Version));

        item.Verify(verifier, "acceptance evidence checked", item.Version);
        Assert.Equal(WorkVerificationState.Verified, item.VerificationState);
        Assert.Equal(WorkApprovalState.Pending, item.ApprovalState);
        Assert.Throws<InvalidOperationException>(() =>
            item.Approve(verifier, "self approval", item.Version));
        Assert.Throws<InvalidOperationException>(() =>
            item.Approve(reporter, "reporter approval", item.Version));

        var approval = item.Approve(approver, "approved", item.Version);
        Assert.Equal(WorkApprovalState.Approved, item.ApprovalState);
        Assert.Equal(WorkItemTransitionDimension.Approval, approval.Dimension);
    }

    [Fact]
    public void LFC_005_the_reporter_cannot_verify_their_own_work()
    {
        var reporter = PrincipalId.New();
        var verifier = PrincipalId.New();
        var item = Create();
        item.Report(ReportedExecutionState.Completed, reporter, "done", 1);

        Assert.Throws<InvalidOperationException>(() =>
            item.Verify(reporter, "self verification", item.Version));

        var verified = item.Verify(verifier, "checked", item.Version);
        Assert.Equal(WorkVerificationState.Verified, item.VerificationState);
        Assert.Equal(WorkItemTransitionDimension.Verification, verified.Dimension);
    }

    [Fact]
    public void LFC_005_a_rejected_approval_can_be_reopened_and_approved_after_rework()
    {
        var reporter = PrincipalId.New();
        var verifier = PrincipalId.New();
        var approver = PrincipalId.New();
        var item = Create();
        item.Report(ReportedExecutionState.Completed, reporter, "done", 1);
        item.Verify(verifier, "checked", item.Version);
        item.RejectApproval(approver, "does not meet the criterion", item.Version);
        Assert.Equal(WorkApprovalState.Rejected, item.ApprovalState);

        // Without reopening, approval can never be reached again: Approve requires Pending.
        Assert.Throws<InvalidOperationException>(() => item.Approve(approver, "retry", item.Version));

        // Reworking (any report, even unchanged) reopens approval instead of leaving it stuck Rejected.
        var transitions = item.Report(ReportedExecutionState.InProgress, reporter, "reworking", item.Version);
        Assert.Contains(transitions, t => t.Dimension == WorkItemTransitionDimension.Approval);
        Assert.Equal(WorkApprovalState.Pending, item.ApprovalState);

        item.Report(ReportedExecutionState.Completed, reporter, "done again", item.Version);
        item.Verify(verifier, "checked again", item.Version);
        var approved = item.Approve(approver, "now it meets the criterion", item.Version);
        Assert.Equal(WorkApprovalState.Approved, item.ApprovalState);
        Assert.Equal(WorkItemTransitionDimension.Approval, approved.Dimension);
    }

    [Fact]
    public void LFC_005_changed_execution_invalidates_verification_and_approval_with_audit_events()
    {
        var reporter = PrincipalId.New();
        var verifier = PrincipalId.New();
        var approver = PrincipalId.New();
        var item = Create();
        item.Report(ReportedExecutionState.Completed, reporter, "done", 1);
        item.Verify(verifier, "verified", item.Version);
        item.Approve(approver, "approved", item.Version);

        var transitions = item.Report(ReportedExecutionState.InProgress, reporter, "reopened", item.Version);

        Assert.Equal(3, transitions.Count);
        Assert.Equal(WorkVerificationState.Unverified, item.VerificationState);
        Assert.Equal(WorkApprovalState.Pending, item.ApprovalState);
        Assert.Equal(new[]
        {
            WorkItemTransitionDimension.Reported,
            WorkItemTransitionDimension.Verification,
            WorkItemTransitionDimension.Approval,
        }, transitions.Select(transition => transition.Dimension));
        Assert.Equal(item.Version, transitions[^1].Version);
    }

    [Fact]
    public void LFC_005_stale_versions_and_illegal_state_transitions_are_rejected()
    {
        var item = Create();
        var actor = PrincipalId.New();

        var first = item.ChangePlannedState(PlannedWorkState.Ready, actor, "ready", 1);
        Assert.Equal(2, first.Version);
        Assert.Throws<WorkItemConcurrencyException>(() =>
            item.ChangePlannedState(PlannedWorkState.InProgress, actor, "stale", 1));
        Assert.Throws<InvalidOperationException>(() =>
            item.ChangePlannedState(PlannedWorkState.Planned, actor, "reopen plan", item.Version));
    }

    [Fact]
    public void LFC_005_dependency_cannot_reference_the_same_work_item()
    {
        var itemId = WorkItemId.New();
        Assert.Throws<ArgumentException>(() => WorkItemDependency.Create(
            new TenantId(Guid.NewGuid()), new ProjectId(Guid.NewGuid()), itemId, itemId, PrincipalId.New()));
    }

    private static WorkItem Create() =>
        WorkItem.Create(new TenantId(Guid.NewGuid()), new ProjectId(Guid.NewGuid()),
            "DW-1", "Implement endpoint", "Work item description", null, null, null,
            WorkPriority.Normal, PrincipalId.New());
}
