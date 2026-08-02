using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TelegramLike.Shared.Application;
using TelegramLike.Chats.Application.Backfill;
using TelegramLike.Chats.Application.Queries;
using TelegramLike.Chats.Application.IntegrationEvents;
using TelegramLike.Chats.Domain.Repositories;
using TelegramLike.Chats.Infrastructure.Storage;
using TelegramLike.Shared.Infrastructure;
using TelegramLike.Shared.Infrastructure.Storage;
using TelegramLike.Shared.Infrastructure.OutgoingEvents;

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
        services.AddMongoIndexes<ChatIndexes>();

        services.AddOutgoingEvents(configuration, ChatsIntegrationEvents.Map);

        services.AddRabbitMqBus(configuration, "chats");
        return services;
    }
}
