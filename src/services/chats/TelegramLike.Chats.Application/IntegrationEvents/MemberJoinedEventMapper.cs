using TelegramLike.Shared.Application;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Application.IntegrationEvents;

public sealed class MemberJoinedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(MemberJoinedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
    {
        var e = (MemberJoinedEvent)domainEvent;
        return new MemberJoinedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            UserId: e.UserId,
            Recipients: e.Recipients,
            Role: e.Role.ToString());
    }
}
