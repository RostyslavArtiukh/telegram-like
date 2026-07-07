using TelegramLike.Messaging.Domain.Aggregates;

namespace TelegramLike.Messaging.Domain.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Message message, CancellationToken cancellationToken = default);

    Task UpdateAsync(Message message, CancellationToken cancellationToken = default);

    // Atomic $inc so concurrent broadcast reads can't lose an increment via a
    // whole-document ReplaceOne. Guarded by the caller to run once per reader.
    Task IncrementBroadcastReadCountAsync(Guid messageId, CancellationToken cancellationToken = default);
}
