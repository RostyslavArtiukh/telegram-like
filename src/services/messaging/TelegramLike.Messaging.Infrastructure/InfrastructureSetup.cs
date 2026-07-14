using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Application.ServiceDefaults;
using TelegramLike.Messaging.Application.Storage;
using TelegramLike.Messaging.Application.IntegrationEvents;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Infrastructure.Messaging.Consumers;
using TelegramLike.Messaging.Infrastructure.Storage;
using TelegramLike.Infrastructure.ServiceDefaults;
using TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

namespace TelegramLike.Messaging.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddMessagingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDb(configuration);
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IMessageQueryService, MessageQueryService>();
        services.AddScoped<IHiddenMessageRepository, HiddenMessageRepository>();
        services.AddScoped<IMessageReadReceiptRepository, MessageReadReceiptRepository>();
        services.AddScoped<IChatMembershipReadModel, MongoChatMembershipReadModel>();
        services.AddScoped<IChatTypeReadModel, MongoChatTypeReadModel>();
        services.AddHostedService<MessageIndexInitializer>();

        services.AddOutgoingEvents(configuration);
        services.AddSingleton<IIntegrationEventMapper, MessageSentEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MessageRetractedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, ReactionAddedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, ReactionRemovedEventMapper>();

        services.AddRabbitMqBus(configuration, "messaging", bus =>
        {
            // Membership events from Chats build the local read model so handlers
            // can run strict IsActiveMember checks without calling Chats back.
            bus.AddConsumer<MemberJoinedConsumer>();
            bus.AddConsumer<MemberKickedConsumer>();
            bus.AddConsumer<MemberLeftConsumer>();
            bus.AddConsumer<MemberRoleChangedConsumer>();
            // Chat type → SendMessage derives isBroadcast server-side ([TL-102]).
            bus.AddConsumer<ChatCreatedConsumer>();
            // One-time backfill of pre-existing chats' membership into the read-model.
            bus.AddConsumer<ChatMembershipsSnapshotConsumer>();
        });

        return services;
    }
}
