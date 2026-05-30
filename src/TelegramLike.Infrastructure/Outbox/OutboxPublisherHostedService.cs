using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TelegramLike.Infrastructure.Outbox;

internal sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    IPublishEndpoint publishEndpoint,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly OutboxPublisherOptions _options = options.Value;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "OutboxPublisher started. Poll interval: {Interval}s, batch size: {Batch}",
            _options.PollIntervalSeconds,
            _options.BatchSize);

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

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var pending = await store.GetPendingAsync(_options.BatchSize, ct);
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

                await publishEndpoint.Publish(payload, type, ct);
                await store.MarkSentAsync(message.Id, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to publish outbox message {MessageId} (type {EventType})",
                    message.Id,
                    message.EventType);
                await store.IncrementRetryAsync(message.Id, ct);
            }
        }
    }
}
