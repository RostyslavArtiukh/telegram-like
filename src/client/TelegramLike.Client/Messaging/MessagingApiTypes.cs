using System.Text.Json.Serialization;

namespace TelegramLike.Client.Messaging;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttachmentType { Image, File, Audio, Video }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReactionEmoji { Like, Heart, Laugh, Wow, Sad, Angry, Fire, Dislike }

public sealed record MessageAttachment(AttachmentType Type, string Url, long SizeBytes, string? FileName);

public sealed record MessageReaction(Guid UserId, ReactionEmoji Emoji, DateTime AddedAt);

public sealed record ChatMessage(
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    string? Text,
    IReadOnlyList<MessageAttachment> Attachments,
    Guid? ReplyToMessageId,
    Guid? ForwardOriginalMessageId,
    Guid? ForwardOriginalChatId,
    IReadOnlyList<MessageReaction> Reactions,
    bool IsRetracted,
    DateTime? RetractedAt,
    Guid? RetractedBy,
    int? BroadcastReadCount,
    DateTime SentAt);

public sealed record ChatMessagePage(IReadOnlyList<ChatMessage> Items, DateTime? NextCursor);

public sealed record OutgoingAttachment(
    AttachmentType Type,
    string Url,
    long SizeBytes,
    string? FileName);
