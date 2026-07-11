using TelegramLike.Notifications.Domain.ValueObjects;

namespace TelegramLike.Notifications.Application.Queries;

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientId,
    NotificationType Type,
    Guid ChatId,
    Guid? MessageId,
    Guid? TriggeredByUserId,
    NotificationStatus Status,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record NotificationFeedDto(
    IReadOnlyList<NotificationDto> Items,
    DateTime? NextCursor);
