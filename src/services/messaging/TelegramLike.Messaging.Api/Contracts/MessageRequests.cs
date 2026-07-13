using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Api.Contracts;

/// <summary>
/// Attachment payload as it arrives from the Web BFF. <see cref="Type"/> serialises as the
/// enum name (e.g. <c>"Image"</c>) thanks to the <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>
/// registered in <c>Program.cs</c>.
/// </summary>
public sealed record SendMessageAttachmentDto(AttachmentType Type, string Url, long SizeBytes, string? FileName);

/// <summary>
/// Body for <c>POST /messages/</c>. <see cref="Recipients"/> and <see cref="IsBroadcast"/> are
/// BFF-enriched cross-context inputs — the Web BFF resolves them from Chats data and supplies
/// them here so Messaging never cross-queries. Shape preserved verbatim from the minimal API.
/// </summary>
public sealed record SendMessageRequest(
    Guid ChatId,
    string? Text,
    IReadOnlyList<Guid> Recipients,
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
