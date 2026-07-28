namespace TelegramLike.Messaging.Application.Storage;

public interface IHiddenMessageRepository
{
    // Write-only on purpose: the read side lives in MessageQueryService, which filters
    // hidden_messages inside the same query that loads the messages rather than paying a
    // round-trip per message.
    Task HideAsync(Guid messageId, Guid userId, CancellationToken cancellationToken = default);
}
