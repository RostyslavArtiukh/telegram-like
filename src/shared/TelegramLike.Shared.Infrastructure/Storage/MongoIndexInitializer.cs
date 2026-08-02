using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace TelegramLike.Shared.Infrastructure.Storage;

/// <summary>
/// Applies every <see cref="IMongoIndexes"/> the service declared, once at startup.
/// Registered by <c>AddMongoDb</c>, so it runs in every Mongo-backed service whether or not
/// that service declared anything — which is the point: a service with no declarations logs
/// a warning instead of quietly having no indexes at all.
/// </summary>
internal sealed class MongoIndexInitializer(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IMongoIndexes> declarations,
    ILogger<MongoIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var all = declarations.ToList();
        if (all.Count == 0)
        {
            logger.LogWarning(
                "No Mongo indexes are declared in this service. Every query it runs is a collection " +
                "scan. Declare an IMongoIndexes and register it with AddMongoIndexes<T>().");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

        foreach (var declaration in all)
        {
            await declaration.EnsureAsync(database, cancellationToken);
            logger.LogInformation("Indexes ensured for {Collection}.", declaration.Collection);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
