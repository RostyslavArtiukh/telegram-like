using TelegramLike.Messaging.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class ReactionAddedEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(ReactionAddedEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
    {
        var e = (ReactionAddedEvent)domainEvent;
        return new ReactionAddedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            MessageId: e.MessageId,
            ChatId: e.ChatId,
            UserId: e.UserId,
            Emoji: e.Emoji.ToString());
    }
}
