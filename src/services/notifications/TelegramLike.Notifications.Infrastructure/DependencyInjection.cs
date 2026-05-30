using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using TelegramLike.Notifications.Application.Queries;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Infrastructure.Messaging.Consumers;
using TelegramLike.Notifications.Infrastructure.Persistence;

namespace TelegramLike.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDB(configuration);
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
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

    private static void AddIntegrationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var username = configuration["RabbitMQ:Username"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        services.AddMassTransit(bus =>
        {
            bus.AddConsumer<MessageSentConsumer>();
            bus.AddConsumer<MemberJoinedConsumer>();
            bus.AddConsumer<MemberKickedConsumer>();

            bus.UsingRabbitMq((ctx, cfg) =>
            {
                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ConfigureEndpoints(ctx);
            });
        });
    }
}
