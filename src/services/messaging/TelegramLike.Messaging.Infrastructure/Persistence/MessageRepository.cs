using MongoDB.Driver;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Repositories;
using TelegramLike.Messaging.Infrastructure.Outbox;

namespace TelegramLike.Messaging.Infrastructure.Persistence;

internal sealed class MessageRepository(
    IMongoClient mongoClient,
    IMongoDatabase database,
    IDomainEventDispatcher dispatcher) : IMessageRepository
{
    private readonly IMongoCollection<MessageDocument> _messages =
        database.GetCollection<MessageDocument>("messages");

    public async Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _messages.Find(m => m.Id == id).FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task AddAsync(Message message, CancellationToken ct = default)
    {
        try
        {
            using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
            await session.WithTransactionAsync(async (s, token) =>
            {
                await _messages.InsertOneAsync(s, MessageDocument.FromDomain(message), cancellationToken: token);
                await dispatcher.DispatchAsync(message.DomainEvents, s, token);
                return true;
            }, cancellationToken: ct);
        }
        catch (Exception ex) when (IsDuplicateKey(ex))
        {
            // Idempotent retry: a message already exists with this id (the client
            // reused it on a retry). The transaction aborted, so nothing was
            // re-inserted and no MessageSent was re-queued to the outbox. Treat as
            // success — same id, no duplicate message, no double notification.
        }

        message.ClearDomainEvents();
    }

    // A duplicate _id surfaces differently depending on where Mongo detects it
    // (write vs. command vs. bulk), so check all three for error code 11000.
    private static bool IsDuplicateKey(Exception ex) => ex switch
    {
        MongoWriteException we => we.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoCommandException ce => ce.Code == 11000,
        MongoBulkWriteException be => be.WriteErrors.Any(e => e.Category == ServerErrorCategory.DuplicateKey),
        _ => false
    };

    public async Task UpdateAsync(Message message, CancellationToken ct = default)
    {
        using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            await _messages.ReplaceOneAsync(
                s,
                Builders<MessageDocument>.Filter.Eq(m => m.Id, message.Id),
                MessageDocument.FromDomain(message),
                new ReplaceOptions { IsUpsert = false },
                token);

            await dispatcher.DispatchAsync(message.DomainEvents, s, token);
            return true;
        }, cancellationToken: ct);

        message.ClearDomainEvents();
    }

    public Task IncrementBroadcastReadCountAsync(Guid messageId, CancellationToken ct = default)
        => _messages.UpdateOneAsync(
            Builders<MessageDocument>.Filter.Eq(m => m.Id, messageId),
            Builders<MessageDocument>.Update.Inc(m => m.BroadcastReadCount, 1),
            cancellationToken: ct);
}
