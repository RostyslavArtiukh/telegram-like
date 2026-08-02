namespace TelegramLike.Web.Services.NewMessage;

/// Bridges MessageSentIntegrationEvent (RabbitMQ) to Blazor Server circuits.
/// ChatView subscribes per ChatId and reloads its message list when notified —
/// replaces the previous 3-second polling.
internal sealed class NewMessagePubSub
{
    private readonly CircuitTopics<Func<Guid, Task>> _byChat = new();

    public IDisposable Subscribe(Guid chatId, Func<Guid, Task> onNewMessage) =>
        _byChat.Subscribe(chatId, onNewMessage);

    public Task PublishAsync(Guid chatId, Guid messageId) =>
        _byChat.PublishAsync(chatId, onNewMessage => onNewMessage(messageId));
}
