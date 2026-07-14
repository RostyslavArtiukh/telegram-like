using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TelegramLike.Chats.Application.Commands.BackfillChatMemberships;

namespace TelegramLike.Chats.Api.Controllers;

/// <summary>
/// Internal operational endpoints. Not fronted by the public gateway (it only routes
/// <c>/chats/*</c>) — reached over the internal service network. Gated behind
/// <c>Admin:BackfillEnabled</c> so the surface stays hidden (404) unless deliberately turned on
/// for an ops window, on top of the standard service-JWT <c>[Authorize]</c>.
/// </summary>
[Route("admin")]
[Authorize]
public sealed class AdminController(IMediator mediator, IConfiguration configuration) : ApiControllerBase
{
    /// <summary>
    /// Republishes the current active membership of every chat as a dedicated snapshot event so
    /// the Messaging + Presence read-models materialize chats that predate them. One-time, idempotent.
    /// </summary>
    [HttpPost("backfill/chat-memberships")]
    public async Task<IActionResult> BackfillChatMemberships(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Admin:BackfillEnabled"))
            return NotFound();

        var result = await mediator.Send(new BackfillChatMembershipsCommand(), cancellationToken);
        return Ok(result);
    }
}
