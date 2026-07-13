using TelegramLike.Application.ServiceDefaults;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Chats.Application.IntegrationEvents;

public sealed class ChatCreatedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(ChatCreatedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
    {
        var e = (ChatCreatedEvent)domainEvent;
        return new ChatCreatedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            Type: e.Type.ToString());
    }
}
