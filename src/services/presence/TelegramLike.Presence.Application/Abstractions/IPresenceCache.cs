namespace TelegramLike.Presence.Application.Abstractions;

public interface IPresenceCache
{
    Task<bool> IsOnlineAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, bool>> AreOnlineAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    Task TouchAsync(Guid userId, CancellationToken ct = default);

    Task ClearAsync(Guid userId, CancellationToken ct = default);
}
