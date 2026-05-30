using TelegramLike.Domain.Messaging.ValueObjects;

namespace TelegramLike.Application.Messaging.Queries;

public sealed record AttachmentDto(AttachmentType Type, string Url, long SizeBytes, string? FileName);

public sealed record ReactionDto(Guid UserId, Emoji Emoji, DateTime AddedAt);

public sealed record MessageDto(
    Guid MessageId,
    Guid ChatId,
    Guid AuthorId,
    string? Text,
    IReadOnlyList<AttachmentDto> Attachments,
    Guid? ReplyToMessageId,
    Guid? ForwardOriginalMessageId,
    Guid? ForwardOriginalChatId,
    IReadOnlyList<ReactionDto> Reactions,
    bool IsRetracted,
    DateTime? RetractedAt,
    Guid? RetractedBy,
    int? BroadcastReadCount,
    DateTime SentAt);

public sealed record MessagePageDto(IReadOnlyList<MessageDto> Items, DateTime? NextCursor);
