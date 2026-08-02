using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Messaging;

/// <summary>
/// A message was sent. Carries its own audience so Notifications never has to ask Chats who
/// is in the room — and is therefore split into parts once that audience gets large.
/// </summary>
/// <remarks>
/// <c>Recipients</c> holds this part's slice, not the whole chat. A part is a complete,
/// independently consumable event with its own <c>EventId</c>; the slices are disjoint, so a
/// per-recipient consumer just does its work and needs to know nothing about parts. A consumer
/// that acts <b>once per message</b> rather than once per recipient (a chat-wide push, say)
/// must do it only when <c>PartIndex == 0</c>, or it repeats that work once per part.
/// <para>
/// Additive and trailing, so a payload written before [TL-124] — which had no parts and no such
/// fields — deserializes as part 0 of 1, i.e. exactly the message it always was.
/// </para>
/// </remarks>
[IntegrationEventName("messaging.message-sent.v1")]
public sealed record MessageSentIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    IReadOnlyList<Guid> Recipients,
    int PartIndex = 0,
    int PartCount = 1) : IIntegrationEvent;
