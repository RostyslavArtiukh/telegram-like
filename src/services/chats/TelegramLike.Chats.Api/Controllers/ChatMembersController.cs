using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TelegramLike.Chats.Api.Contracts;
using TelegramLike.Chats.Application.Commands.ChangeMemberRole;
using TelegramLike.Chats.Application.Commands.JoinChat;
using TelegramLike.Chats.Application.Commands.KickMember;
using TelegramLike.Chats.Application.Commands.LeaveChat;
using TelegramLike.Chats.Application.Commands.TransferOwnership;
using TelegramLike.Chats.Application.Queries.GetChatMembers;

namespace TelegramLike.Chats.Api.Controllers;

[Route("chats")]
[Authorize]
public sealed class ChatMembersController(IMediator mediator) : ApiControllerBase
{
    [HttpGet("{chatId:guid}/members")]
    public async Task<IActionResult> GetChatMembers(Guid chatId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetChatMembersQuery(chatId, CurrentUserId), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{chatId:guid}/join")]
    public async Task<IActionResult> Join(Guid chatId, CancellationToken cancellationToken)
    {
        await mediator.Send(new JoinChatCommand(chatId, CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/leave")]
    public async Task<IActionResult> Leave(Guid chatId, CancellationToken cancellationToken)
    {
        await mediator.Send(new LeaveChatCommand(chatId, CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/members/{memberUserId:guid}/kick")]
    public async Task<IActionResult> KickMember(Guid chatId, Guid memberUserId, CancellationToken cancellationToken)
    {
        await mediator.Send(new KickMemberCommand(chatId, memberUserId, CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/members/{memberUserId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(
        Guid chatId, Guid memberUserId, [FromBody] ChangeMemberRoleRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new ChangeMemberRoleCommand(chatId, memberUserId, body.NewRole, CurrentUserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{chatId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(
        Guid chatId, [FromBody] TransferOwnershipRequest body, CancellationToken cancellationToken)
    {
        await mediator.Send(new TransferOwnershipCommand(chatId, body.NewOwnerUserId, CurrentUserId), cancellationToken);
        return NoContent();
    }
}
