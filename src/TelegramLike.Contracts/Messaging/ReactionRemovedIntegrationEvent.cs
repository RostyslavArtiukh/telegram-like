using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

public sealed record ReactionRemovedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid UserId,
    string Emoji) : IIntegrationEvent;
