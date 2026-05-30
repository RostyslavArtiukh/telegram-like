namespace TelegramLike.Contracts.Notifications;

public enum NotificationTypeContract
{
    NewMessage = 0,
    MentionInGroup = 1,
    MemberJoined = 2,
    MemberKicked = 3
}

public enum NotificationStatusContract
{
    Pending = 0,
    Delivered = 1,
    Read = 2
}

public sealed record NotificationApiDto(
    Guid Id,
    Guid RecipientId,
    NotificationTypeContract Type,
    Guid ChatId,
    Guid? MessageId,
    Guid? ActorId,
    NotificationStatusContract Status,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationFeedApiDto(
    IReadOnlyList<NotificationApiDto> Items,
    DateTime? NextCursor);
