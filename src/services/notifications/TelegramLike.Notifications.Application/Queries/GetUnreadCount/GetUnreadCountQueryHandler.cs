using MediatR;

namespace TelegramLike.Notifications.Application.Queries.GetUnreadCount;

public sealed class GetUnreadCountQueryHandler(INotificationQueryService queryService)
    : IRequestHandler<GetUnreadCountQuery, long>
{
    public Task<long> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
        => queryService.GetUnreadCountAsync(request.RecipientId, cancellationToken);
}
