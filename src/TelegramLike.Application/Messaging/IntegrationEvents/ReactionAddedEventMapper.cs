using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.Common;
using TelegramLike.Domain.Messaging.Events;

namespace TelegramLike.Application.Messaging.IntegrationEvents;

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
