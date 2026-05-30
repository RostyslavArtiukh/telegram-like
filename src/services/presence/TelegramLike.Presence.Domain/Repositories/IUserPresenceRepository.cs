using TelegramLike.Presence.Domain.Aggregates;

namespace TelegramLike.Presence.Domain.Repositories;

public interface IUserPresenceRepository
{
    Task<UserPresence?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task UpsertAsync(UserPresence presence, CancellationToken ct = default);
}
