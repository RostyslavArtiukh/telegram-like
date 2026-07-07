using MongoDB.Driver;

namespace TelegramLike.Chats.Infrastructure.Outbox;

internal interface IOutboxStore
{
    Task AddAsync(
        IEnumerable<OutboxMessage> messages,
        IClientSessionHandle session,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task MarkSentAsync(Guid id, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        Guid id,
        string error,
        int maxRetries,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetDeadLetteredAsync(
        int batchSize,
        CancellationToken cancellationToken = default);
}
