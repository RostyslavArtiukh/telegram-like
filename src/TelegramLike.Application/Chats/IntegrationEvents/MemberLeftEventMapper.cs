using TelegramLike.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Domain.Chats.Events;
using TelegramLike.Domain.Common;

namespace TelegramLike.Application.Chats.IntegrationEvents;

public sealed class MemberLeftEventMapper : IIntegrationEventMapper
{
    public Type DomainEventType => typeof(MemberLeftEvent);

    public IIntegrationEvent Map(IDomainEvent domainEvent)
    {
        var e = (MemberLeftEvent)domainEvent;
        return new MemberLeftIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            UserId: e.UserId);
    }
}
