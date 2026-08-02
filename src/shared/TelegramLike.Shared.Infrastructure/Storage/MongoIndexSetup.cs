using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace TelegramLike.Shared.Infrastructure.Storage;

public static class MongoIndexSetup
{
    /// <summary>
    /// Declares one collection's indexes. Registered as a singleton, so an implementation may
    /// inject singletons (a logger, options) but never a scoped service — the database arrives
    /// as a method argument precisely so declarations stay scope-free.
    /// </summary>
    public static IServiceCollection AddMongoIndexes<TIndexes>(this IServiceCollection services)
        where TIndexes : class, IMongoIndexes
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMongoIndexes, TIndexes>());
        return services;
    }
}
