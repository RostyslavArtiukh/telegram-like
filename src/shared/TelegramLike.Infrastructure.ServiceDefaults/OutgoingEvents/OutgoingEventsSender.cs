using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

/// <summary>
/// Background loop that drains the outgoing-events queue: claims a batch of unsent
/// rows, publishes each to RabbitMQ and marks it sent; failures are retried and
/// eventually dead-lettered.
/// </summary>
public sealed class OutgoingEventsSender(
    IServiceScopeFactory scopeFactory,
    IOptions<OutgoingEventsSenderOptions> options,
    ILogger<OutgoingEventsSender> logger) : BackgroundService
{
    private readonly OutgoingEventsSenderOptions _options = options.Value;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "OutgoingEventsSender started. Poll interval: {Interval}s, batch size: {Batch}, max retries: {MaxRetries}",
            _options.PollIntervalSeconds,
            _options.BatchSize,
            _options.MaxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutgoingEventsSender loop iteration failed");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SendPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<OutgoingEventsStore>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pending = await store.GetPendingAsync(_options.BatchSize, cancellationToken);
        if (pending.Count == 0) return;

        foreach (var outgoingEvent in pending)
        {
            try
            {
                var type = Type.GetType(outgoingEvent.EventType)
                           ?? throw new InvalidOperationException(
                               $"Cannot resolve integration event type '{outgoingEvent.EventType}'.");

                var payload = JsonSerializer.Deserialize(outgoingEvent.Payload, type)
                              ?? throw new InvalidOperationException(
                                  $"Failed to deserialize outgoing event payload for event {outgoingEvent.Id}.");

                await publishEndpoint.Publish(payload, type, cancellationToken);
                await store.MarkSentAsync(outgoingEvent.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                var nextAttempt = outgoingEvent.Retries + 1;
                if (nextAttempt >= _options.MaxRetries)
                {
                    logger.LogError(
                        ex,
                        "Outgoing event {EventId} (type {EventType}) dead-lettered after {Attempts} attempts",
                        outgoingEvent.Id,
                        outgoingEvent.EventType,
                        nextAttempt);
                }
                else
                {
                    logger.LogWarning(
                        ex,
                        "Failed to publish outgoing event {EventId} (type {EventType}); attempt {Attempt}/{Max}",
                        outgoingEvent.Id,
                        outgoingEvent.EventType,
                        nextAttempt,
                        _options.MaxRetries);
                }

                await store.RecordFailureAsync(outgoingEvent.Id, ex.Message, _options.MaxRetries, cancellationToken);
            }
        }
    }
}
