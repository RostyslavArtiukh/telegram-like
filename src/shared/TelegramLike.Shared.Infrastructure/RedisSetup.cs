using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace TelegramLike.Shared.Infrastructure;

/// <summary>
/// Registers the shared Redis connection from the standard
/// <c>Redis:ConnectionString</c> configuration key.
/// </summary>
public static class RedisSetup
{
    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"]!;
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));

        return services;
    }
}
