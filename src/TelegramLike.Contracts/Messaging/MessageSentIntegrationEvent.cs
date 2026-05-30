using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

public sealed record MessageSentIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent;
