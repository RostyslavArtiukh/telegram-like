using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using StackExchange.Redis;
using TelegramLike.Identity.Application.Common.Interfaces;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Infrastructure.Auth;
using TelegramLike.Identity.Infrastructure.Caching;
using TelegramLike.Identity.Infrastructure.Persistence;

namespace TelegramLike.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDB(configuration);
        services.AddRedis(configuration);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddHostedService<UserIndexInitializer>();

        var ttlDays = int.TryParse(configuration["Auth:SessionTokenTtlDays"], out var days) ? days : 7;
        services.AddSingleton<ISessionService>(sp => new RedisSessionService(
            sp.GetRequiredService<IConnectionMultiplexer>(), TimeSpan.FromDays(ttlDays)));

        // Identity is the IdP — it signs the access tokens downstream services trust.
        var secret = configuration["ServiceAuth:JwtSecret"]
            ?? throw new InvalidOperationException("ServiceAuth:JwtSecret is not configured.");
        var issuer = configuration["ServiceAuth:Issuer"]
            ?? throw new InvalidOperationException("ServiceAuth:Issuer is not configured.");
        var audience = configuration["ServiceAuth:Audience"]
            ?? throw new InvalidOperationException("ServiceAuth:Audience is not configured.");
        var lifetime = int.TryParse(configuration["ServiceAuth:TokenLifetimeSeconds"], out var ttl) ? ttl : 300;
        services.AddSingleton<IAccessTokenIssuer>(_ =>
            new AccessTokenIssuer(secret, issuer, audience, lifetime));

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
}
