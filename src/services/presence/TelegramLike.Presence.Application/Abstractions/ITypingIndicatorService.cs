namespace TelegramLike.Presence.Application.Abstractions;

public interface ITypingIndicatorService
{
    Task StartTypingAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    Task StopTypingAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetTypingUserIdsAsync(Guid chatId, CancellationToken cancellationToken = default);
}
