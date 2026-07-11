using MongoDB.Driver;

namespace TelegramLike.Infrastructure.ServiceDefaults.OutgoingEvents;

/// <summary>
/// The Mongo-backed queue of integration events waiting to be sent. Rows are inserted
/// inside the caller's transaction and later claimed, published and marked sent by the
/// <see cref="OutgoingEventsSender"/>.
/// </summary>
public sealed class OutgoingEventsStore(IMongoDatabase database)
{
    private readonly IMongoCollection<OutgoingEventDocument> _collection =
        database.GetCollection<OutgoingEventDocument>("outgoing_events");

    public async Task AddAsync(
        IEnumerable<OutgoingEvent> events,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default)
    {
        var docs = events.Select(OutgoingEventDocument.FromEvent).ToList();
        if (docs.Count == 0) return;

        await _collection.InsertManyAsync(session, docs, cancellationToken: cancellationToken);
    }

    // Lease held while a replica publishes a claimed row. Must comfortably exceed a
    // single publish attempt so the lease can't expire mid-publish and let another
    // replica double-send.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<OutgoingEvent>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var claimUntil = now.Add(ClaimLease);

        // Atomically claim up to batchSize unsent, un-dead-lettered rows whose lease is
        // absent or expired. Each FindOneAndUpdate hands a row to exactly one replica,
        // so concurrent senders get disjoint batches instead of all publishing every
        // pending event (the >1-replica duplicate-publish bug).
        var filter = Builders<OutgoingEventDocument>.Filter.And(
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.SentAt, null),
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.DeadLetteredAt, null),
            Builders<OutgoingEventDocument>.Filter.Or(
                Builders<OutgoingEventDocument>.Filter.Eq(d => d.ClaimedUntil, null),
                Builders<OutgoingEventDocument>.Filter.Lt(d => d.ClaimedUntil, now)));

        var claim = Builders<OutgoingEventDocument>.Update.Set(d => d.ClaimedUntil, claimUntil);
        var opts = new FindOneAndUpdateOptions<OutgoingEventDocument>
        {
            Sort = Builders<OutgoingEventDocument>.Sort.Ascending(d => d.OccurredAt),
            ReturnDocument = ReturnDocument.After
        };

        var claimed = new List<OutgoingEventDocument>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            var doc = await _collection.FindOneAndUpdateAsync(filter, claim, opts, cancellationToken);
            if (doc is null) break;
            claimed.Add(doc);
        }

        return claimed.Select(d => d.ToEvent()).ToList();
    }

    public Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _collection.UpdateOneAsync(
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.Id, id),
            Builders<OutgoingEventDocument>.Update.Set(d => d.SentAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

    public async Task RecordFailureAsync(
        Guid id,
        string error,
        int maxRetries,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<OutgoingEventDocument>.Filter.Eq(d => d.Id, id);
        var update = Builders<OutgoingEventDocument>.Update
            .Inc(d => d.Retries, 1)
            .Set(d => d.LastError, error);

        var options = new FindOneAndUpdateOptions<OutgoingEventDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var updated = await _collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
        if (updated is null) return;

        if (updated.Retries >= maxRetries && updated.DeadLetteredAt is null)
        {
            await _collection.UpdateOneAsync(
                filter,
                Builders<OutgoingEventDocument>.Update.Set(d => d.DeadLetteredAt, DateTime.UtcNow),
                cancellationToken: cancellationToken);
        }
    }

    public async Task<IReadOnlyList<OutgoingEvent>> GetDeadLetteredAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var docs = await _collection
            .Find(d => d.DeadLetteredAt != null)
            .SortBy(d => d.DeadLetteredAt)
            .Limit(batchSize)
            .ToListAsync(cancellationToken);

        return docs.Select(d => d.ToEvent()).ToList();
    }
}
