using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

/// <summary>
/// Wires up the whole outgoing-events queue (store + writer + background sender)
/// for a service. The service itself only adds its <c>IIntegrationEventMapper</c>s.
/// </summary>
public static class OutgoingEventsSetup
{
    public static IServiceCollection AddOutgoingEvents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OutgoingEventsSenderOptions>(opts =>
        {
            if (int.TryParse(configuration["OutgoingEvents:PollIntervalSeconds"], out var poll))
                opts.PollIntervalSeconds = poll;
            if (int.TryParse(configuration["OutgoingEvents:BatchSize"], out var batch))
                opts.BatchSize = batch;
            if (int.TryParse(configuration["OutgoingEvents:MaxRetries"], out var maxRetries))
                opts.MaxRetries = maxRetries;
        });

        services.AddScoped<OutgoingEventsStore>();
        services.AddScoped<IOutgoingEventsWriter, OutgoingEventsWriter>();
        services.AddHostedService<OutgoingEventsSender>();

        // Metrics are part of the queue, not an opt-in: a silently stalled outbox is the
        // failure mode this pattern trades away for atomicity, so every service that
        // takes the queue also takes its instrumentation.
        services.AddSingleton<OutboxMetrics>();
        services.AddHostedService<OutboxBacklogPoller>();
        services.AddHostedService<OutgoingEventsIndexInitializer>();

        return services;
    }
}
