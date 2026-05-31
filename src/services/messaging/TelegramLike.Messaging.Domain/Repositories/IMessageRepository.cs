using TelegramLike.Messaging.Domain.Aggregates;

namespace TelegramLike.Messaging.Domain.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Message message, CancellationToken ct = default);

    Task UpdateAsync(Message message, CancellationToken ct = default);
}
