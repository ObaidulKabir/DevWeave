using DevWeave.Application.Artifacts;

namespace DevWeave.Workers.Retention;

/// <summary>
/// Periodic retention sweep that performs the explicit cleanup half of the quarantine disposition
/// (SRS ART-008). Quarantined bytes carry a bounded lifetime, so this worker periodically deletes any
/// that have elapsed, ensuring flagged content can never become permanent source retention. A failed
/// sweep is logged and retried on the next cadence rather than stopping the host.
/// </summary>
public sealed class QuarantineCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuarantineCleanupWorker> _logger;

    public QuarantineCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<QuarantineCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var purge = scope.ServiceProvider.GetRequiredService<PurgeExpiredQuarantine>();
                var purged = await purge.ExecuteAsync(DateTimeOffset.UtcNow, stoppingToken);

                if (purged > 0)
                    _logger.LogInformation("Purged {Count} expired quarantined artifact(s).", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quarantine cleanup sweep failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
