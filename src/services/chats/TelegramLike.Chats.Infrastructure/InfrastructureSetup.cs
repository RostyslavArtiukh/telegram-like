using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Application.ServiceDefaults;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Chats.Application.Queries;
using TelegramLike.Chats.Application.IntegrationEvents;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Infrastructure.Storage;
using TelegramLike.Infrastructure.ServiceDefaults;
using TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

namespace TelegramLike.Chats.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddChatsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDb(configuration);
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IChatQueryService, ChatQueryService>();
        services.AddScoped<IChatMembershipBackfillReader, MongoChatMembershipBackfillReader>();

        services.AddOutgoingEvents(configuration);
        services.AddSingleton<IIntegrationEventMapper, MemberJoinedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberKickedEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberLeftEventMapper>();
        services.AddSingleton<IIntegrationEventMapper, MemberRoleChangedEventMapper>();

        services.AddRabbitMqBus(configuration);
        return services;
    }
}
