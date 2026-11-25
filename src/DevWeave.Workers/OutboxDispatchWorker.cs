using DevWeave.Application.Delivery;
using DevWeave.Domain.Common;

namespace DevWeave.Workers;

/// <summary>
/// Delivers the transactional outbox. Each cycle claims a batch, invokes the consumer, and
/// commits processed, delayed, or poisoned rows.
/// </summary>
public sealed class OutboxDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxDispatchWorker> _logger;

    public OutboxDispatchWorker(IServiceScopeFactory scopes, ILogger<OutboxDispatchWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher>();
                var delivered = await dispatcher.DispatchAsync(LogDeliveryAsync, stoppingToken);
                if (delivered > 0)
                    _logger.LogInformation("Delivered {Count} outbox messages.", delivered);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private Task LogDeliveryAsync(OutboxMessage message, CancellationToken ct)
    {
        _logger.LogInformation(
            "Outbox {EventType} {MessageId} for tenant {TenantId}.",
            message.EventType,
            message.Id,
            message.TenantId.Value);
        return Task.CompletedTask;
    }
}
