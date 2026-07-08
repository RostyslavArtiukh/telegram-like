using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Presence.Application.Commands.GoOffline;
using TelegramLike.Presence.Application.Commands.Heartbeat;
using TelegramLike.Presence.Application.Queries.GetBatchPresence;
using TelegramLike.Presence.Application.Queries.GetUserPresence;

namespace TelegramLike.Presence.Api.Controllers;

/// <summary>
/// Online-status side of the presence API: heartbeat/offline transitions and presence reads
/// (single + batch). Mirrors the former <c>POST /presence/heartbeat</c>, <c>POST /presence/offline</c>,
/// <c>GET /presence/{userId}</c> and <c>POST /presence/batch</c> minimal-API endpoints — routes,
/// verbs, route constraints, body bindings and 204/200/404/401 status codes preserved.
/// </summary>
[Route("presence")]
[Authorize]
public sealed class PresenceController(IMediator mediator) : ApiControllerBase
{
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        await mediator.Send(new HeartbeatCommand(CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("offline")]
    public async Task<IActionResult> GoOffline(CancellationToken cancellationToken)
    {
        await mediator.Send(new GoOfflineCommand(CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserPresence(Guid userId, CancellationToken cancellationToken)
    {
        var dto = await mediator.Send(new GetUserPresenceQuery(userId), cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> GetBatchPresence([FromBody] Guid[] userIds, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBatchPresenceQuery(userIds), cancellationToken);
        return Ok(result);
    }
}
