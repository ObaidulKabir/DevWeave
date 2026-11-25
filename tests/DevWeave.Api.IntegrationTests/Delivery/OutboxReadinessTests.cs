using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevWeave.Application.Abstractions;
using DevWeave.Application.Delivery;
using DevWeave.Domain.Common;
using DevWeave.Domain.Identity;
using DevWeave.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DevWeave.Api.IntegrationTests.Delivery;

public sealed class OutboxReadinessTests
{
    [Fact]
    public async Task A_due_message_is_delivered_once_and_backoff_hides_it_until_AvailableAt()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        Guid deliveredId;
        Guid delayedId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var tenant = Tenant.Create("Outbox delivery");
            var actor = Principal.Create(tenant.Id, $"actor-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
            db.AddRange(tenant, actor);
            var due = OutboxMessage.Create(tenant.Id, actor.Id, "ArchiveExported", "1", "{}", "due");
            var delayed = OutboxMessage.Create(tenant.Id, actor.Id, "ArchiveExported", "1", "{}", "delayed");
            db.OutboxMessages.AddRange(due, delayed);
            await db.SaveChangesAsync();
            deliveredId = due.Id;
            delayedId = delayed.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dispatcher = new OutboxDispatcher(scope.ServiceProvider.GetRequiredService<IOutboxStore>());
            var seen = new List<Guid>();
            var delivered = await dispatcher.DispatchAsync((message, _) =>
            {
                seen.Add(message.Id);
                if (message.Id == delayedId)
                    throw new InvalidOperationException("downstream unavailable");
                return Task.CompletedTask;
            }, batchSize: 10);

            Assert.Equal(1, delivered);
            Assert.Equal(2, seen.Count);
        }

