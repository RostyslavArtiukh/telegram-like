using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TelegramLike.Messaging.Infrastructure.Storage;

internal sealed class MessageIndexInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<MessageIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        await EnsureIndexesAsync(database, cancellationToken);
        logger.LogInformation("Messaging indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Exposed so integration tests apply the same indexes as production.
    // Unique (MessageId, MemberId) on message_read_receipts is the idempotency backstop
    // for read receipts: two concurrent MarkAsRead for the same reader both see "no
    // receipt" and both upsert-insert, producing duplicate receipts (and, for broadcast,
    // a double-counted read). The unique index makes the second insert a duplicate-key
    // no-op — the project's mandated partial-unique-index rule, previously missing here.
    public static Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        var receipts = database.GetCollection<BsonDocument>("message_read_receipts");
        var receiptIndex = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("MessageId").Ascending("MemberId"),
            new CreateIndexOptions { Name = "uniq_message_member", Unique = true });

        return receipts.Indexes.CreateOneAsync(receiptIndex, cancellationToken: cancellationToken);
    }
}
