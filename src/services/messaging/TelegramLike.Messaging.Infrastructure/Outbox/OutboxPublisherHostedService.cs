using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelegramLike.Messaging.Infrastructure.Outbox;

internal sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly OutboxPublisherOptions _options = options.Value;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "OutboxPublisher started. Poll interval: {Interval}s, batch size: {Batch}, max retries: {MaxRetries}",
            _options.PollIntervalSeconds,
            _options.BatchSize,
            _options.MaxRetries);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxPublisher loop iteration failed");
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

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pending = await store.GetPendingAsync(_options.BatchSize, cancellationToken);
        if (pending.Count == 0) return;

        foreach (var message in pending)
        {
            try
            {
                var type = Type.GetType(message.EventType)
                           ?? throw new InvalidOperationException(
                               $"Cannot resolve integration event type '{message.EventType}'.");

                var payload = JsonSerializer.Deserialize(message.Payload, type)
                              ?? throw new InvalidOperationException(
                                  $"Failed to deserialize outbox payload for event {message.Id}.");

                await publishEndpoint.Publish(payload, type, cancellationToken);
                await store.MarkSentAsync(message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                var nextAttempt = message.Retries + 1;
                if (nextAttempt >= _options.MaxRetries)
                {
                    logger.LogError(
                        ex,
                        "Outbox message {MessageId} (type {EventType}) dead-lettered after {Attempts} attempts",
                        message.Id,
                        message.EventType,
                        nextAttempt);
                }
                else
                {
                    logger.LogWarning(
                        ex,
                        "Failed to publish outbox message {MessageId} (type {EventType}); attempt {Attempt}/{Max}",
                        message.Id,
                        message.EventType,
                        nextAttempt,
                        _options.MaxRetries);
                }

                await store.RecordFailureAsync(message.Id, ex.Message, _options.MaxRetries, cancellationToken);
            }
        }
    }
}
