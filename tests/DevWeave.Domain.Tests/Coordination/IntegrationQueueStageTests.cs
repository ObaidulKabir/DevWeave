using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Coordination;

public sealed class IntegrationQueueStageTests
{
    [Fact]
    public void MCO_BR_09_review_test_merge_and_verify_stay_separate_from_the_queue_decision()
    {
        var item = IntegrationQueueItem.Enqueue(
            new TenantId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            new ChangeSetId(Guid.NewGuid()),
            $"queue-{Guid.NewGuid():N}");

        item.RecordStage(IntegrationStatus.Reviewed, "Human reviewed the reference.");
        Assert.Equal(IntegrationStatus.Reviewed, item.Status);
        item.RecordStage(IntegrationStatus.Tested, "Checks passed on the host.");
        Assert.Equal(IntegrationStatus.Tested, item.Status);
        item.RecordStage(IntegrationStatus.Merged, "Host merge SHA abc1234. DevWeave did not merge.");
        Assert.Equal(IntegrationStatus.Merged, item.Status);
        item.RecordStage(IntegrationStatus.Verified, "Evidence accepted.");
        Assert.Equal(IntegrationStatus.Verified, item.Status);

        item.Decide(true, "Accepted: the work package is still part of the current plan.", 1);
        Assert.Equal(IntegrationStatus.Accepted, item.Status);
        Assert.NotEqual(IntegrationStatus.Merged, item.Status);
    }

    [Fact]
    public void MCO_BR_09_a_decided_item_does_not_accept_another_stage()
    {
        var item = IntegrationQueueItem.Enqueue(
            new TenantId(Guid.NewGuid()),
            new ProjectId(Guid.NewGuid()),
            new ChangeSetId(Guid.NewGuid()),
            $"queue-{Guid.NewGuid():N}");
        item.Decide(false, "Rejected", 1);

        Assert.Throws<InvalidOperationException>(() =>
            item.RecordStage(IntegrationStatus.Merged, "Too late"));
    }
}
