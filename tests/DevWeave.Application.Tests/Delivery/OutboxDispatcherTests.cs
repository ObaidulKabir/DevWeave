using DevWeave.Application.Abstractions;
using DevWeave.Application.Delivery;
using DevWeave.Domain.Common;
using DevWeave.Domain.Identity;

namespace DevWeave.Application.Tests.Delivery;

public sealed class OutboxDispatcherTests
{
    [Fact]
    public async Task A_failed_delivery_is_retried_then_poisoned()
    {
        var store = new MemoryOutbox();
        store.Messages.Add(Message());
        var dispatcher = new OutboxDispatcher(store);
        var attempts = 0;

        for (var cycle = 0; cycle < OutboxDispatcher.MaxAttempts; cycle++)
            await dispatcher.DispatchAsync((_, _) =>
            {
                attempts++;
                throw new InvalidOperationException("downstream unavailable");
            });

        await dispatcher.DispatchAsync((_, _) => Task.CompletedTask);

        Assert.Equal(OutboxDispatcher.MaxAttempts, attempts);
        Assert.Null(store.Messages[0].ProcessedAt);
        Assert.NotNull(store.Messages[0].PoisonedAt);
        Assert.Equal(OutboxDispatcher.MaxAttempts, store.Messages[0].AttemptCount);
    }

    [Fact]
    public async Task A_successful_retry_delivers_once()
    {
        var store = new MemoryOutbox();
        store.Messages.Add(Message());
        var dispatcher = new OutboxDispatcher(store);
        var seen = 0;

        await dispatcher.DispatchAsync((_, _) => throw new InvalidOperationException("once"));
        await dispatcher.DispatchAsync((_, _) =>
        {
            seen++;
            return Task.CompletedTask;
        });
        await dispatcher.DispatchAsync((_, _) =>
        {
            seen++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, seen);
        Assert.NotNull(store.Messages[0].ProcessedAt);
    }

    [Fact]
    public async Task A_batch_is_delivered_once_each()
    {
        var store = new MemoryOutbox();
        for (var i = 0; i < 200; i++)
            store.Messages.Add(Message());
        var dispatcher = new OutboxDispatcher(store);
        var ids = new HashSet<Guid>();

        var delivered = await dispatcher.DispatchAsync((message, _) =>
        {
            Assert.True(ids.Add(message.Id));
            return Task.CompletedTask;
        }, batchSize: 200);

        Assert.Equal(200, delivered);
        Assert.Equal(200, ids.Count);
        Assert.All(store.Messages, message => Assert.NotNull(message.ProcessedAt));
    }

    [Fact]
    public void A_failed_attempt_stays_unavailable_until_backoff_elapses()
    {
        var now = DateTimeOffset.Parse("2026-11-23T00:00:00Z");
        var message = Message();

        message.RecordFailure("downstream unavailable", now, OutboxDispatcher.MaxAttempts);

        Assert.Equal(now.AddSeconds(2), message.AvailableAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.PoisonedAt);
    }

    private static OutboxMessage Message() =>
        OutboxMessage.Create(new TenantId(Guid.NewGuid()), new PrincipalId(Guid.NewGuid()), "ArchiveExported", "devweave.archive.v1", "{}", "corr");

    private sealed class MemoryOutbox : IOutboxStore
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task<IReadOnlyList<OutboxMessage>> ClaimDueAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>(
                Messages.Where(message => message.ProcessedAt is null && message.PoisonedAt is null).Take(batchSize).ToList());

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
