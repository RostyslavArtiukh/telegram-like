namespace TelegramLike.Web.Services.ChatChanged;

/// Unified pubsub for "something in this chat changed and the message list
/// needs to redraw" — MessageRetracted / ReactionAdded / ReactionRemoved.
/// One subscription per ChatView, one callback (reload). New events live —
/// just add another consumer that publishes here.
public interface IChatChangedPubSub
{
    IDisposable Subscribe(Guid chatId, Func<Task> onChanged);

    Task PublishAsync(Guid chatId);
}
