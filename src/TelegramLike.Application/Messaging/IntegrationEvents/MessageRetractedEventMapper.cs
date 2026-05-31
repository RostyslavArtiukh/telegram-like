using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.Common;
using TelegramLike.Domain.Messaging.Events;

namespace TelegramLike.Application.Messaging.IntegrationEvents;

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
