using TelegramLike.Chats.Domain.Aggregates;

namespace TelegramLike.Chats.Domain.Repositories;

public interface IChatRepository
{
    Task<Chat?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DirectChat?> FindDirectBetweenAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);

    Task AddAsync(Chat chat, CancellationToken cancellationToken = default);

    Task UpdateAsync(Chat chat, CancellationToken cancellationToken = default);
}
