namespace TelegramLike.Presence.Application.Queries;

public interface IUserPresenceQueryService
{
    Task<UserPresenceDto?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<UserPresenceDto>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}
