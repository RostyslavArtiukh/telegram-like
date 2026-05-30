using MongoDB.Driver;

namespace TelegramLike.Infrastructure.Outbox;

internal sealed class MongoOutboxStore(IMongoDatabase database) : IOutboxStore
{
    private readonly IMongoCollection<OutboxDocument> _outbox =
        database.GetCollection<OutboxDocument>("outbox");

    public async Task AddAsync(
        IEnumerable<OutboxMessage> messages,
        IClientSessionHandle session,
        CancellationToken ct = default)
    {
        var docs = messages.Select(OutboxDocument.FromMessage).ToList();
        if (docs.Count == 0) return;

        await _outbox.InsertManyAsync(session, docs, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        var docs = await _outbox
            .Find(d => d.SentAt == null)
            .SortBy(d => d.OccurredAt)
            .Limit(batchSize)
            .ToListAsync(ct);

        return docs.Select(d => d.ToMessage()).ToList();
    }

    public Task MarkSentAsync(Guid id, CancellationToken ct = default) =>
        _outbox.UpdateOneAsync(
            Builders<OutboxDocument>.Filter.Eq(d => d.Id, id),
            Builders<OutboxDocument>.Update.Set(d => d.SentAt, DateTime.UtcNow),
            cancellationToken: ct);

    public Task IncrementRetryAsync(Guid id, CancellationToken ct = default) =>
        _outbox.UpdateOneAsync(
            Builders<OutboxDocument>.Filter.Eq(d => d.Id, id),
            Builders<OutboxDocument>.Update.Inc(d => d.Retries, 1),
            cancellationToken: ct);
}
