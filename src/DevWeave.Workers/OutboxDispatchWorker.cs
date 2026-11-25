using DevWeave.Application.Delivery;

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
                var consumer = scope.ServiceProvider.GetRequiredService<OutboxDeliveryConsumer>();
                var delivered = await dispatcher.DispatchAsync(consumer.ConsumeAsync, stoppingToken);
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
}
