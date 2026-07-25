using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramLike.Shared.Infrastructure;

/// <summary>
/// Registers the MassTransit bus on RabbitMQ from the standard <c>RabbitMQ:*</c>
/// configuration keys. Services pass their consumers via <paramref name="registerConsumers"/>;
/// every service used to carry its own copy of this block.
/// </summary>
public static class RabbitMqSetup
{
    /// <param name="serviceName">
    /// Unique per-service queue-name prefix (e.g. <c>"messaging"</c>). Without it, same-named
    /// consumer classes in different services (each service has its own <c>MemberJoinedConsumer</c>)
    /// map to ONE shared queue name, and RabbitMQ round-robins each event to a single service —
    /// silently starving the others' read-models. The prefix gives every service its own queue,
    /// so each gets its own copy of every event (proper pub/sub fan-out).
    /// </param>
    public static IServiceCollection AddRabbitMqBus(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<IBusRegistrationConfigurator>? registerConsumers = null)
    {
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var vhost = configuration["RabbitMQ:VirtualHost"] ?? "/";

        services.AddMassTransit(bus =>
        {
            bus.SetEndpointNameFormatter(
                new KebabCaseEndpointNameFormatter(prefix: serviceName, includeNamespace: false));

            registerConsumers?.Invoke(bus);

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, vhost, h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });

        return services;
    }
}
