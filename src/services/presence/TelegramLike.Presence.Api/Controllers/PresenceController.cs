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
public sealed class PresenceController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public PresenceController(IMediator mediator) => _mediator = mediator;

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new HeartbeatCommand(userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("offline")]
    public async Task<IActionResult> GoOffline(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new GoOfflineCommand(userId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetUserPresence(Guid userId, CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetUserPresenceQuery(userId), cancellationToken);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> GetBatchPresence([FromBody] Guid[] userIds, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBatchPresenceQuery(userIds), cancellationToken);
        return Ok(result);
    }
}
