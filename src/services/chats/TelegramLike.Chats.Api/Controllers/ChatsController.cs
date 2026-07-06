using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Chats.Api.Contracts;
using TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;
using TelegramLike.Chats.Application.Commands.CreateDirectChat;
using TelegramLike.Chats.Application.Commands.CreateGroupChat;
using TelegramLike.Chats.Application.Commands.RenameChat;
using TelegramLike.Chats.Application.Queries.GetChatById;
using TelegramLike.Chats.Application.Queries.GetMyChats;

namespace TelegramLike.Chats.Api.Controllers;

[Route("chats")]
[Authorize]
public sealed class ChatsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public ChatsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("my")]
    public async Task<IActionResult> GetMyChats(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _mediator.Send(new GetMyChatsQuery(userId), ct);
        return Ok(result);
    }

    [HttpGet("{chatId:guid}")]
    public async Task<IActionResult> GetChatById(Guid chatId, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await _mediator.Send(new GetChatByIdQuery(chatId, userId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("direct")]
    public async Task<IActionResult> CreateDirect([FromBody] CreateDirectChatRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var id = await _mediator.Send(new CreateDirectChatCommand(body.ChatId, userId, body.PeerUserId), ct);
        return Created($"/chats/{id}", new ChatCreatedResponse(id));
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupChatRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var id = await _mediator.Send(new CreateGroupChatCommand(body.ChatId, userId, body.Name), ct);
        return Created($"/chats/{id}", new ChatCreatedResponse(id));
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> CreateBroadcast([FromBody] CreateBroadcastChannelRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var id = await _mediator.Send(new CreateBroadcastChannelCommand(body.ChatId, userId, body.Name), ct);
        return Created($"/chats/{id}", new ChatCreatedResponse(id));
    }

    [HttpPatch("{chatId:guid}")]
    public async Task<IActionResult> Rename(Guid chatId, [FromBody] RenameChatRequest body, CancellationToken ct)
    {
        if (!TryGetUserId(out var actorId)) return Unauthorized();
        await _mediator.Send(new RenameChatCommand(chatId, body.NewName, actorId), ct);
        return NoContent();
    }
}
