using MediatR;

namespace TelegramLike.Notifications.Application.Queries.GetNotificationFeed;

public sealed class GetNotificationFeedQueryHandler(INotificationQueryService queryService)
    : IRequestHandler<GetNotificationFeedQuery, NotificationFeedDto>
{
    public Task<NotificationFeedDto> Handle(GetNotificationFeedQuery request, CancellationToken cancellationToken)
    {
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;
        return queryService.GetFeedAsync(
            request.RecipientId, request.BeforeCreatedAt, pageSize, request.UnreadOnly, cancellationToken);
    }
}
