using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Messaging.Api.Contracts;
using TelegramLike.Messaging.Application.Commands.MarkMessageAsRead;

namespace TelegramLike.Messaging.Api.Controllers;

/// <summary>
/// Read receipts: marking a message as read. Direct/Group write to the
/// <c>message_read_receipts</c> read-model; Broadcast bumps the message read counter.
/// <c>isBroadcast</c> is a BFF-enriched input that tells Messaging which path to take.
/// </summary>
[Authorize]
[Route("messages")]
public sealed class MessageReadReceiptsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid messageId, [FromBody] MarkAsReadRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new MarkMessageAsReadCommand(messageId, CurrentUserId, body.IsBroadcast), cancellationToken);
        return NoContent();
    }
}
