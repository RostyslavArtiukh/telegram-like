using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TelegramLike.Shared.Application;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Background loop that drains the outgoing-events queue: claims a batch of unsent
/// rows, publishes each to RabbitMQ and marks it sent; failures are retried and
/// eventually dead-lettered.
/// </summary>
public sealed class OutgoingEventsSender(
    IServiceScopeFactory scopeFactory,
    IOptions<OutgoingEventsSenderOptions> options,
    OutboxMetrics metrics,
    ILogger<OutgoingEventsSender> logger) : BackgroundService
{
    private readonly OutgoingEventsSenderOptions _options = options.Value;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "OutgoingEventsSender started. Poll interval: {Interval}s, batch size: {Batch}, " +
            "publish concurrency: {Concurrency}, max retries: {MaxRetries}",
            _options.PollIntervalSeconds,
            _options.BatchSize,
            _options.PublishConcurrency,
            _options.MaxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            var hadWork = false;
            try
            {
                hadWork = await SendPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutgoingEventsSender loop iteration failed");
            }

            // The poll interval is how long to wait before asking an EMPTY queue again — it was
            // never meant to be a per-batch pause. Sleeping after a batch put a floor of one
            // interval under delivery latency and capped a replica at BatchSize per interval no
            // matter how deep the backlog was ([TL-125]). So: drain while there is anything to
            // drain, and only sleep once the queue has run dry.
            if (hadWork) continue;

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

    /// <returns>Whether anything was claimed, i.e. whether the queue may still hold work.</returns>
    private async Task<bool> SendPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<OutgoingEventsStore>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pending = await store.GetPendingAsync(_options.BatchSize, cancellationToken);
        if (pending.Count == 0) return false;

        // Both the store and MassTransit's publish endpoint are safe to use concurrently, and
        // each event's outcome is recorded on its own row, so a batch fans out across
        // PublishConcurrency broker round-trips instead of waiting them out one by one.
        await Parallel.ForEachAsync(
            pending,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, _options.PublishConcurrency),
                CancellationToken = cancellationToken
            },
            (outgoingEvent, token) => PublishAsync(store, publishEndpoint, outgoingEvent, token));

        return true;
    }

    private async ValueTask PublishAsync(
        OutgoingEventsStore store,
        IPublishEndpoint publishEndpoint,
        OutgoingEvent outgoingEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var type = IntegrationEventNames.Resolve(outgoingEvent.EventType)
                       ?? throw new InvalidOperationException(
                           $"Cannot resolve integration event type '{outgoingEvent.EventType}'. " +
                           "Either its [IntegrationEventName] was removed or renamed — a wire " +
                           "name must never change once rows carry it — or this is a legacy " +
                           "CLR-named row whose type no longer exists.");

            var payload = JsonSerializer.Deserialize(outgoingEvent.Payload, type)
                          ?? throw new InvalidOperationException(
                              $"Failed to deserialize outgoing event payload for event {outgoingEvent.Id}.");

            await publishEndpoint.Publish(payload, type, cancellationToken);
            await store.MarkSentAsync(outgoingEvent.Id, cancellationToken);
            metrics.RecordPublished(outgoingEvent.EventType, outgoingEvent.OccurredAt);
        }
        catch (Exception ex)
        {
            metrics.RecordPublishFailure(outgoingEvent.EventType);

            var nextAttempt = outgoingEvent.Retries + 1;
            if (nextAttempt >= _options.MaxRetries)
            {
                metrics.RecordDeadLettered(outgoingEvent.EventType);
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
