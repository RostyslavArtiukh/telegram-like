using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Keeps the outbox gauges fresh. The gauge callbacks fire synchronously during metric
/// collection, so they can't query Mongo themselves — this samples the backlog on a timer
/// and hands <see cref="OutboxMetrics"/> a snapshot to report.
/// </summary>
public sealed class OutboxBacklogPoller(
    IServiceScopeFactory scopeFactory,
    OutboxMetrics metrics,
    ILogger<OutboxBacklogPoller> logger) : BackgroundService
{
    // Matched to Prometheus' 10s scrape interval: sampling faster only adds Mongo load
    // for readings nobody scrapes, sampling slower leaves the gauge visibly stale.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<OutgoingEventsStore>();
                metrics.UpdateBacklog(await store.GetBacklogAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Keep serving the last good snapshot rather than zeroing the gauges —
                // a reported 0 backlog would look like "all healthy" during an outage.
                logger.LogWarning(ex, "Failed to sample the outgoing-events backlog");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
