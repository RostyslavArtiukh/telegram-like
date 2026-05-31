using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

public sealed record MemberLeftIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId) : IIntegrationEvent;
