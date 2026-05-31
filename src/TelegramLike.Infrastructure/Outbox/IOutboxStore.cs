using MongoDB.Driver;

namespace TelegramLike.Infrastructure.Outbox;

internal interface IOutboxStore
{
    Task AddAsync(
        IEnumerable<OutboxMessage> messages,
        IClientSessionHandle session,
        CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken ct = default);

    Task MarkSentAsync(Guid id, CancellationToken ct = default);

    // Atomically increments Retries, stores the error message, and — if the
    // resulting retry count reaches maxRetries — flips the message into the
    // dead-letter state (DeadLetteredAt set), so GetPendingAsync stops returning it.
    Task RecordFailureAsync(
        Guid id,
        string error,
        int maxRetries,
        CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(
        int batchSize,
        CancellationToken ct = default);
}
