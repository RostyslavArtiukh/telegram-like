namespace TelegramLike.Web.Services.Typing;

/// Bridges cross-service typing events to Blazor Server circuits. The consumer
/// (driven by RabbitMQ via MassTransit) calls Publish; Razor components subscribe
/// on init and unsubscribe on dispose. Each component's callback is invoked on
/// the publishing thread — use InvokeAsync(StateHasChanged) to marshal back.
internal sealed class TypingPubSub
{
    private readonly CircuitTopics<Func<Guid, Task>> _byChat = new();

    public IDisposable Subscribe(Guid chatId, Func<Guid, Task> onUserTyping) =>
        _byChat.Subscribe(chatId, onUserTyping);

    public Task PublishAsync(Guid chatId, Guid userId) =>
        _byChat.PublishAsync(chatId, onUserTyping => onUserTyping(userId));
}
