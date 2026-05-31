using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

public sealed record ReactionAddedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid UserId,
    string Emoji) : IIntegrationEvent;
