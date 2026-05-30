namespace TelegramLike.Application.Common.Interfaces;

public interface ITypingIndicatorService
{
    Task StartTypingAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    Task StopTypingAsync(Guid chatId, Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetTypingUserIdsAsync(Guid chatId, CancellationToken ct = default);
}
