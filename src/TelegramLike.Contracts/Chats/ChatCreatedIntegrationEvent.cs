using TelegramLike.Contracts.Common;

namespace TelegramLike.Contracts.Chats;

/// <summary>
/// A chat was created. Carries the chat's immutable type so Messaging can materialize a
/// chat-type read-model and derive <c>isBroadcast</c> server-side ([TL-102]) instead of trusting
/// a client-supplied flag. Only Messaging consumes it. <see cref="Type"/> is the
/// <c>ChatType</c> name (<c>"Direct"</c>/<c>"Group"</c>/<c>"Broadcast"</c>).
/// </summary>
[IntegrationEventName("chats.chat-created.v1")]
public sealed record ChatCreatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ChatId,
    string Type) : IIntegrationEvent;
