using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Messaging.Api.Contracts;
using TelegramLike.Messaging.Application.Commands.AddReaction;
using TelegramLike.Messaging.Application.Commands.RemoveReaction;
using TelegramLike.Messaging.Domain.ValueObjects;

namespace TelegramLike.Messaging.Api.Controllers;

/// <summary>
/// Reactions on a message (embedded sub-resource, atomic <c>$push</c>/<c>$pull</c>):
/// add and remove. <c>userIsPremium</c> is a BFF-enriched input carried in the add body.
/// </summary>
[Authorize]
[Route("messages")]
public sealed class MessageReactionsController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("{messageId:guid}/reactions")]
    public async Task<IActionResult> Add(
        Guid messageId, [FromBody] AddReactionRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new AddReactionCommand(messageId, CurrentUserId, body.Emoji, body.UserIsPremium), cancellationToken);
        return NoContent();
    }

    // The emoji segment is a free string (the BFF sends the enum name, e.g. "Like"), parsed
    // case-insensitively. Kept off the JSON enum converter path on purpose — it is a route
    // value, and an unknown value returns the same 400 "Unknown emoji." the minimal API did.
    [HttpDelete("{messageId:guid}/reactions/{emoji}")]
    public async Task<IActionResult> Remove(Guid messageId, string emoji, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<Emoji>(emoji, ignoreCase: true, out var parsed))
            return Problem("Unknown emoji.", statusCode: StatusCodes.Status400BadRequest);

        await mediator.Send(new RemoveReactionCommand(messageId, CurrentUserId, parsed), cancellationToken);
        return NoContent();
    }
}
