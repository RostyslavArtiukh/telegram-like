namespace TelegramLike.Web.Services.PresenceApi;

public interface IPresenceApi
{
    Task HeartbeatAsync(Guid userId, CancellationToken ct = default);

    Task GoOfflineAsync(Guid userId, CancellationToken ct = default);
}
