namespace TelegramLike.Web.Services.PresenceApi;

public interface IPresenceApi
{
    Task HeartbeatAsync(Guid userId, CancellationToken ct = default);

    Task GoOfflineAsync(Guid userId, CancellationToken ct = default);

    Task StartTypingAsync(Guid userId, Guid chatId, CancellationToken ct = default);

    Task StopTypingAsync(Guid userId, Guid chatId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetTypingUsersAsync(Guid userId, Guid chatId, CancellationToken ct = default);

    Task<UserPresenceSummary?> GetUserPresenceAsync(Guid actorUserId, Guid targetUserId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, bool>> GetBatchPresenceAsync(
        Guid actorUserId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);
}

public sealed record UserPresenceSummary(Guid UserId, bool IsOnline, DateTime? LastSeenAt);
