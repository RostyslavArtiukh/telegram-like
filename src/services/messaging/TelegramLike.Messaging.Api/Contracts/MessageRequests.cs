using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Api.Contracts;

/// <summary>
/// Attachment payload as it arrives from the Web BFF. <see cref="Type"/> serialises as the
/// enum name (e.g. <c>"Image"</c>) thanks to the <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>
/// registered in <c>Program.cs</c>.
/// </summary>
public sealed record SendMessageAttachmentDto(AttachmentType Type, string Url, long SizeBytes, string? FileName);

/// <summary>
/// Body for <c>POST /messages/</c>. There is no <c>recipients</c> field ([TL-118]): the audience
/// is derived server-side from the membership read-model, so callers can neither supply nor spoof
/// it. <see cref="IsBroadcast"/> remains a caller-supplied fallback for a chat the chat-type
/// read-model hasn't materialized yet.
/// </summary>
public sealed record SendMessageRequest(
    Guid ChatId,
    string? Text,
    bool IsBroadcast,
    IReadOnlyList<SendMessageAttachmentDto>? Attachments = null,
    Guid? ReplyToMessageId = null,
    Guid? ForwardOriginalMessageId = null,
    Guid? ForwardOriginalChatId = null,
    // Client-generated duplicate-protection key = the message's id. Empty/absent => the
    // service mints one. A retried send reuses it so the message isn't duplicated.
    Guid MessageId = default);

/// <summary>
/// Body for <c>POST /messages/{messageId}/reactions</c>. Premium status is no longer taken from
/// the body ([TL-102]) — it is read from the signed <c>premium</c> JWT claim server-side.
/// <see cref="Emoji"/> serialises as its enum name.
/// </summary>
public sealed record AddReactionRequest(Emoji Emoji);

/// <summary>
/// Body for <c>POST /messages/{messageId}/retract</c>. <see cref="RetractedByModerator"/> is a
/// BFF-enriched input.
/// </summary>
public sealed record RetractMessageRequest(bool RetractedByModerator);
