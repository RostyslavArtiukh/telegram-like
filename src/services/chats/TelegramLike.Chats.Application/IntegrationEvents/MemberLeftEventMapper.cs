using TelegramLike.Chats.Application.Common.IntegrationEvents;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Chats.Domain.Common;

namespace TelegramLike.Chats.Application.IntegrationEvents;

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
