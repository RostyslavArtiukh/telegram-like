using MongoDB.Driver;

namespace TelegramLike.Messaging.Infrastructure.Outbox;

internal sealed class MongoOutboxStore(IMongoDatabase database) : IOutboxStore
{
    private readonly IMongoCollection<OutboxDocument> _outbox =
        database.GetCollection<OutboxDocument>("outbox");

    public async Task AddAsync(
        IEnumerable<OutboxMessage> messages,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default)
    {
        var docs = messages.Select(OutboxDocument.FromMessage).ToList();
        if (docs.Count == 0) return;

        await _outbox.InsertManyAsync(session, docs, cancellationToken: cancellationToken);
    }

    // Lease held while a replica publishes a claimed row. Must comfortably exceed a
    // single publish attempt so the lease can't expire mid-publish and let another
    // replica double-send.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var claimUntil = now.Add(ClaimLease);

        // Atomically claim up to batchSize unsent, un-dead-lettered rows whose lease is
        // absent or expired. Each FindOneAndUpdate hands a row to exactly one replica,
        // so concurrent publishers get disjoint batches instead of all publishing every
        // pending event (the >1-replica duplicate-publish bug).
        var filter = Builders<OutboxDocument>.Filter.And(
            Builders<OutboxDocument>.Filter.Eq(d => d.SentAt, null),
            Builders<OutboxDocument>.Filter.Eq(d => d.DeadLetteredAt, null),
            Builders<OutboxDocument>.Filter.Or(
                Builders<OutboxDocument>.Filter.Eq(d => d.ClaimedUntil, null),
                Builders<OutboxDocument>.Filter.Lt(d => d.ClaimedUntil, now)));

        var claim = Builders<OutboxDocument>.Update.Set(d => d.ClaimedUntil, claimUntil);
        var opts = new FindOneAndUpdateOptions<OutboxDocument>
        {
            Sort = Builders<OutboxDocument>.Sort.Ascending(d => d.OccurredAt),
            ReturnDocument = ReturnDocument.After
        };

        var claimed = new List<OutboxDocument>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            var doc = await _outbox.FindOneAndUpdateAsync(filter, claim, opts, cancellationToken);
            if (doc is null) break;
            claimed.Add(doc);
        }

        return claimed.Select(d => d.ToMessage()).ToList();
    }

    public Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _outbox.UpdateOneAsync(
            Builders<OutboxDocument>.Filter.Eq(d => d.Id, id),
            Builders<OutboxDocument>.Update.Set(d => d.SentAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

    public async Task RecordFailureAsync(
        Guid id,
        string error,
        int maxRetries,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OutboxDocument>.Filter.Eq(d => d.Id, id);
        var update = Builders<OutboxDocument>.Update
            .Inc(d => d.Retries, 1)
            .Set(d => d.LastError, error);

        var options = new FindOneAndUpdateOptions<OutboxDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await _outbox.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        if (updated is null) return;

        if (updated.Retries >= maxRetries && updated.DeadLetteredAt is null)
        {
            await _outbox.UpdateOneAsync(
                filter,
                Builders<OutboxDocument>.Update.Set(d => d.DeadLetteredAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var docs = await _outbox
            .Find(d => d.DeadLetteredAt != null)
            .SortBy(d => d.DeadLetteredAt)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToMessage()).ToList();
    }
}
