namespace TelegramLike.Web.Services.NewMessage;

/// Bridges MessageSentIntegrationEvent (RabbitMQ) to Blazor Server circuits.
/// ChatView subscribes per ChatId and reloads its message list when notified —
/// replaces the previous 3-second polling.
public interface INewMessagePubSub
{
    IDisposable Subscribe(Guid chatId, Func<Guid, Task> onNewMessage);

    Task PublishAsync(Guid chatId, Guid messageId);
}
