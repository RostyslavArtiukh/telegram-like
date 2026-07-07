namespace TelegramLike.Presence.Application.Abstractions;

public interface IPresenceCache
{
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, bool>> AreOnlineAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    Task TouchAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ClearAsync(Guid userId, CancellationToken cancellationToken = default);
}
