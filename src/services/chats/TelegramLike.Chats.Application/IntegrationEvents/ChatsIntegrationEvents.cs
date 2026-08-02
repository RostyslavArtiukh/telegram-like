using TelegramLike.Chats.Domain.Events;
using TelegramLike.Contracts.Chats;
using TelegramLike.Contracts.Common;
using TelegramLike.Shared.Application;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Chats.Application.IntegrationEvents;

/// <summary>
/// Everything Chats publishes, in one place. Each arm is the translation from an internal
/// change event to the public contract; the default arm keeps an event inside the service.
/// </summary>
/// <remarks>
/// The translation is not ceremony: <c>Contracts</c> deliberately has no project references
/// (it ships inside the client SDK), so domain types like <c>MemberRole</c> and
/// <c>ChatType</c> cannot cross — hence the <c>ToString()</c> calls. It is also where the
/// published shape is narrowed: <see cref="MemberRoleChangedEvent"/> carries
/// <c>OldRole</c> and <c>ChangedBy</c> internally, and neither goes on the wire. And because
/// the outbox replays a stored payload long after it was written, a domain rename has to
/// stop here or it breaks in-flight rows and every consumer at once.
/// </remarks>
public static class ChatsIntegrationEvents
{
    public static IReadOnlyList<IIntegrationEvent> Map(IChangeEvent changeEvent) => changeEvent switch
    {
        ChatCreatedEvent e => [new ChatCreatedIntegrationEvent(
            e.EventId, e.OccurredAt, e.ChatId, e.Type.ToString())],

        ChatDeletedEvent e => [new ChatDeletedIntegrationEvent(
            e.EventId, e.OccurredAt, e.ChatId, e.DeletedBy)],

        // Join and kick embed the audience to notify, so their size follows the size of the
        // chat and they are split into parts ([TL-124]). Far rarer than a send, but the same
        // shape — a single ~400 KB frame for a large group.
        MemberJoinedEvent e => FanoutParts.Split(
            e.Recipients,
            e.EventId,
            (id, part, index, count) => new MemberJoinedIntegrationEvent(
                id, e.OccurredAt, e.ChatId, e.UserId, part, e.Role.ToString(), index, count)),

        MemberLeftEvent e => [new MemberLeftIntegrationEvent(
            e.EventId, e.OccurredAt, e.ChatId, e.UserId)],

        MemberKickedEvent e => FanoutParts.Split(
            e.Recipients,
            e.EventId,
            (id, part, index, count) => new MemberKickedIntegrationEvent(
                id, e.OccurredAt, e.ChatId, e.UserId, e.KickedBy, part, index, count)),

        MemberBannedEvent e => [new MemberBannedIntegrationEvent(
            e.EventId, e.OccurredAt, e.ChatId, e.UserId, e.BannedBy, e.Reason)],

        // Only the new role travels: who demoted whom stays inside Chats.
        MemberRoleChangedEvent e => [new MemberRoleChangedIntegrationEvent(
            e.EventId, e.OccurredAt, e.ChatId, e.UserId, e.NewRole.ToString())],

        // Deliberately internal — publishing an event no one consumes only costs outbox
        // rows and bus traffic. Add an arm above the moment a consumer needs it.
        //   ChatRenamedEvent         — no service stores chat names.
        //   OwnershipTransferredEvent — the two MemberRoleChanged events already carry the roles.
        _ => []
    };
}
