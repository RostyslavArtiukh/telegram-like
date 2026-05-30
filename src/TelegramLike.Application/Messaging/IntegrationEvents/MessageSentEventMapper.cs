using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.Common;
using TelegramLike.Domain.Messaging.Events;

namespace TelegramLike.Application.Messaging.IntegrationEvents;

public sealed class MessageSentEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(MessageSentEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
    {
        var e = (MessageSentEvent)domainEvent;
        return new MessageSentIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            MessageId: e.MessageId,
            ChatId: e.ChatId,
            AuthorId: e.AuthorId,
            Recipients: e.Recipients);
    }
}
