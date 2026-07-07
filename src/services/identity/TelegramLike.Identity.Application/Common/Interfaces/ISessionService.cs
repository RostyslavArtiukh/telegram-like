namespace TelegramLike.Identity.Application.Common.Interfaces;

public interface ISessionService
{
    Task<string> CreateSessionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetUserIdAsync(string token, CancellationToken cancellationToken = default);
    Task DeleteSessionAsync(string token, CancellationToken cancellationToken = default);
}
