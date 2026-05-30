namespace TelegramLike.Application.Common.Interfaces;

public interface ISessionService
{
    Task<string> CreateSessionAsync(Guid userId, CancellationToken ct = default);
    Task<Guid?> GetUserIdAsync(string token, CancellationToken ct = default);
    Task DeleteSessionAsync(string token, CancellationToken ct = default);
}
