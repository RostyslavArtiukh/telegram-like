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
public sealed class MessageReadReceiptsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public MessageReadReceiptsController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid messageId, [FromBody] MarkAsReadRequest body, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new MarkMessageAsReadCommand(messageId, userId, body.IsBroadcast), cancellationToken);
        return NoContent();
    }
}
