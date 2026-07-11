namespace TelegramLike.Messaging.Application.Storage;

public interface IHiddenMessageRepository
{
    Task HideAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> IsHiddenAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
