using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Chats.Api.Contracts;
using TelegramLike.Chats.Application.Commands.CreateBroadcastChannel;
using TelegramLike.Chats.Application.Commands.CreateDirectChat;
using TelegramLike.Chats.Application.Commands.CreateGroupChat;
using TelegramLike.Chats.Application.Commands.DeleteChat;
using TelegramLike.Chats.Application.Commands.RenameChat;
using TelegramLike.Chats.Application.Queries.GetChatById;
using TelegramLike.Chats.Application.Queries.GetMyChats;

namespace TelegramLike.Chats.Api.Controllers;

[Route("chats")]
[Authorize]
public sealed class ChatsController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("my")]
    public async Task<IActionResult> GetMyChats(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMyChatsQuery(CurrentUserId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{chatId:guid}")]
    public async Task<IActionResult> GetChatById(Guid chatId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetChatByIdQuery(chatId, CurrentUserId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("direct")]
    public async Task<IActionResult> CreateDirect([FromBody] CreateDirectChatRequest body, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDirectChatCommand(body.ChatId, CurrentUserId, body.PeerUserId), cancellationToken);
        return Created($"/chats/{id}", new ChatCreatedResponse(id));
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupChatRequest body, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateGroupChatCommand(body.ChatId, CurrentUserId, body.Name), cancellationToken);
        return Created($"/chats/{id}", new ChatCreatedResponse(id));
    }

    [HttpPost("broadcast")]
    public async Task<IActionResult> CreateBroadcast([FromBody] CreateBroadcastChannelRequest body, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateBroadcastChannelCommand(body.ChatId, CurrentUserId, body.Name), cancellationToken);
        return Created($"/chats/{id}", new ChatCreatedResponse(id));
    }

    [HttpPatch("{chatId:guid}")]
    public async Task<IActionResult> Rename(Guid chatId, [FromBody] RenameChatRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new RenameChatCommand(chatId, body.NewName, CurrentUserId), cancellationToken);
        return NoContent();
    }

    // Soft delete, Owner only. DirectChat rejects it in the aggregate.
    [HttpDelete("{chatId:guid}")]
    public async Task<IActionResult> Delete(Guid chatId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteChatCommand(chatId, CurrentUserId), cancellationToken);
        return NoContent();
    }
}
