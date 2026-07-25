using TelegramLike.Shared.Application;
using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Shared.Domain;
using TelegramLike.Messaging.Domain.Events;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

public sealed class ReactionRemovedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(ReactionRemovedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
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
