namespace TelegramLike.Contracts.Notifications;

public enum NotificationType
{
    NewMessage = 0,
    MentionInGroup = 1,
    MemberJoined = 2,
    MemberKicked = 3
}

public enum NotificationStatus
{
    Pending = 0,
    Delivered = 1,
    Read = 2
}

public sealed record NotificationApiDto(
    Guid Id,
    Guid RecipientId,
    NotificationType Type,
    Guid ChatId,
    Guid? MessageId,
    Guid? ActorId,
    NotificationStatus Status,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationFeedApiDto(
    IReadOnlyList<NotificationApiDto> Items,
    DateTime? NextCursor);
