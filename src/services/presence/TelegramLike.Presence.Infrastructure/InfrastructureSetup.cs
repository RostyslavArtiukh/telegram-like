using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TelegramLike.Shared.Infrastructure;
using TelegramLike.Presence.Application.Storage;
using TelegramLike.Presence.Application.Queries;
using TelegramLike.Presence.Domain.Repositories;
using TelegramLike.Presence.Infrastructure.Caching;
using TelegramLike.Presence.Infrastructure.Messaging.Consumers;
using TelegramLike.Presence.Infrastructure.Storage;

namespace TelegramLike.Presence.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddPresenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDb(configuration);
        services.AddRedis(configuration);

        services.AddScoped<IUserPresenceRepository, UserPresenceRepository>();
        services.AddScoped<IUserPresenceQueryService, UserPresenceQueryService>();
        services.AddScoped<IChatMembershipReadModel, MongoChatMembershipReadModel>();

        var heartbeatTtl = TimeSpan.FromSeconds(
            int.TryParse(configuration["Presence:HeartbeatTtlSeconds"], out var hb) ? hb : 30);
        services.AddSingleton<IPresenceCache>(sp => new RedisPresenceCache(
            sp.GetRequiredService<IConnectionMultiplexer>(), heartbeatTtl));

        var typingTtl = TimeSpan.FromSeconds(
            int.TryParse(configuration["Presence:TypingTtlSeconds"], out var tt) ? tt : 5);
        services.AddSingleton<ITypingIndicatorService>(sp => new RedisTypingIndicatorService(
            sp.GetRequiredService<IConnectionMultiplexer>(), typingTtl));

        // Presence публікує UserTypingIntegrationEvent + споживає membership events
        // (MemberJoined/Kicked/Left) з Chats для побудови локальної read-моделі —
        // потрібно для StartTyping membership-check без cross-context call.
        services.AddRabbitMqBus(configuration, "presence", bus =>
        {
            bus.AddConsumer<MemberJoinedConsumer>();
            bus.AddConsumer<MemberKickedConsumer>();
            bus.AddConsumer<MemberLeftConsumer>();
            // One-time backfill of pre-existing chats' membership into the read-model.
            bus.AddConsumer<ChatMembershipsSnapshotConsumer>();
        });

        return services;
    }
}
