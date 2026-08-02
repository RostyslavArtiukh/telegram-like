using MongoDB.Driver;

namespace TelegramLike.Shared.Infrastructure.OutgoingEvents;

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

    /// <summary>
    /// Claims up to <paramref name="batchSize"/> pending rows for this replica and returns them
    /// oldest first. A claimed row is invisible to other replicas until its lease expires, so
    /// concurrent senders get disjoint batches instead of all publishing every pending event.
    /// </summary>
    /// <remarks>
    /// Three round-trips regardless of batch size, where this used to cost one
    /// <c>FindOneAndUpdate</c> per row — fifty sequential round-trips before a single event
    /// could be published, which is most of what capped a replica's throughput ([TL-125]).
    /// <para>
    /// It is still race-free without a transaction: the update re-checks the lease
    /// <i>per document</i>, and Mongo applies each document update atomically. Two replicas
    /// that pick overlapping candidates therefore split them — whoever stamps a row first owns
    /// it, and the loser's update simply doesn't match. The read-back is what tells each
    /// replica which of its candidates it actually won.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<OutgoingEvent>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var claimUntil = now.Add(ClaimLease);
        var claimToken = Guid.NewGuid().ToString("N");

        var claimable = Builders<OutgoingEventDocument>.Filter.And(
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.SentAt, null),
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.DeadLetteredAt, null),
            Builders<OutgoingEventDocument>.Filter.Or(
                Builders<OutgoingEventDocument>.Filter.Eq(d => d.ClaimedUntil, null),
                Builders<OutgoingEventDocument>.Filter.Lt(d => d.ClaimedUntil, now)));

        var candidateIds = await _collection
            .Find(claimable)
            .SortBy(d => d.OccurredAt)
            .Limit(batchSize)
            .Project(d => d.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0) return [];

        await _collection.UpdateManyAsync(
            Builders<OutgoingEventDocument>.Filter.And(
                Builders<OutgoingEventDocument>.Filter.In(d => d.Id, candidateIds),
                claimable),
            Builders<OutgoingEventDocument>.Update
                .Set(d => d.ClaimedUntil, claimUntil)
                .Set(d => d.ClaimToken, claimToken),
            cancellationToken: cancellationToken);

        // Narrowed by id as well as token so this rides the _id index; a token-only filter
        // would scan the collection, which is the very thing the outbox indexes exist to avoid.
        var claimed = await _collection
            .Find(Builders<OutgoingEventDocument>.Filter.And(
                Builders<OutgoingEventDocument>.Filter.In(d => d.Id, candidateIds),
                Builders<OutgoingEventDocument>.Filter.Eq(d => d.ClaimToken, claimToken)))
            .SortBy(d => d.OccurredAt)
            .ToListAsync(cancellationToken);

        return claimed.Select(d => d.ToEvent()).ToList();
    }

    // The claim fields are dropped along the way: a sent row is publish history, and a lease
    // that outlives the thing it was protecting only misleads whoever reads the collection.
    public Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default) =>
        _collection.UpdateOneAsync(
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.Id, id),
            Builders<OutgoingEventDocument>.Update
                .Set(d => d.SentAt, DateTime.UtcNow)
                .Unset(d => d.ClaimedUntil)
                .Unset(d => d.ClaimToken),
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

    /// <summary>
    /// Samples queue depth and head-of-queue age for the outbox gauges. Unlike
    /// <see cref="GetPendingAsync"/> this ignores the claim lease: a row a sender is
    /// mid-publish on is still backlog until it is actually marked sent.
    /// </summary>
    public async Task<OutboxBacklog> GetBacklogAsync(CancellationToken cancellationToken = default)
    {
        var pendingFilter = Builders<OutgoingEventDocument>.Filter.And(
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.SentAt, null),
            Builders<OutgoingEventDocument>.Filter.Eq(d => d.DeadLetteredAt, null));

        var pendingCount = await _collection.CountDocumentsAsync(pendingFilter, cancellationToken: cancellationToken);

        var deadLetteredCount = await _collection.CountDocumentsAsync(
            Builders<OutgoingEventDocument>.Filter.Ne(d => d.DeadLetteredAt, null),
            cancellationToken: cancellationToken);

        var oldestPending = await _collection
            .Find(pendingFilter)
            .SortBy(d => d.OccurredAt)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        // An empty queue reports 0 age rather than "no sample", so the gauge stays a
        // continuous line in Grafana instead of going stale between bursts.
        var oldestAgeSeconds = oldestPending is null
            ? 0
            : Math.Max(0, (DateTime.UtcNow - oldestPending.OccurredAt).TotalSeconds);

        return new OutboxBacklog(pendingCount, deadLetteredCount, oldestAgeSeconds);
    }
}
