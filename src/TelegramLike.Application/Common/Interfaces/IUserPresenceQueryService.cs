using TelegramLike.Application.Presence.Queries;

namespace TelegramLike.Application.Common.Interfaces;

public interface IUserPresenceQueryService
{
    Task<UserPresenceDto?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<UserPresenceDto>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
