using TelegramLike.Domain.Chats.Aggregates;

namespace TelegramLike.Domain.Chats.Repositories;

public interface IChatRepository
{
    Task<Chat?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<DirectChat?> FindDirectBetweenAsync(Guid userA, Guid userB, CancellationToken ct = default);

    Task AddAsync(Chat chat, CancellationToken ct = default);

    Task UpdateAsync(Chat chat, CancellationToken ct = default);
}
