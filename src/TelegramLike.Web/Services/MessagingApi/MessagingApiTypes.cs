using System.Text.Json.Serialization;

namespace TelegramLike.Web.Services.MessagingApi;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttachmentTypeContract { Image, File, Audio, Video }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EmojiContract { Like, Heart, Laugh, Wow, Sad, Angry, Fire, Dislike }

public sealed record AttachmentContract(AttachmentTypeContract Type, string Url, long SizeBytes, string? FileName);

public sealed record ReactionContract(Guid UserId, EmojiContract Emoji, DateTime AddedAt);

public sealed record MessageContract(
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    string? Text,
    IReadOnlyList<AttachmentContract> Attachments,
    Guid? ReplyToMessageId,
    Guid? ForwardOriginalMessageId,
    Guid? ForwardOriginalChatId,
    IReadOnlyList<ReactionContract> Reactions,
    bool IsRetracted,
    DateTime? RetractedAt,
    Guid? RetractedBy,
    int? BroadcastReadCount,
    DateTime SentAt);

public sealed record MessagePageContract(IReadOnlyList<MessageContract> Items, DateTime? NextCursor);

public sealed record SendMessageAttachmentContract(
    AttachmentTypeContract Type,
    string Url,
    long SizeBytes,
    string? FileName);
