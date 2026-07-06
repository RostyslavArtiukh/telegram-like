using MongoDB.Driver;
using TelegramLike.Messaging.Domain.Aggregates;
using TelegramLike.Messaging.Domain.Common;
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
        // Optimistic concurrency: guard the whole-document write on the version the
        // aggregate was loaded at, and bump it. If another writer already advanced the
        // version, MatchedCount is 0 → abort (no clobber, no event dispatched) and let
        // the caller reload+retry. Fixes the reaction/retract lost-update.
        var expectedVersion = message.Version;
        var doc = MessageDocument.FromDomain(message);
        doc.Version = expectedVersion + 1;

        using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            var result = await _messages.ReplaceOneAsync(
                s,
                Builders<MessageDocument>.Filter.And(
                    Builders<MessageDocument>.Filter.Eq(m => m.Id, message.Id),
                    Builders<MessageDocument>.Filter.Eq(m => m.Version, expectedVersion)),
                doc,
                new ReplaceOptions { IsUpsert = false },
                token);

            if (result.MatchedCount == 0)
                throw new ConcurrencyConflictException(
                    $"Message {message.Id} was modified concurrently (expected version {expectedVersion}).");

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
