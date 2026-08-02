namespace TelegramLike.Web.Services.ChatChanged;

/// Unified pubsub for "something in this chat changed and the message list
/// needs to redraw" — MessageRetracted / ReactionAdded / ReactionRemoved.
/// One subscription per ChatView, one callback (reload). New events live —
/// just add another consumer that publishes here.
internal sealed class ChatChangedPubSub
{
    private readonly CircuitTopics<Func<Task>> _byChat = new();

    public IDisposable Subscribe(Guid chatId, Func<Task> onChanged) =>
        _byChat.Subscribe(chatId, onChanged);

    public Task PublishAsync(Guid chatId) =>
        _byChat.PublishAsync(chatId, onChanged => onChanged());
}
