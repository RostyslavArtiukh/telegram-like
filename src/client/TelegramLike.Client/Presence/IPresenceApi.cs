namespace TelegramLike.Client.Presence;

public interface IPresenceApi
{
    Task HeartbeatAsync(Guid userId, CancellationToken cancellationToken = default);

    Task GoOfflineAsync(Guid userId, CancellationToken cancellationToken = default);

    Task StartTypingAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    Task StopTypingAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetTypingUsersAsync(Guid userId, Guid chatId, CancellationToken cancellationToken = default);

    Task<UserPresenceSummary?> GetUserPresenceAsync(Guid actorUserId, Guid targetUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, bool>> GetBatchPresenceAsync(
        Guid actorUserId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

public sealed record UserPresenceSummary(Guid UserId, bool IsOnline, DateTime? LastSeenAt);
