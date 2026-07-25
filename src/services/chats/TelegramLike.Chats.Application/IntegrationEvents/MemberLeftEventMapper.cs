using TelegramLike.Shared.Application;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Application.IntegrationEvents;

public sealed class MemberLeftEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(MemberLeftEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
    {
        var e = (MemberLeftEvent)domainEvent;
        return new MemberLeftIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            UserId: e.UserId);
    }
}
