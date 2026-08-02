using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Shared.Application;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Wires up the whole outgoing-events queue (store + writer + background sender) for a
/// service. The service supplies one <see cref="IntegrationEventMap"/> saying which of its
/// change events go on the wire and in what shape.
/// </summary>
public static class OutgoingEventsSetup
{
    public static IServiceCollection AddOutgoingEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        IntegrationEventMap map)
    {
        services.Configure<OutgoingEventsSenderOptions>(opts =>
        {
            if (int.TryParse(configuration["OutgoingEvents:PollIntervalSeconds"], out var poll))
                opts.PollIntervalSeconds = poll;
            if (int.TryParse(configuration["OutgoingEvents:BatchSize"], out var batch))
                opts.BatchSize = batch;
            if (int.TryParse(configuration["OutgoingEvents:MaxRetries"], out var maxRetries))
                opts.MaxRetries = maxRetries;
            if (int.TryParse(configuration["OutgoingEvents:SentRetentionDays"], out var retentionDays))
                opts.SentRetentionDays = retentionDays;
        });

        services.AddScoped<OutgoingEventsStore>();

        // The map is a constructor argument, not a service. Only the writer ever needs it, so
        // registering it in the container would just publish a resolvable type nothing asks
        // for — and make "was it registered?" a runtime question again. Closing over the
        // parameter makes it impossible to wire up the queue without one.
        services.AddScoped<IOutgoingEventsWriter>(sp =>
            new OutgoingEventsWriter(map, sp.GetRequiredService<OutgoingEventsStore>()));

        services.AddHostedService<OutgoingEventsSender>();

        // Metrics are part of the queue, not an opt-in: a silently stalled outbox is the
        // failure mode this pattern trades away for atomicity, so every service that
        // takes the queue also takes its instrumentation.
        services.AddSingleton<OutboxMetrics>();
        services.AddHostedService<OutboxBacklogPoller>();
        services.AddMongoIndexes<OutgoingEventsIndexes>();

        return services;
    }
}
