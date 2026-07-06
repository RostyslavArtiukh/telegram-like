using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TelegramLike.Messaging.Application.Common.Interfaces;
using TelegramLike.Messaging.Application.Common.IntegrationEvents;
using TelegramLike.Messaging.Application.IntegrationEvents;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Infrastructure.Messaging.Consumers;
using TelegramLike.Messaging.Infrastructure.Outbox;
using TelegramLike.Messaging.Infrastructure.Persistence;

namespace TelegramLike.Messaging.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMessagingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDB(configuration);
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageQueryService, MessageQueryService>();
        services.AddScoped<IHiddenMessageRepository, HiddenMessageRepository>();
        services.AddScoped<IMessageReadReceiptRepository, MessageReadReceiptRepository>();
        services.AddScoped<IChatMembershipReadModel, MongoChatMembershipReadModel>();
        services.AddHostedService<MessageIndexInitializer>();
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

        services.AddSingleton<IIntegrationEventMapper, MessageSentEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MessageRetractedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, ReactionAddedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, ReactionRemovedEventMapper>();

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
            // Membership events from Chats build the local read model so handlers
            // can run strict IsActiveMember checks without calling Chats back.
            bus.AddConsumer<MemberJoinedConsumer>();
            bus.AddConsumer<MemberKickedConsumer>();
            bus.AddConsumer<MemberLeftConsumer>();

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
