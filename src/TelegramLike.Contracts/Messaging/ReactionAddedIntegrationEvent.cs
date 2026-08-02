using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

[IntegrationEventName("messaging.reaction-added.v1")]
public sealed record ReactionAddedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid UserId,
    string Emoji) : IIntegrationEvent;
