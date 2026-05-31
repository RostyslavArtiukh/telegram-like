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
        using var session = await mongoClient.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            await _messages.InsertOneAsync(s, MessageDocument.FromDomain(message), cancellationToken: token);
            await dispatcher.DispatchAsync(message.DomainEvents, s, token);
            return true;
        }, cancellationToken: ct);

        message.ClearDomainEvents();
    }

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
}
