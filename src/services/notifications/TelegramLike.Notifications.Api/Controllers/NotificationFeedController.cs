using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Notifications.Api.Contracts;
using TelegramLike.Notifications.Api.Mapping;
using TelegramLike.Notifications.Application.Queries.GetNotificationFeed;
using TelegramLike.Notifications.Application.Queries.GetUnreadCount;

namespace TelegramLike.Notifications.Api.Controllers;

/// <summary>
/// Read side of the notifications API: the paged feed and the unread-count badge value.
/// Mirrors the former <c>GET /notifications/</c> and <c>GET /notifications/unread-count</c>
/// minimal-API endpoints (routes, query bindings and 200/401 status codes preserved).
/// </summary>
[Route("notifications")]
[Authorize]
public sealed class NotificationFeedController(IMediator mediator) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetFeed(
        [FromQuery] DateTime? before,
        [FromQuery] int? pageSize,
        [FromQuery] bool? unreadOnly,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetNotificationFeedQuery(CurrentUserId, before, pageSize ?? 20, unreadOnly ?? false), cancellationToken);

        return Ok(result.ToContract());
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await mediator.Send(new GetUnreadCountQuery(CurrentUserId), cancellationToken);
        return Ok(new UnreadCountResponse(count));
    }
}
