using TelegramLike.Application.ServiceDefaults;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Domain.ServiceDefaults;

namespace TelegramLike.Chats.Application.IntegrationEvents;

public sealed class MemberKickedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(MemberKickedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
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
