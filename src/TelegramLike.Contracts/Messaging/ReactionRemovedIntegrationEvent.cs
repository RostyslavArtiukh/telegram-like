using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

[IntegrationEventName("messaging.reaction-removed.v1")]
public sealed record ReactionRemovedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid UserId,
    string Emoji) : IIntegrationEvent;
