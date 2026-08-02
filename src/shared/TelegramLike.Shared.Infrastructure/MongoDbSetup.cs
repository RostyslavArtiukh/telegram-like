using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using TelegramLike.Shared.Infrastructure.Storage;

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

        // Attached to the database itself, not to AddMongoIndexes<T>: a service that declares
        // no indexes is exactly the case worth hearing about, and it would register nothing.
        // Runs first among hosted services because AddMongoDb is each setup's first call.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MongoIndexInitializer>());

        return services;
    }
}
