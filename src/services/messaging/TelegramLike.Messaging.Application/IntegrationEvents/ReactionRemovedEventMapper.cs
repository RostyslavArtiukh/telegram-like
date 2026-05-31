using TelegramLike.Messaging.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Messaging.Domain.Common;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class ReactionRemovedEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(ReactionRemovedEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
    {
        var e = (ReactionRemovedEvent)domainEvent;
        return new ReactionRemovedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            MessageId: e.MessageId,
            ChatId: e.ChatId,
            UserId: e.UserId,
            Emoji: e.Emoji.ToString());
    }
}
