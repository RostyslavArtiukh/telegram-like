using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

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

        return services;
    }
}
