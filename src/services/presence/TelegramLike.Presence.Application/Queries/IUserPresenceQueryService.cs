namespace TelegramLike.Presence.Application.Queries;

public interface IUserPresenceQueryService
{
    Task<UserPresenceDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

}
