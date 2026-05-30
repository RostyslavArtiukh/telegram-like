using MediatR;

namespace TelegramLike.Notifications.Application.Queries.GetNotificationFeed;

public sealed record GetNotificationFeedQuery(
    Guid RecipientId,
    DateTime? BeforeCreatedAt = null,
    int PageSize = 20,
    bool UnreadOnly = false) : IRequest<NotificationFeedDto>;
