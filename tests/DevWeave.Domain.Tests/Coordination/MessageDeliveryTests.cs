using DevWeave.Domain.Coordination;
using DevWeave.Domain.Identity;
using DevWeave.Domain.Projects;

namespace DevWeave.Domain.Tests.Coordination;

public sealed class MessageDeliveryTests
{
    [Fact]
    public void MCP_035_persistence_notification_read_acknowledgement_and_acceptance_stay_separate()
    {
        var cursor = Cursor();
        var saved = MessageDelivery.ForSequence(1, cursor);

        Assert.True(saved.Persisted);
        Assert.False(saved.Notified);
        Assert.False(saved.Read);
        Assert.False(saved.Acknowledged);
        Assert.False(saved.Accepted);

        cursor.UpdateNotified(1);
        var notified = MessageDelivery.ForSequence(1, cursor);
        Assert.True(notified.Notified);
        Assert.False(notified.Read);
        Assert.False(notified.Acknowledged);
        Assert.False(notified.Accepted);

        cursor.UpdateAcknowledged(1);
        var acknowledged = MessageDelivery.ForSequence(1, cursor);
        Assert.True(acknowledged.Acknowledged);
        Assert.True(acknowledged.Notified);
        Assert.False(acknowledged.Read);
        Assert.False(acknowledged.Accepted);

        cursor.UpdateRead(1);
        var read = MessageDelivery.ForSequence(1, cursor);
        Assert.True(read.Read);
        Assert.False(read.Accepted);

        cursor.UpdateAccepted(1);
        Assert.True(MessageDelivery.ForSequence(1, cursor).Accepted);

        var noCursor = MessageDelivery.ForSequence(1, null);
        Assert.True(noCursor.Persisted);
        Assert.False(noCursor.Notified);
        Assert.False(noCursor.Read);
        Assert.False(noCursor.Acknowledged);
        Assert.False(noCursor.Accepted);
    }

    [Fact]
    public void MCP_035_a_later_cursor_does_not_rewind()
    {
        var cursor = Cursor();
        cursor.UpdateRead(2);
        cursor.UpdateRead(1);
        cursor.UpdateAcknowledged(0);

        Assert.Equal(2, cursor.LastReadSequence);
        Assert.Equal(0, cursor.LastAcknowledgedSequence);
    }

    private static CoordinationCursor Cursor() =>
        CoordinationCursor.Create(TenantId.New(), ProjectId.New(), ChannelId.New(), PrincipalId.New());
}
