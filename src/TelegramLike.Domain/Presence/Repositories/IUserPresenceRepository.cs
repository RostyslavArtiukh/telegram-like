using TelegramLike.Domain.Presence.Aggregates;

namespace TelegramLike.Domain.Presence.Repositories;

public interface IUserPresenceRepository
{
    Task<UserPresence?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task UpsertAsync(UserPresence presence, CancellationToken ct = default);
}