        DateTimeOffset availableAt;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var done = await db.OutboxMessages.SingleAsync(message => message.Id == deliveredId);
            var waiting = await db.OutboxMessages.SingleAsync(message => message.Id == delayedId);
            Assert.NotNull(done.ProcessedAt);
            Assert.Null(waiting.ProcessedAt);
            Assert.Null(waiting.PoisonedAt);
            Assert.Equal(1, waiting.AttemptCount);
            Assert.True(waiting.AvailableAt > DateTimeOffset.UtcNow);
            availableAt = waiting.AvailableAt;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var early = await store.ClaimDueAsync(availableAt.AddSeconds(-1), 10);
            await store.SaveAsync();
            Assert.DoesNotContain(early, message => message.Id == delayedId);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var due = await store.ClaimDueAsync(availableAt, 10);
            await store.SaveAsync();
            Assert.Contains(due, message => message.Id == delayedId);
        }
    }

    [Fact]
    public async Task Five_failures_poison_the_row_and_a_later_claim_skips_it()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        Guid messageId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var tenant = Tenant.Create("Outbox poison");
            var actor = Principal.Create(tenant.Id, $"poison-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
            var message = OutboxMessage.Create(tenant.Id, actor.Id, "ArchiveExported", "1", "{}", "poison");
            db.AddRange(tenant, actor, message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        var clock = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= OutboxDispatcher.MaxAttempts; attempt++)
        {
            using var scope = factory.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
            var claimed = await store.ClaimDueAsync(clock, 5);
            var message = Assert.Single(claimed);
            Assert.Equal(messageId, message.Id);
            message.RecordFailure("downstream unavailable", clock, OutboxDispatcher.MaxAttempts);
            await store.SaveAsync();
            clock = clock.AddMinutes(1);
        }

        using var verify = factory.Services.CreateScope();
        var dbAfter = verify.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
        var poisoned = await dbAfter.OutboxMessages.SingleAsync(message => message.Id == messageId);
        Assert.NotNull(poisoned.PoisonedAt);
        Assert.Null(poisoned.ProcessedAt);
        Assert.Equal(OutboxDispatcher.MaxAttempts, poisoned.AttemptCount);

        var storeAfter = verify.ServiceProvider.GetRequiredService<IOutboxStore>();
        var later = await storeAfter.ClaimDueAsync(clock, 5);
        await storeAfter.SaveAsync();
        Assert.DoesNotContain(later, message => message.Id == messageId);
    }

    [Fact]
    public async Task One_poisoned_row_leaves_readiness_healthy()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var tenant = Tenant.Create("Outbox one poison");
            var actor = Principal.Create(tenant.Id, $"one-poison-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
            var message = OutboxMessage.Create(tenant.Id, actor.Id, "Probe", "1", "{}", "one-poison");
            var now = DateTimeOffset.UtcNow;
            for (var attempt = 0; attempt < OutboxDispatcher.MaxAttempts; attempt++)
                message.RecordFailure("downstream unavailable", now, OutboxDispatcher.MaxAttempts);
            db.AddRange(tenant, actor, message);
            db.Entry(message).Property(nameof(OutboxMessage.OccurredAt)).CurrentValue = now.AddHours(-1);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ready", body.GetProperty("status").GetString());
        Assert.Equal(0, body.GetProperty("outboxLag").GetInt32());
        Assert.Equal(1, body.GetProperty("poisoned").GetInt32());
    }

    [Fact]
    public async Task Readiness_returns_503_when_outbox_lag_exceeds_one_hundred()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var tenant = Tenant.Create("Outbox lag");
            var actor = Principal.Create(tenant.Id, $"lag-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
            db.AddRange(tenant, actor);
            var past = DateTimeOffset.UtcNow.AddHours(-1);
            for (var i = 0; i < 101; i++)
            {
                var message = OutboxMessage.Create(tenant.Id, actor.Id, "Probe", "1", "{}", $"lag-{i}");
                db.OutboxMessages.Add(message);
                db.Entry(message).Property(nameof(OutboxMessage.OccurredAt)).CurrentValue = past;
            }

            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("outbox_lag", body.GetProperty("reason").GetString());
        Assert.True(body.GetProperty("outboxLag").GetInt32() > 100);
    }

    [Fact]
    public async Task Readiness_returns_503_when_a_migration_is_pending()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"""DELETE FROM "__EFMigrationsHistory" WHERE "MigrationId" = {"20261122195253_ArchiveJobsAndOutboxDispatch"}""");
        }

        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("migrations", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task INT_004_delivery_records_an_audit_without_copying_the_payload()
    {
        using var factory = new IdentityTestFactory();
        factory.PrepareDatabase();
        Guid messageId;
        const string payload = """{"secret":"do-not-copy"}""";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var tenant = Tenant.Create("Outbox audit");
            var actor = Principal.Create(tenant.Id, $"actor-{Guid.NewGuid():N}@example.test", PrincipalType.Human);
            db.AddRange(tenant, actor);
            var message = OutboxMessage.Create(tenant.Id, actor.Id, "ArchiveExported", "1", payload, "audit");
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
            messageId = message.Id;
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dispatcher = new OutboxDispatcher(scope.ServiceProvider.GetRequiredService<IOutboxStore>());
            var consumer = scope.ServiceProvider.GetRequiredService<OutboxDeliveryConsumer>();
            var delivered = await dispatcher.DispatchAsync(consumer.ConsumeAsync);
            Assert.Equal(1, delivered);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DevWeaveDbContext>();
            var message = await db.OutboxMessages.SingleAsync(row => row.Id == messageId);
            Assert.NotNull(message.ProcessedAt);
            var audit = await db.AuditLogs.SingleAsync(row =>
                row.Action == OutboxDeliveryConsumer.Action && row.ResourceId == messageId.ToString("D"));
            Assert.Equal(OutboxDeliveryConsumer.ResourceType, audit.ResourceType);
            Assert.Contains("ArchiveExported", audit.MetadataJson, StringComparison.Ordinal);
            Assert.DoesNotContain("do-not-copy", audit.MetadataJson, StringComparison.Ordinal);
        }
    }
}
