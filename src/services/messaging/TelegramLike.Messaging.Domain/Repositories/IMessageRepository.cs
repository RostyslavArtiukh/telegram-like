using TelegramLike.Messaging.Domain.Aggregates;

namespace TelegramLike.Messaging.Domain.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Message message, CancellationToken ct = default);

    Task UpdateAsync(Message message, CancellationToken ct = default);

    // Atomic $inc so concurrent broadcast reads can't lose an increment via a
    // whole-document ReplaceOne. Guarded by the caller to run once per reader.
    Task IncrementBroadcastReadCountAsync(Guid messageId, CancellationToken ct = default);
}
