using MongoDB.Driver;

namespace TelegramLike.Messaging.Infrastructure.Outbox;

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

    Task RecordFailureAsync(
        Guid id,
        string error,
        int maxRetries,
        CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(
        int batchSize,
        CancellationToken ct = default);
}
