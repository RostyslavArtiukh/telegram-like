using TelegramLike.Shared.Application;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Shared.Domain;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class MessageRetractedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(MessageRetractedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
    {
        var e = (MessageRetractedEvent)domainEvent;
        return new MessageRetractedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            MessageId: e.MessageId,
            ChatId: e.ChatId,
            RetractedBy: e.RetractedBy);
    }
}
