using TelegramLike.Domain.Messaging.Aggregates;

namespace TelegramLike.Domain.Messaging.Repositories;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Message message, CancellationToken ct = default);

    Task UpdateAsync(Message message, CancellationToken ct = default);
}
