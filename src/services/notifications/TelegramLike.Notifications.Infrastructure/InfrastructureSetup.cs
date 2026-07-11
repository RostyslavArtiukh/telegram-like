using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Infrastructure.ServiceDefaults;
using TelegramLike.Notifications.Application.Queries;
using TelegramLike.Notifications.Domain.Repositories;
using TelegramLike.Notifications.Infrastructure.Messaging.Consumers;
using TelegramLike.Notifications.Infrastructure.Storage;

namespace TelegramLike.Notifications.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDb(configuration);
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationQueryService, NotificationQueryService>();
        services.AddHostedService<NotificationIndexInitializer>();

        services.AddRabbitMqBus(configuration, bus =>
        {
            bus.AddConsumer<MessageSentConsumer>();
            bus.AddConsumer<MemberJoinedConsumer>();
            bus.AddConsumer<MemberKickedConsumer>();
        });

        return services;
    }
}
