using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Domain.Common;
using TelegramLike.Domain.Messaging.Events;

namespace TelegramLike.Application.Messaging.IntegrationEvents;

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
