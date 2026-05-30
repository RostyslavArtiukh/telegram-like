namespace TelegramLike.Web.Services.Typing;

/// Bridges cross-service typing events to Blazor Server circuits. The consumer
/// (driven by RabbitMQ via MassTransit) calls Publish; Razor components subscribe
/// on init and unsubscribe on dispose. Each component's callback is invoked on
/// the publishing thread — use InvokeAsync(StateHasChanged) to marshal back.
public interface ITypingPubSub
{
    IDisposable Subscribe(Guid chatId, Func<Guid, Task> onUserTyping);

    Task PublishAsync(Guid chatId, Guid userId);
}
