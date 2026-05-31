using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Domain.Identity.Repositories;
using TelegramLike.Infrastructure.Auth;
using TelegramLike.Infrastructure.Caching.Redis;
using TelegramLike.Infrastructure.Outbox;
using TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;

namespace TelegramLike.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus = null)
    {
        services.AddMongoDB(configuration);
        services.AddRedis(configuration);
        services.AddRepositories();
        services.AddServices(configuration);
        services.AddOutbox(configuration);
        services.AddIntegrationMessaging(configuration, configureBus);
        return services;
    }

    private static void AddMongoDB(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"]!;
        var databaseName = configuration["MongoDB:DatabaseName"]!;

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
    }

    private static void AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"]!;
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
    }

    private static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        var ttlDays = int.TryParse(configuration["Auth:SessionTokenTtlDays"], out var days) ? days : 7;
        services.AddSingleton<ISessionService>(sp => new RedisSessionService(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            TimeSpan.FromDays(ttlDays)));
    }

    // Outbox + IIntegrationEventMapper are kept (small, dormant) so future Identity
    // integration events can plug in without re-wiring the monolith. After Phase 6
    // there are no concrete mappers registered, so the publisher simply finds no
    // pending messages and idles.
    private static void AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxPublisherOptions>(opts =>
        {
            if (int.TryParse(configuration["Outbox:PollIntervalSeconds"], out var poll))
                opts.PollIntervalSeconds = poll;
            if (int.TryParse(configuration["Outbox:BatchSize"], out var batch))
                opts.BatchSize = batch;
            if (int.TryParse(configuration["Outbox:MaxRetries"], out var maxRetries))
                opts.MaxRetries = maxRetries;
        });

        services.AddScoped<IOutboxStore, MongoOutboxStore>();
        services.AddScoped<IDomainEventDispatcher, OutboxDomainEventDispatcher>();

        services.AddHostedService<OutboxPublisherHostedService>();
    }

    private static void AddIntegrationMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureBus)
    {
        // Monolith still hosts the Web BFF's MassTransit bus so that pubsub consumers
        // (UserTyping, NewMessage, ChatChanged, Presence) keep working. Publishing
        // is now done by Chats/Messaging services, not by the monolith outbox.
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var vhost = configuration["RabbitMQ:VirtualHost"] ?? "/";

        services.AddMassTransit(bus =>
        {
            configureBus?.Invoke(bus);

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
    }
}
