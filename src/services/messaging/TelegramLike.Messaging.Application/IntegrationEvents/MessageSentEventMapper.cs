using TelegramLike.Application.ServiceDefaults;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.ServiceDefaults;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class MessageSentEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(MessageSentEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
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
