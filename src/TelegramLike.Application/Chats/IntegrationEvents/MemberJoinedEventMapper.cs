using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Domain.Chats.Events;
using TelegramLike.Domain.Common;

namespace TelegramLike.Application.Chats.IntegrationEvents;

public sealed class MemberJoinedEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(MemberJoinedEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
    {
        var e = (MemberJoinedEvent)domainEvent;
        return new MemberJoinedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            UserId: e.UserId,
            Recipients: e.Recipients);
    }
}
