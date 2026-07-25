using TelegramLike.Shared.Application;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Chats.Domain.Events;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Application.IntegrationEvents;

// Promote/demote and ownership transfer all raise MemberRoleChangedEvent (transfer
// raises one for the old owner and one for the new), so mapping this single event
// keeps a downstream role read-model current for every role transition.
public sealed class MemberRoleChangedEventMapper : IIntegrationEventMapper
{
    public Type ChangeEventType => typeof(MemberRoleChangedEvent);

    public IIntegrationEvent Map(IChangeEvent domainEvent)
    {
        var e = (MemberRoleChangedEvent)domainEvent;
        return new MemberRoleChangedIntegrationEvent(
            EventId: e.EventId,
            OccurredAt: e.OccurredAt,
            ChatId: e.ChatId,
            UserId: e.UserId,
            Role: e.NewRole.ToString());
    }
}
