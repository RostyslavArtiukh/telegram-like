using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TelegramLike.Chats.Application.Common.Interfaces;
using TelegramLike.Chats.Application.Common.IntegrationEvents;
using TelegramLike.Chats.Application.IntegrationEvents;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Infrastructure.Outbox;
using TelegramLike.Chats.Infrastructure.Persistence;

namespace TelegramLike.Chats.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDB(configuration);
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatQueryService, ChatQueryService>();
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

        services.AddSingleton<IIntegrationEventMapper, MemberJoinedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberKickedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberLeftEventMapper>();

        services.AddScoped<IOutboxStore, MongoOutboxStore>();
        services.AddScoped<IDomainEventDispatcher, OutboxDomainEventDispatcher>();

        services.AddHostedService<OutboxPublisherHostedService>();
    }

    private static void AddIntegrationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";
        var vhost = configuration["RabbitMQ:VirtualHost"] ?? "/";

        services.AddMassTransit(bus =>
        {
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
