using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TelegramLike.Identity.Application.Security;
using TelegramLike.Identity.Domain.Repositories;
using TelegramLike.Identity.Infrastructure.Auth;
using TelegramLike.Identity.Infrastructure.Caching;
using TelegramLike.Identity.Infrastructure.Storage;
using TelegramLike.Shared.Infrastructure;
using TelegramLike.Shared.Infrastructure.Storage;

namespace TelegramLike.Identity.Infrastructure;

public static class InfrastructureSetup
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMongoDb(configuration);
        services.AddRedis(configuration);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddMongoIndexes<UserIndexes>();

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
}
