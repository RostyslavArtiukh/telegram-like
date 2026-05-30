using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using StackExchange.Redis;
using TelegramLike.Application.Chats.IntegrationEvents;
using TelegramLike.Application.Common.Interfaces;
using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Application.Messaging.IntegrationEvents;
using TelegramLike.Domain.Chats.Repositories;
using TelegramLike.Domain.Identity.Repositories;
using TelegramLike.Domain.Messaging.Repositories;
using TelegramLike.Infrastructure.Auth;
using TelegramLike.Infrastructure.Caching.Redis;
using TelegramLike.Infrastructure.Outbox;
using TelegramLike.Infrastructure.Persistence.MongoDB.Repositories;

namespace TelegramLike.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDB(configuration);
        services.AddRedis(configuration);
        services.AddRepositories();
        services.AddServices(configuration);
        services.AddOutbox(configuration);
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

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatQueryService, ChatQueryService>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageQueryService, MessageQueryService>();
        services.AddScoped<IHiddenMessageRepository, HiddenMessageRepository>();
        services.AddScoped<IMessageReadReceiptRepository, MessageReadReceiptRepository>();
    }

    private static void AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        var ttlDays = int.TryParse(configuration["Auth:SessionTokenTtlDays"], out var days) ? days : 7;
        services.AddSingleton<ISessionService>(sp => new RedisSessionService(
            sp.GetRequiredService<IConnectionMultiplexer>(),
            TimeSpan.FromDays(ttlDays)));
    }

    private static void AddOutbox(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxPublisherOptions>(opts =>
        {
            if (int.TryParse(configuration["Outbox:PollIntervalSeconds"], out var poll))
                opts.PollIntervalSeconds = poll;
            if (int.TryParse(configuration["Outbox:BatchSize"], out var batch))
                opts.BatchSize = batch;
        });

        services.AddSingleton<IIntegrationEventMapper, MessageSentEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberJoinedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberKickedEventMapper>();

        services.AddScoped<IOutboxStore, MongoOutboxStore>();
        services.AddScoped<IDomainEventDispatcher, OutboxDomainEventDispatcher>();

        services.AddHostedService<OutboxPublisherHostedService>();
    }

    private static void AddIntegrationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        // Monolith публікує integration events (через outbox), але НЕ підписується —
        // consumers переїхали в окремі сервіси (Notifications etc.).
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
