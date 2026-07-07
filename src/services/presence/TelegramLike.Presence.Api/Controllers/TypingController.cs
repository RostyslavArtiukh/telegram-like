using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Presence.Application.Commands.StartTyping;
using TelegramLike.Presence.Application.Commands.StopTyping;
using TelegramLike.Presence.Application.Queries.GetTypingUsers;

namespace TelegramLike.Presence.Api.Controllers;

/// <summary>
/// Typing-indicator side of the presence API. Mirrors the former
/// <c>POST /presence/typing/{chatId}/start</c>, <c>POST /presence/typing/{chatId}/stop</c> and
/// <c>GET /presence/typing/{chatId}</c> minimal-API endpoints — routes, verbs, the
/// <c>:guid</c> route constraint, and 204/200/401 status codes preserved.
/// <para>
/// Membership validation (and its deliberate fail-open behaviour for unknown chats) lives in
/// <see cref="StartTypingCommand"/>'s handler; this is an API-layer refactor and does not touch it.
/// </para>
/// </summary>
[Route("presence/typing")]
[Authorize]
public sealed class TypingController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public TypingController(IMediator mediator) => _mediator = mediator;

    [HttpPost("{chatId:guid}/start")]
    public async Task<IActionResult> StartTyping(Guid chatId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new StartTypingCommand(chatId, userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/stop")]
    public async Task<IActionResult> StopTyping(Guid chatId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await _mediator.Send(new StopTypingCommand(chatId, userId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{chatId:guid}")]
    public async Task<IActionResult> GetTypingUsers(Guid chatId, CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new GetTypingUsersQuery(chatId), cancellationToken);
        return Ok(dto);
    }
}
