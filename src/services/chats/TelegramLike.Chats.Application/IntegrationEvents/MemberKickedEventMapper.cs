using TelegramLike.Chats.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Application.IntegrationEvents;

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
