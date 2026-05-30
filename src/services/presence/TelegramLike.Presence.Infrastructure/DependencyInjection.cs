using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;
using TelegramLike.Presence.Application.Abstractions;
using TelegramLike.Presence.Application.Queries;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Infrastructure.Caching;
using TelegramLike.Presence.Infrastructure.Persistence;

namespace TelegramLike.Presence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPresenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDB(configuration);
        services.AddRedis(configuration);

        services.AddScoped<IUserPresenceRepository, UserPresenceRepository>();
        services.AddScoped<IUserPresenceQueryService, UserPresenceQueryService>();

        var heartbeatTtl = TimeSpan.FromSeconds(
            int.TryParse(configuration["Presence:HeartbeatTtlSeconds"], out var hb) ? hb : 30);
        services.AddSingleton<IPresenceCache>(sp => new RedisPresenceCache(
            sp.GetRequiredService<IConnectionMultiplexer>(), heartbeatTtl));

        var typingTtl = TimeSpan.FromSeconds(
            int.TryParse(configuration["Presence:TypingTtlSeconds"], out var tt) ? tt : 5);
        services.AddSingleton<ITypingIndicatorService>(sp => new RedisTypingIndicatorService(
            sp.GetRequiredService<IConnectionMultiplexer>(), typingTtl));

        services.AddIntegrationMessaging(configuration);

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

    private static void AddIntegrationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        // Presence публікує UserTypingIntegrationEvent. Не підписується ні на що.
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        services.AddMassTransit(bus =>
        {
            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });
            });
        });
    }
}
