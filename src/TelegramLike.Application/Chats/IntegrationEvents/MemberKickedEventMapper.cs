using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Domain.Chats.Events;
using TelegramLike.Domain.Common;

namespace TelegramLike.Application.Chats.IntegrationEvents;

public sealed class MemberKickedEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(MemberKickedEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
    {
        var e = (MemberKickedEvent)domainEvent;
        return new MemberKickedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            UserId: e.UserId,
            KickedBy: e.KickedBy,
            Recipients: e.Recipients);
    }
}
