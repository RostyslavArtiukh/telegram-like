using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Notifications.Application.Commands.MarkAllNotificationsAsRead;
using TelegramLike.Notifications.Application.Commands.MarkChatNotificationsAsRead;
using TelegramLike.Notifications.Application.Commands.MarkNotificationAsRead;

namespace TelegramLike.Notifications.Api.Controllers;

/// <summary>
/// Write side of the notifications API: marking notifications as read (single, all, per-chat).
/// Mirrors the former <c>POST /notifications/{id}/read</c>, <c>POST /notifications/read-all</c>
/// and <c>POST /notifications/chats/{chatId}/read</c> minimal-API endpoints. All three return
/// 204 on success; <c>{id}/read</c> surfaces handler <see cref="InvalidOperationException"/> as a
/// 400 ProblemDetails via the global <c>DomainExceptionFilter</c> (matching the old try/catch).
/// </summary>
[Route("notifications")]
[Authorize]
public sealed class NotificationReadController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkNotificationAsReadCommand(id, CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkAllNotificationsAsReadCommand(CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("chats/{chatId:guid}/read")]
    public async Task<IActionResult> MarkChatAsRead(Guid chatId, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkChatNotificationsAsReadCommand(CurrentUserId, chatId), cancellationToken);
        return NoContent();
    }
}
