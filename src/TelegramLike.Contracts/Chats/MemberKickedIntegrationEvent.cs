using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

[IntegrationEventName("chats.member-kicked.v1")]
public sealed record MemberKickedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    Guid UserId,
    Guid KickedBy,
    // This part's slice of the audience, not the whole chat — see MessageSentIntegrationEvent.
    IReadOnlyList<Guid> Recipients,
    int PartIndex = 0,
    int PartCount = 1) : IIntegrationEvent;
