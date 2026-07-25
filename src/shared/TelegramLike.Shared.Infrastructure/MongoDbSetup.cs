using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace TelegramLike.Shared.Infrastructure;

/// <summary>
/// Registers the service's MongoDB client and database from the standard
/// <c>MongoDB:ConnectionString</c> / <c>MongoDB:DatabaseName</c> configuration keys.
/// Every service used to carry its own copy of this block.
/// </summary>
public static class MongoDbSetup
{
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"]!;
        var databaseName = configuration["MongoDB:DatabaseName"]!;

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddScoped<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));

        return services;
    }
}
