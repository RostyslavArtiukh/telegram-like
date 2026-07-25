using TelegramLike.Shared.Application;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Shared.Domain;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class ReactionAddedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(ReactionAddedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
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
