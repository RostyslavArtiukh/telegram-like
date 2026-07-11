using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TelegramLike.Infrastructure.ServiceDefaults;

/// <summary>
/// Registers the MassTransit bus on RabbitMQ from the standard <c>RabbitMQ:*</c>
/// configuration keys. Services pass their consumers via <paramref name="registerConsumers"/>;
/// every service used to carry its own copy of this block.
/// </summary>
public static class RabbitMqSetup
{
    public static IServiceCollection AddRabbitMqBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? registerConsumers = null)
    {
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var vhost = configuration["RabbitMQ:VirtualHost"] ?? "/";

        services.AddMassTransit(bus =>
        {
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
