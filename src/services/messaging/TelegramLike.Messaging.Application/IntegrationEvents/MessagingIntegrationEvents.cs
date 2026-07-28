using TelegramLike.Contracts.Common;
using TelegramLike.Contracts.Messaging;
using TelegramLike.Messaging.Domain.Events;
using TelegramLike.Shared.Domain;

namespace TelegramLike.Messaging.Application.IntegrationEvents;

/// <summary>
/// Everything Messaging publishes, in one place. Each arm is the translation from an internal
/// change event to the public contract; the default arm keeps an event inside the service.
/// See <c>ChatsIntegrationEvents</c> for why the translation itself is load-bearing —
/// the <c>Emoji</c> enum here is exactly the kind of domain type Contracts must not see.
/// </summary>
public static class MessagingIntegrationEvents
{
    public static IIntegrationEvent? Map(IChangeEvent changeEvent) => changeEvent switch
    {
        MessageSentEvent e => new MessageSentIntegrationEvent(
            e.EventId, e.OccurredAt, e.MessageId, e.ChatId, e.AuthorId, e.Recipients),

        MessageRetractedEvent e => new MessageRetractedIntegrationEvent(
            e.EventId, e.OccurredAt, e.MessageId, e.ChatId, e.RetractedBy),

        ReactionAddedEvent e => new ReactionAddedIntegrationEvent(
            e.EventId, e.OccurredAt, e.MessageId, e.ChatId, e.UserId, e.Emoji.ToString()),

        ReactionRemovedEvent e => new ReactionRemovedIntegrationEvent(
            e.EventId, e.OccurredAt, e.MessageId, e.ChatId, e.UserId, e.Emoji.ToString()),

        _ => null
    };
}
