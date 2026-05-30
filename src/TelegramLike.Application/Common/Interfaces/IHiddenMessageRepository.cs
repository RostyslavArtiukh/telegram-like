namespace TelegramLike.Application.Common.Interfaces;

public interface IHiddenMessageRepository
{
    Task HideAsync(Guid messageId, Guid userId, CancellationToken ct = default);

    Task<bool> IsHiddenAsync(Guid messageId, Guid userId, CancellationToken ct = default);
}
