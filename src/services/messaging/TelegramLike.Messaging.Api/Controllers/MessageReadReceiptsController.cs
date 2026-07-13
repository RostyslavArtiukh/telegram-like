using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

namespace TelegramLike.Messaging.Api.Controllers;

/// <summary>
/// Read receipts: marking a message as read. Direct/Group write to the
/// <c>message_read_receipts</c> read-model; Broadcast bumps the message read counter.
/// Broadcast-ness is derived server-side from the message ([TL-102]) — the request has no body.
/// </summary>
[Authorize]
[Route("messages")]
public sealed class MessageReadReceiptsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid messageId, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkMessageAsReadCommand(messageId, CurrentUserId), cancellationToken);
        return NoContent();
    }
}
