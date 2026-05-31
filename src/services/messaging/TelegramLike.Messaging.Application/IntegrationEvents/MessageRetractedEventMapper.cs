using TelegramLike.Messaging.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class MessageRetractedEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(MessageRetractedEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
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
